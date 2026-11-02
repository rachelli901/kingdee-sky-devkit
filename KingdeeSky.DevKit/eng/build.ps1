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
    [switch]$SkipSecretScan,
    # Number of release packages kept under artifacts\. Older packages and their
    # SHA-256 sidecars are pruned after a successful build. 0 disables pruning.
    [int]$KeepArtifacts = 5
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

# Secret-scan scope is the repository root, not this DevKit folder: the runnable
# demo, the browser console and the deploy deliverables share the same repository
# and must pass the same gate. Falls back to the DevKit root outside a checkout.
$repoRoot = $root
try {
    $top = & git -C $root rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($top)) {
        $repoRoot = (Resolve-Path -LiteralPath $top).Path
    }
} catch { }

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
    # Fallback: the .NET Framework MSBuild shipped with the OS. It only loads
    # projects up to ToolsVersion 4.0 and crashes (0xC0000005) on this solution's
    # ToolsVersion 15.0, so it must never be used for a real build.
    $fx = (Join-Path $winDir 'Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe')
    if (Test-Path $fx) {
        Write-Warning ('Only the legacy .NET Framework MSBuild was found at ' + $fx +
            '; it cannot load ToolsVersion 15.0 projects. Install Visual Studio or the ' +
            'Build Tools to build the Plugins project.')
        return $null
    }
    return $null
}

Write-Output ('==> Version      : ' + $Version)
Write-Output ('==> Package name : ' + $pkgName)

# ---------- 0. secret scan (plaintext secret gate) ----------
if (-not $SkipSecretScan) {
    Write-Output '==> [0/4] Secret scan (eng\scan-secrets.ps1, repo root)'
    & $secretScan -Root $repoRoot
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
# Remove stale symbols before compiling. The csc invocation below uses /debug-,
# so it produces no .pdb at all; anything left in bin\ came from an earlier
# Visual Studio build and would not match the exe shipped next to it.
Get-ChildItem $outDir -Filter '*.pdb' -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
$cscArgs = @('/nologo','/target:exe','/optimize+','/debug-',('/out:' + $exePath),
    '/r:System.dll','/r:System.Core.dll','/r:System.Configuration.dll',
    '/r:System.Security.dll','/r:System.Web.Extensions.dll','/r:System.Xml.dll') + $srcFiles
& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw 'WebApiClient compile failed exit=' + $LASTEXITCODE }
if (-not (Test-Path $exePath)) { throw 'Compile reported success but exe missing: ' + $exePath }
Write-Output ('    -> ' + $exePath)

