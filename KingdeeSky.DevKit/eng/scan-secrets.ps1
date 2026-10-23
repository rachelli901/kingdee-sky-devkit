param(
    [string]$Root = '',
    [switch]$IncludeTests
)
$ErrorActionPreference = 'Stop'

# Static secret scanner for KingdeeSky.DevKit.
# Detects high-signal secret patterns and sensitive key=value assignments.
# Default scan excludes tests/ (fixture values are not production secrets);
# pass -IncludeTests to scan them explicitly.
# Exits 1 when any hit is found (usable as a CI/build gate).

$excludeDirs = @('.git', 'bin', 'obj', 'artifacts', 'packages', '.vs', '.idea', 'node_modules')
$excludeExts = @('.example', '.zip', '.dll', '.exe', '.pdb', '.log', '.dpapi', '.png', '.jpg', '.jpeg', '.gif', '.svg', '.ico')

if ([string]::IsNullOrWhiteSpace($Root)) { $Root = (Get-Location).Path }
$Root = (Resolve-Path -LiteralPath $Root).Path

$files = Get-ChildItem -LiteralPath $Root -Recurse -File | Where-Object {
    $rel = $_.FullName.Substring($Root.Length).TrimStart('\', '/')
    if (-not $rel) { return $false }
    $parts = $rel -split '[\\/]'
    foreach ($d in $excludeDirs) { if ($parts -contains $d) { return $false } }
    if (-not $IncludeTests -and $parts[0] -eq 'tests') { return $false }
    $ext = $_.Extension.ToLowerInvariant()
    if ($excludeExts -contains $ext) { return $false }
    return $true
}

$patterns = @(
    @{ Name = 'private-key-block';   Regex = '-----BEGIN [A-Z ]*PRIVATE KEY-----' },
    @{ Name = 'aws-access-key-id';   Regex = 'AKIA[0-9A-Z]{16}' },
    @{ Name = 'github-token';        Regex = 'gh[pousr]_[A-Za-z0-9]{20,}' },
    @{ Name = 'openai-style-sk';     Regex = 'sk-[A-Za-z0-9]{20,}' }
)

# Key=value style: only meaningful when the key resembles a secret.
# For .cs files, require the value to be a quoted string literal to avoid
# false positives from assignments that read from a secret source.
$kvAny = '(?i)\b(password|passwd|pwd|secret|token|apikey|api_key|access_key|private_key|client_id|client_secret)\b[\s]*[:=][\s]*["'']?([^"' + "'" + '\s,;)]+)'
$kvCs = '(?i)\b(password|passwd|pwd|secret|token|apikey|api_key|access_key|private_key|client_id|client_secret)\b[\s]*=[\s]*"([^"]+)"'

# Values that are clearly placeholders / examples / key names, not real secrets.
function Test-AllowedValue([string]$value) {
    $t = ($value.Trim('"', "'"))
    if ($t -match '^[A-Z0-9_]+$') { return $true }        # SNAKE_CASE name / config key
    if ($t -match '[{}]') { return $true }                # placeholder braces
    if ($t -match '[()]') { return $true }                # method call / expression, not a literal
    if ($t -match '(?i)(CHANGE_ME|PLACEHOLDER|YOUR_|example|test|demo123)') { return $true }
    return $false
}

$hits = New-Object System.Collections.Generic.List[string]

function Add-Hit([string]$file, [int]$line, [string]$rule, [string]$value) {
    if (Test-AllowedValue $value) { return }
    $hits.Add(($file + ':' + $line + ':' + $rule + ':' + $value))
}

$count = 0
foreach ($f in $files) {
    $count++
    $lines = @(Get-Content -LiteralPath $f.FullName -Encoding UTF8)
    if ($lines.Count -eq 0) { continue }
    $rel = $f.FullName.Substring($Root.Length).TrimStart('\', '/')
    $isCs = ($f.Extension -eq '.cs')

    $text = [string]::Join("`n", $lines)
    foreach ($p in $patterns) {
        $m = [regex]::Match($text, $p.Regex)
        if ($m.Success) {
            Add-Hit $rel 0 $p.Name $m.Value
        }
    }

    $re = $kvAny
    if ($isCs) { $re = $kvCs }
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $ms = [regex]::Matches($line, $re)
        foreach ($m in $ms) {
            $val = $m.Groups[$m.Groups.Count - 1].Value
            Add-Hit $rel ($i + 1) 'key-value' $val
        }
    }
}

if ($hits.Count -gt 0) {
    Write-Output ('Secret scan FAILED: scanned ' + $count + ' files, ' + $hits.Count + ' hit(s).')
    foreach ($h in $hits) { Write-Output ('  ' + $h) }
    exit 1
}

Write-Output ('Secret scan OK: scanned ' + $count + ' files, no plaintext secrets found.')
exit 0