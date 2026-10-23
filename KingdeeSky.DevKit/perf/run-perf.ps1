# Author: rachelli901 <rachelli901@users.noreply.github.com>
# Repository: https://github.com/rachelli901/kingdee-sky-devkit

# run-perf.ps1 - client-side performance smoke: start stub -> compile PerfProbe -> run -> save baseline.
# ASCII-only (Windows PowerShell 5.1 safe; no Chinese string literals).
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\perf\run-perf.ps1
# Exit codes: 0 = pass; probe exit (1/2/3) = query/login failure or threshold; others = infra failure.
param(
    [string]$Root = '',
    [string]$ServerUrl = 'http://127.0.0.1:18081/K3Cloud/',
    [int]$Iterations = 20,
    [int]$StubPort = 18081,
    [int]$DelayMs = 0,
    [int]$ThresholdMs = 5000
)
$ErrorActionPreference = 'Stop'

# repo root: derived from this script's location (perf/) -> works for local and CI checkouts
if ([string]::IsNullOrWhiteSpace($Root)) { $Root = Split-Path -Parent $PSScriptRoot }

$perf = Join-Path $Root 'perf'
$src = Join-Path $Root 'src\KingdeeSky.WebApiClient'
$outDir = Join-Path $perf 'bin\Debug'
$exe = Join-Path $outDir 'PerfProbe.exe'
$stubPy = Join-Path $perf 'stub_server.py'
if (-not (Test-Path $perf)) { New-Item -ItemType Directory -Path $perf -Force | Out-Null }
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

Write-Output '==> [1/5] Check stub port'
$listener = Get-NetTCPConnection -LocalPort $StubPort -State Listen -ErrorAction SilentlyContinue
if ($listener) {
    Write-Error ('Port ' + $StubPort + ' already in use; stop the existing stub first.')
    exit 20
}

Write-Output '==> [2/5] Start stub server'
$py = (Get-Command python -ErrorAction SilentlyContinue)
if (-not $py) { Write-Error 'python not found in PATH'; exit 22 }
$stub = Start-Process python -ArgumentList @($stubPy, '-p', $StubPort, '-d', $DelayMs) -PassThru -WindowStyle Hidden
$ready = $false
for ($i = 0; $i -lt 20; $i++) {
    try {
        $tcp = New-Object System.Net.Sockets.TcpClient
        $tcp.Connect('127.0.0.1', $StubPort)
        $tcp.Close()
        $ready = $true
        break
    } catch {
        Start-Sleep -Milliseconds 200
    }
}
if (-not $ready) {
    if ($stub -and -not $stub.HasExited) { Stop-Process -Id $stub.Id -Force -ErrorAction SilentlyContinue }
    Write-Error 'Stub did not become ready in time'; exit 21
}
Write-Output ('==> Stub up on 127.0.0.1:' + $StubPort)

Write-Output '==> [3/5] Compile PerfProbe (csc, .NET Framework 4.8, zero Kingdee deps)'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) {
    if ($stub -and -not $stub.HasExited) { Stop-Process -Id $stub.Id -Force -ErrorAction SilentlyContinue }
    throw 'csc.exe (NET Framework 4.8) not found'
}
$refs = @(
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Configuration.dll',
    '/r:System.Security.dll',
    '/r:System.Web.Extensions.dll',
    '/r:System.Xml.dll'
)
$srcFiles = @(
    'KingdeeWebApiClient.cs','Scenarios.cs','Logging\SafeLogger.cs',
    'Security\AppIdAppSecAuthProvider.cs','Security\AuthProviderFactory.cs','Security\DpapiSecretProvider.cs',
    'Security\EnvironmentSecretProvider.cs','Security\FileSecretProvider.cs','Security\HmacSha256AppSecSigner.cs',
    'Security\IAuthProvider.cs','Security\IAppSecSigner.cs','Security\ISecretProvider.cs',
    'Security\KmsSecretProvider.cs','Security\OAuth2AuthProvider.cs','Security\PasswordAuthProvider.cs',
    'Security\SecretProviderFactory.cs','Security\SecretRedactor.cs','Security\TransportSecurityPolicy.cs'
)
$cscArgs = @('/nologo','/target:exe',('/out:' + $exe)) + $refs
$cscArgs += (Join-Path $perf 'PerfProbe.cs')
foreach ($f in $srcFiles) { $cscArgs += (Join-Path $src $f) }
& $csc $cscArgs
if ($LASTEXITCODE -ne 0) {
    if ($stub -and -not $stub.HasExited) { Stop-Process -Id $stub.Id -Force -ErrorAction SilentlyContinue }
    Write-Error ('COMPILE FAILED exit=' + $LASTEXITCODE); exit $LASTEXITCODE
}
Write-Output ('==> Compiled: ' + $exe)

Write-Output '==> [4/5] Run perf probe (inject KINGDEE_PASSWORD, scrubbed output)'
$env:KINGDEE_PASSWORD = 'demo123'
$out = & $exe -url $ServerUrl -iterations $Iterations -thresholdMs $ThresholdMs 2>&1
$probeExit = $LASTEXITCODE
Remove-Item Env:KINGDEE_PASSWORD -ErrorAction SilentlyContinue

Write-Output '==> [5/5] Save baseline and cleanup'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$baseline = Join-Path $perf ('perf-baseline-' + $stamp + '.txt')
$latest = Join-Path $perf 'perf-baseline-latest.txt'
$header = '# perf baseline ' + $stamp + ' iterations=' + $Iterations + ' delayMs=' + $DelayMs + ' thresholdMs=' + $ThresholdMs + ' url=' + $ServerUrl
$content = $header + "`r`n" + ($out -join "`r`n") + "`r`n" + ('exit=' + $probeExit)
$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($baseline, $content, $utf8)
Copy-Item $baseline $latest -Force
Write-Output ('==> Baseline saved: ' + $baseline)

Write-Output $out

if ($stub -and -not $stub.HasExited) { Stop-Process -Id $stub.Id -Force -ErrorAction SilentlyContinue }

if ($probeExit -ne 0) { Write-Error ('PERF PROBE FAILED exit=' + $probeExit); exit $probeExit }
Write-Output '==> PERF OK'
exit 0