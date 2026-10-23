param(
    [string]$Environment = 'dev',
    [string]$Command = 'selfcheck',
    [string]$Root = ''
)
$ErrorActionPreference = 'Stop'

# repo root: derived from this script's location (eng/) -> works for local and CI checkouts
if ([string]::IsNullOrWhiteSpace($Root)) { $Root = Split-Path -Parent $PSScriptRoot }

# run-env: load environment overrides (env/<env>.env, non-sensitive only)
# and run the client executable (selfcheck gate or interactive run).
# All output is ASCII to stay compatible with Windows PowerShell 5.1.

$exe = Join-Path $Root 'src\KingdeeSky.WebApiClient\bin\Release\KingdeeSky.WebApiClient.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    Write-Error ('run-env: executable not found, build first. ' + $exe)
    exit 30
}

$envFile = Join-Path $Root ('env\' + $Environment + '.env')
$loaded = 0
if (Test-Path -LiteralPath $envFile) {
    Get-Content -LiteralPath $envFile -Encoding UTF8 | ForEach-Object {
        $line = $_.Trim()
        if ($line -and -not $line.StartsWith('#')) {
            $eq = $line.IndexOf('=')
            if ($eq -gt 0) {
                $k = $line.Substring(0, $eq).Trim()
                $v = $line.Substring($eq + 1).Trim()
                [Environment]::SetEnvironmentVariable($k, $v, 'Process')
                $loaded++
            }
        }
    }
}
else {
    Write-Output ('run-env: env file not found, using current process variables only. ' + $envFile)
}
[Environment]::SetEnvironmentVariable('KDSKY_ENV', $Environment, 'Process')
Write-Output ('run-env: environment=' + $Environment + ' loaded=' + $loaded + ' command=' + $Command)

if ($Command -eq 'selfcheck') {
    & $exe --selfcheck --json
    exit $LASTEXITCODE
}
if ($Command -eq 'run') {
    & $exe
    exit $LASTEXITCODE
}
Write-Error ('run-env: unknown command: ' + $Command)
exit 31