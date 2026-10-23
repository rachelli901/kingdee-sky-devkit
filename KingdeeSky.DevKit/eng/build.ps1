# Author: rachelli901 <rachelli901@users.noreply.github.com>
# Repository: https://github.com/rachelli901/kingdee-sky-devkit

# ============================================================
#  KingdeeSky.DevKit - production build script
#  Steps: secret scan -> compile WebApiClient (zero Kingdee deps)
#         -> run test gate ->
#         optional Plugins build (needs Kingdee.BOS DLLs) ->
#         produce versioned package under artifacts\
#  Usage:
#    powershell -NoProfile -ExecutionPolicy Bypass -File eng\build.ps1
#    powershell ... -File eng\build.ps1 1.1.0 -SkipPlugins
#    powershell ... -File eng\build.ps1 -SkipSecretScan
# ============================================================

param(
    [Parameter(Position=0)][string]$Version = '',
    [switch]$SkipPlugins,
    [switch]$SkipTests,
    [switch]$SkipSecretScan
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$srcProj = Join-Path $root 'src\KingdeeSky.WebApiClient'
$pluginProj = Join-Path $root 'src\KingdeeSky.Plugins\KingdeeSky.Plugins.csproj'
$libDir = Join-Path $root 'lib'
$testsScript = Join-Path $root 'tests\run-tests.ps1'
$secretScan = Join-Path $PSScriptRoot 'scan-secrets.ps1'
$artifacts = Join-Path $root 'artifacts'
$versionFile = Join-Path $PSScriptRoot 'version.txt'

if ([string]::IsNullOrEmpty($Version)) {
    if (Test-Path $versionFile) { $Version = (Get-Content $versionFile -Raw).Trim() }
    if ([string]::IsNullOrEmpty($Version)) { $Version = '0.0.0' }
}
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$pkgName = 'KingdeeSky.DevKit-' + $Version + '-' + $stamp
$stageRoot = Join-Path $env:TEMP ('ksd-pkg-' + [Guid]::NewGuid().ToString('N'))
$stageClient = Join-Path $stageRoot 'WebApiClient'
$stagePlugins = Join-Path $stageRoot 'Plugins'
$stageDeploy = Join-Path $stageRoot 'deploy'
$stageLib = Join-Path $stageRoot 'lib'

# OS install root from %WINDIR% (the system drive is not always C:); .NET Framework lives under a fixed subpath
$winDir = $env:WINDIR

function Resolve-Csc {
    $cand = @(
        (Join-Path $winDir 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
        (Join-Path $winDir 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
    )
    foreach ($p in $cand) { if (Test-Path $p) { return $p } }
    throw 'csc.exe (.NET Framework 4.8) not found'
}

function Resolve-Msbuild {
    $vswhere = ${env:ProgramFiles(x86)} + '\Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' 2>$null | Select-Object -First 1
        if ($found) { return $found }
    }
    $fx = (Join-Path $winDir 'Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe')
    if (Test-Path $fx) { return $fx }
    return $null
}

Write-Output ('==> Version      : ' + $Version)
Write-Output ('==> Package name : ' + $pkgName)

# ---------- 0. secret scan (plaintext secret gate) ----------
if (-not $SkipSecretScan) {
    Write-Output '==> [0/4] Secret scan (eng\scan-secrets.ps1)'
    & $secretScan -Root $root
    if ($LASTEXITCODE -ne 0) { throw 'Secret scan failed exit=' + $LASTEXITCODE }
} else {
    Write-Output '==> [0/4] Skipping secret scan (-SkipSecretScan)'
}

# ---------- 1. compile WebApiClient (Release, zero Kingdee deps) ----------
$csc = Resolve-Csc
$outDir = Join-Path $srcProj 'bin\Release'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
$exePath = Join-Path $outDir 'KingdeeSky.WebApiClient.exe'
$srcFiles = @(Get-ChildItem $srcProj -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)(\\|$)' } |
    Sort-Object FullName | ForEach-Object { $_.FullName })
if ($srcFiles.Count -eq 0) { throw 'No C# source files found under ' + $srcProj }

Write-Output '==> [1/4] Compiling WebApiClient (csc, .NET Framework 4.8)'
$cscArgs = @('/nologo','/target:exe','/optimize+','/debug-',('/out:' + $exePath),
    '/r:System.dll','/r:System.Core.dll','/r:System.Configuration.dll',
    '/r:System.Security.dll','/r:System.Web.Extensions.dll','/r:System.Xml.dll') + $srcFiles
& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw 'WebApiClient compile failed exit=' + $LASTEXITCODE }
if (-not (Test-Path $exePath)) { throw 'Compile reported success but exe missing: ' + $exePath }
Write-Output ('    -> ' + $exePath)