# Refresh the runtime configuration next to the built exe.
# csc.exe does not copy App.config the way MSBuild does, so without this step
# bin\<Config>\KingdeeSky.WebApiClient.exe.config stays stale and any tool that
# runs the exe in place (VS Code self-check task, manual runs) silently keeps
# using an old ServerUrl / AcctId / key names.
$exeConfigPath = Join-Path $outDir 'KingdeeSky.WebApiClient.exe.config'
Copy-Item (Join-Path $srcProj 'App.config') $exeConfigPath -Force
Write-Output ('    -> ' + $exeConfigPath + ' (refreshed from src\App.config)')

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
        $writeBackPlugin = Join-Path $root 'src\KingdeeSky.Plugins\Convert\PurchaseInStockWriteBackPlugin.cs'
        if (Test-Path $writeBackPlugin) {
            $writeBackText = Get-Content $writeBackPlugin -Raw
            if ($writeBackText -match 'TargetRuleId\s*=\s*"在此粘贴反写规则GUID"') {
                throw 'Plugins build blocked: PurchaseInStockWriteBackPlugin.TargetRuleId is still a placeholder. Replace it with the real rule GUID before building.'
            }
        }
        $msbuild = Resolve-Msbuild
        if ($msbuild) {
            $pluginOut = Join-Path $root 'src\KingdeeSky.Plugins\bin\Release'
            # Drop stale symbols so the package never ships a .pdb that does not
            # match the plugin dll next to it.
            Get-ChildItem $pluginOut -Filter '*.pdb' -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
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
# Symbols are shipped only when this build produced them. Stale ones were
# removed right before compiling, so anything here belongs to this build.
$exePdb = Join-Path $outDir 'KingdeeSky.WebApiClient.pdb'
if (Test-Path $exePdb) {
    Copy-Item $exePdb $stageClient -Force
}

if ($pluginBuilt) {
    New-Item -ItemType Directory -Path $stagePlugins -Force | Out-Null
    $pluginDllPath = Join-Path $root 'src\KingdeeSky.Plugins\bin\Release\KingdeeSky.Plugins.dll'
    Copy-Item $pluginDllPath (Join-Path $stagePlugins 'KingdeeSky.Plugins.dll') -Force
    $pluginPdb = $pluginDllPath + '.pdb'
    if (Test-Path $pluginPdb) {
        Copy-Item $pluginPdb $stagePlugins -Force
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

# The browser console and the V9.1 deployment handbook live in the repository
# root's deploy\ folder, not under KingdeeSky.DevKit\deploy\. Both are listed as
# customer entry points in the README and the getting-started guide, so they
# must travel with the package - otherwise the customer follows the manual and
# the file is simply not there.
$repoDeploy = Join-Path $repoRoot 'deploy'
if (Test-Path -LiteralPath $repoDeploy) {
    Get-ChildItem -LiteralPath $repoDeploy -Filter '*.html' -File | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $stageDeploy $_.Name) -Force
        Write-Output ('    staged deliverable: deploy\' + $_.Name)
    }
}
Get-ChildItem $libDir -File | ForEach-Object { Copy-Item $_.FullName $stageLib -Force }

Copy-Item (Join-Path $root 'README.md') $stageRoot -Force
Copy-Item $versionFile (Join-Path $stageRoot 'VERSION.txt') -Force

# ---------- compress ----------
if (-not (Test-Path $artifacts)) { New-Item -ItemType Directory -Path $artifacts -Force | Out-Null }
$zip = Join-Path $artifacts ($pkgName + '.zip')
Compress-Archive -Path (Join-Path $stageRoot '*') -DestinationPath $zip -CompressionLevel Optimal -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$hashFile = $zip + '.sha256'
Set-Content -LiteralPath $hashFile -Value ($hash + '  ' + (Split-Path $zip -Leaf)) -Encoding ASCII
$writtenHash = (Get-Content -LiteralPath $hashFile -Raw).Trim().Split(' ')[0]
if ($writtenHash -ne $hash) { throw 'SHA-256 sidecar verification failed' }

# ---------- prune old packages ----------
# Keeps artifacts\ bounded so a delivery folder does not accumulate every
# historical build. Runs only AFTER the new package and its sidecar verified,
# so a failed build can never delete the existing history.
# Ordering uses the timestamp embedded in the file name, not the file name and
# not mtime: the version segment sorts lexicographically (1.0.10 < 1.0.9) and
# mtime changes whenever a package is copied around.
if ($KeepArtifacts -gt 0) {
    $pruned = 0
    $packages = @()
    if (Test-Path $artifacts) {
        $packages = @(Get-ChildItem -LiteralPath $artifacts -Filter 'KingdeeSky.DevKit-*.zip' -File)
    }
    $ordered = @($packages | Sort-Object -Property @{
        Expression = {
            if ($_.Name -match '(\d{8}-\d{6})') { $matches[1] } else { '00000000-000000' }
        }
    } -Descending)
    if ($ordered.Count -gt $KeepArtifacts) {
        foreach ($old in $ordered[$KeepArtifacts..($ordered.Count - 1)]) {
            $oldSidecar = $old.FullName + '.sha256'
            Remove-Item -LiteralPath $old.FullName -Force
            if (Test-Path -LiteralPath $oldSidecar) { Remove-Item -LiteralPath $oldSidecar -Force }
            $pruned++
            Write-Output ('    pruned old package: ' + $old.Name)
        }
        Write-Output ('    kept newest ' + $KeepArtifacts + ' package(s)')
    }
    # A kept package must stay verifiable: the SHA-256 of a package is the hash of
    # its bytes, so a sidecar can be recomputed for packages built before the
    # sidecar existed or whose sidecar was removed.
    foreach ($kept in @($ordered | Select-Object -First $KeepArtifacts)) {
        $keptSidecar = $kept.FullName + '.sha256'
        if (-not (Test-Path -LiteralPath $keptSidecar)) {
            $keptHash = (Get-FileHash -LiteralPath $kept.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            Set-Content -LiteralPath $keptSidecar -Value ($keptHash + '  ' + $kept.Name) -Encoding ASCII
            Write-Output ('    restored missing SHA-256 sidecar: ' + $kept.Name)
        }
    }
    # Drop sidecars whose package is gone (orphans left by an interrupted build).
    if (Test-Path $artifacts) {
        Get-ChildItem -LiteralPath $artifacts -Filter '*.sha256' -File | ForEach-Object {
            $zipPath = $_.FullName.Substring(0, $_.FullName.Length - '.sha256'.Length)
            if (-not (Test-Path -LiteralPath $zipPath)) {
                Remove-Item -LiteralPath $_.FullName -Force
                Write-Output ('    pruned orphan sidecar: ' + $_.Name)
            }
        }
    }
    if ($pruned -eq 0) { Write-Output ('    nothing to prune (' + $ordered.Count + ' package(s) kept)') }
}

Remove-Item $stageRoot -Recurse -Force -ErrorAction SilentlyContinue

Write-Output '============================================'
Write-Output ('  Package created: ' + $zip)
Write-Output ('  SHA-256        : ' + $hash)
if ($pluginBuilt) { Write-Output '  Plugins: INCLUDED in package' }
else { Write-Output '  Plugins: skipped (Kingdee.BOS DLLs not available)' }
Write-Output '============================================'
