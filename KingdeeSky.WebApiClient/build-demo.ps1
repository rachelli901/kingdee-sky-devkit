# Author: rachelli901 <rachelli901@users.noreply.github.com>
# Repository: https://github.com/rachelli901/kingdee-sky-devkit

# ============================================================
#  Build the runnable demo client.
#  The demo has exactly one source of truth - the DevKit project
#  KingdeeSky.DevKit/src/KingdeeSky.WebApiClient. This script compiles it with
#  the .NET Framework csc (zero Kingdee dependencies) into the local app/
#  output directory, which is a build artifact and is NOT tracked by git.
#  Usage:
#    powershell -NoProfile -ExecutionPolicy Bypass -File build-demo.ps1
# ============================================================
$ErrorActionPreference = 'Stop'

$here = $PSScriptRoot
if ([string]::IsNullOrEmpty($here)) { $here = Split-Path -Parent $MyInvocation.MyCommand.Path }
$src = Join-Path (Split-Path -Parent $here) 'KingdeeSky.DevKit\src\KingdeeSky.WebApiClient'
$outDir = Join-Path $here 'app'

if (-not (Test-Path (Join-Path $src 'KingdeeWebApiClient.cs'))) {
    throw ('demo source not found: ' + $src)
}

# .NET Framework csc, resolved from %WINDIR% (the system drive is not always C:)
$csc = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { throw 'csc.exe (.NET Framework 4.8) not found' }

New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$sources = @(Get-ChildItem $src -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)(\\|$)' } |
    Sort-Object FullName | ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) { throw ('no C# sources under ' + $src) }

$exe = Join-Path $outDir 'KingdeeSky.WebApiClient.exe'
$refs = @('/r:System.dll', '/r:System.Core.dll', '/r:System.Configuration.dll',
    '/r:System.Security.dll', '/r:System.Web.Extensions.dll', '/r:System.Xml.dll')
$cscArgs = @('/nologo', '/target:exe', '/optimize+', '/debug-', ('/out:' + $exe)) + $refs + $sources
& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw ('demo client compile failed exit=' + $LASTEXITCODE) }

# Runtime config next to the exe. App.config carries endpoints and key *names*
# only - no secret value ever lives there, so it is safe to copy verbatim.
 $demoConfig = Join-Path $outDir 'KingdeeSky.WebApiClient.exe.config'
Copy-Item (Join-Path $src 'App.config') $demoConfig -Force
# The checked-in configuration is production-safe (HTTPS). The demo is the
# only place where plaintext localhost transport is intentionally enabled.
$demoXml = Get-Content $demoConfig -Raw
$demoXml = $demoXml -replace '<add key="ServerUrl" value="http://127\.0\.0\.1:18080/K3Cloud/" />', '<add key="ServerUrl" value="http://127.0.0.1:18080/K3Cloud/" />'
$demoXml = $demoXml -replace '<add key="AllowInsecureHttp" value="false" />', '<add key="AllowInsecureHttp" value="true" />'
Set-Content -LiteralPath $demoConfig -Value $demoXml -Encoding UTF8
Copy-Item (Join-Path $src 'App.config.example') $outDir -Force
Copy-Item (Join-Path $src 'secrets.env.example') $outDir -Force

Write-Output ('==> Demo client built: ' + $exe)