# ---------- 2. test gate ----------
if (-not $SkipTests) {
    Write-Output '==> [2/4] Running test gate'
    & $testsScript
    if ($LASTEXITCODE -ne 0) { throw 'Test gate failed exit=' + $LASTEXITCODE }
} else {
    Write-Output '==> [2/4] Skipping test gate (-SkipTests)'
}

# ---------- 3. optional Plugins build and package ----------
$pluginBuilt = $false
if (-not $SkipPlugins) {
    $bosProbe = Join-Path $libDir 'Kingdee.BOS.Core.dll'
    if (Test-Path $bosProbe) {
        $msbuild = Resolve-Msbuild
        if ($msbuild) {
            $pluginOut = Join-Path $root 'src\KingdeeSky.Plugins\bin\Release'
            Write-Output '==> [3/4] Building Plugins (Kingdee.BOS DLLs present)'
            & $msbuild $pluginProj /t:Build /p:Configuration=Release "/p:KingdeeBinsDir=$libDir" /v:m /nologo
            if ($LASTEXITCODE -ne 0) { throw 'Plugins build failed exit=' + $LASTEXITCODE }
            $pluginDll = Join-Path $pluginOut 'KingdeeSky.Plugins.dll'
            if (Test-Path $pluginDll) { $pluginBuilt = $true }
        } else {
            Write-Warning 'Kingdee.BOS DLLs present but MSBuild not found; skipping Plugins build'
        }
    } else {
        Write-Warning ('Kingdee.BOS DLLs not found in ' + $libDir + '; skipping Plugins build. ' +
            'Run lib\copy-kingdee-dlls.bat from an authorized K3Cloud server to enable it.')
    }
} else {
    Write-Output '==> [3/4] Skipping Plugins build (-SkipPlugins)'
}

# ---------- stage package ----------
Write-Output '==> Staging package'
New-Item -ItemType Directory -Path $stageClient -Force | Out-Null
New-Item -ItemType Directory -Path $stageDeploy -Force | Out-Null
New-Item -ItemType Directory -Path $stageLib -Force | Out-Null

Copy-Item $exePath (Join-Path $stageClient 'KingdeeSky.WebApiClient.exe') -Force
Copy-Item (Join-Path $srcProj 'App.config') (Join-Path $stageClient 'KingdeeSky.WebApiClient.exe.config') -Force
Copy-Item (Join-Path $srcProj 'App.config.example') $stageClient -Force
Copy-Item (Join-Path $srcProj 'secrets.env.example') $stageClient -Force
if (Test-Path (Join-Path $outDir 'KingdeeSky.WebApiClient.pdb')) {
    Copy-Item (Join-Path $outDir 'KingdeeSky.WebApiClient.pdb') $stageClient -Force
}

if ($pluginBuilt) {
    New-Item -ItemType Directory -Path $stagePlugins -Force | Out-Null
    Copy-Item (Join-Path $root 'src\KingdeeSky.Plugins\bin\Release\KingdeeSky.Plugins.dll') $stagePlugins -Force
    if (Test-Path (Join-Path $root 'src\KingdeeSky.Plugins\bin\Release\KingdeeSky.Plugins.pdb')) {
        Copy-Item (Join-Path $root 'src\KingdeeSky.Plugins\bin\Release\KingdeeSky.Plugins.pdb') $stagePlugins -Force
    }
}

Get-ChildItem (Join-Path $root 'deploy') -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring((Join-Path $root 'deploy').Length).TrimStart('\')
    $relDir = Split-Path $rel -Parent
    $destParent = $stageDeploy
    if (-not [string]::IsNullOrEmpty($relDir)) { $destParent = Join-Path $stageDeploy $relDir }
    if (-not (Test-Path $destParent)) { New-Item -ItemType Directory -Path $destParent -Force | Out-Null }
    Copy-Item $_.FullName (Join-Path $stageDeploy $rel) -Force
}
Get-ChildItem $libDir -File | ForEach-Object { Copy-Item $_.FullName $stageLib -Force }

Copy-Item (Join-Path $root 'README.md') $stageRoot -Force
Copy-Item $versionFile (Join-Path $stageRoot 'VERSION.txt') -Force

# ---------- compress ----------
if (-not (Test-Path $artifacts)) { New-Item -ItemType Directory -Path $artifacts -Force | Out-Null }
$zip = Join-Path $artifacts ($pkgName + '.zip')
Compress-Archive -Path (Join-Path $stageRoot '*') -DestinationPath $zip -CompressionLevel Optimal -Force

Remove-Item $stageRoot -Recurse -Force -ErrorAction SilentlyContinue

Write-Output '============================================'
Write-Output ('  Package created: ' + $zip)
if ($pluginBuilt) { Write-Output '  Plugins: INCLUDED in package' }
else { Write-Output '  Plugins: skipped (Kingdee.BOS DLLs not available)' }
Write-Output '============================================'