# Author: rachelli901 <lir30481@gmail.com>
# Repository: https://github.com/rachelli901/kingdee-sky-devkit

param(
    [string]$Root = '',
    [switch]$IncludeTests
)
$ErrorActionPreference = 'Stop'

# Static secret scanner.
# Scope: by default the whole repository root and only files under version
# control (git ls-files). The DevKit tree no longer defines the boundary, so
# sibling projects - the runnable demo client, the browser console and the
# deploy deliverables - are scanned too: they ship in the same repository and
# could carry a plaintext credential just as easily.
# Exits 1 when any hit is found (usable as a CI/build gate).
#
# Detection is deliberately value-agnostic: no allow-list entry names a real
# credential string. Only values that cannot be a credential at all (key names,
# placeholder delimiters, template markers) are skipped, so a genuine secret can
# never pass merely because it equals a "known demo" string.

$excludeDirs = @('.git', 'bin', 'obj', 'artifacts', 'packages', '.vs', '.idea', 'node_modules', 'vendor',
    '.dumate', '.tmp', '.workbuddy', '.ruff_cache', '_shots', '.design-suite')
$excludeExts = @('.example', '.zip', '.dll', '.exe', '.pdb', '.log', '.dpapi', '.png', '.jpg', '.jpeg', '.gif', '.svg', '.ico')

# Machine-generated single-file bundles stay in scope, but such a bundle inlines
# minified third-party libraries onto extremely long lines, so pattern matching
# is limited to its line-structured first-party portion. Each bundle's
# first-party source is tracked separately and is scanned in full.
$generatedBundles = @('deploy/KingdeeSky.WebApiClient.html')
$bundleMaxLineLength = 200

# Extensions whose native syntax is a bare KEY=value line (safe to read the
# unquoted value as a literal). Code files are excluded from that path: in code
# `token = expr` is an ordinary assignment, not a credential.
$bareValueExts = @('.env', '.ini', '.cfg', '.conf', '.properties', '.yml', '.yaml', '.txt', '.md',
    '.ps1', '.sh', '.bat', '.cmd', '.toml', '.tf', '.tfvars', '.json', '.xml', '.config')

function Resolve-RepoRoot([string]$start) {
    $probe = $start
    if ([string]::IsNullOrWhiteSpace($probe)) { $probe = (Get-Location).Path }
    try {
        $top = & git -C $probe rev-parse --show-toplevel 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($top)) {
            return (Resolve-Path -LiteralPath $top).Path
        }
    } catch { }
    return (Resolve-Path -LiteralPath $probe).Path
}

# An explicit -Root is honoured verbatim: the caller decides the scope. Only when
# it is omitted do we fall back to the repository that contains this script.
# Deriving the root unconditionally would silently rewrite an explicit scope and
# make the gate scan a different file set depending on where it was invoked from
# (`git ls-files` is cwd-relative, so a subdirectory probe yields only that
# subtree) - a scope that varies with the call site is a gate that can be
# bypassed by choosing a different working directory.
if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Resolve-RepoRoot $PSScriptRoot
} else {
    $Root = (Resolve-Path -LiteralPath $Root).Path
}
$selfPath = $MyInvocation.MyCommand.Path

# ---- collect candidate files: tracked files first, filesystem walk as fallback ----
# git writes paths as raw UTF-8 bytes. PowerShell decodes a native command's
# output with [Console]::OutputEncoding, which on a Chinese Windows console is
# the ANSI code page (GBK/936) unless the caller happened to run `chcp 65001`.
# Under the wrong code page every non-ASCII path comes back mangled (U+FFFD),
# Test-Path then fails, and the file is silently dropped from the scan - which
# means a credential stored in a Chinese-named file would never be inspected.
# Pin the encoding here so the gate does not depend on the caller's console.
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
$tracked = $null
try {
    $lsOut = & git -C $Root ls-files -z 2>$null
    if ($LASTEXITCODE -eq 0 -and $null -ne $lsOut) {
        $tracked = @(($lsOut -join '') -split "`0" | Where-Object { $_ -ne '' })
    }
} catch { $tracked = $null }

if ($null -ne $tracked) {
    $candidates = @()
    $unresolved = 0
    foreach ($rel in $tracked) {
        $full = Join-Path $Root ($rel -replace '/', '\')
        if (Test-Path -LiteralPath $full -PathType Leaf) {
            $candidates += $full
        } elseif ($rel -match '[^\x00-\x7F]') {
            # A non-ASCII path that cannot be resolved is almost always an
            # encoding problem, not a missing file. Counting it keeps the gate
            # honest: a silently dropped Chinese-named file is an unscanned file.
            $unresolved++
        }
    }
    if ($unresolved -gt 0) {
        [Console]::Error.WriteLine(
            ('WARNING: ' + $unresolved + ' non-ASCII path(s) from git could not be resolved and were NOT scanned. ' +
             'The console encoding is likely not UTF-8 - run "chcp 65001" before this script, ' +
             'or a credential in a non-ASCII-named file would go unnoticed.'))
    }
} else {
    $candidates = @(Get-ChildItem -LiteralPath $Root -Recurse -File | ForEach-Object { $_.FullName })
}

$files = $candidates | Where-Object {
    $full = $_
    # The scanner's own rule definitions necessarily contain the patterns.
    if ($selfPath -and ($full -eq $selfPath)) { return $false }
    $rel = $full.Substring($Root.Length).TrimStart('\', '/')
    if (-not $rel) { return $false }
    $parts = $rel -split '[\\/]'
    foreach ($d in $excludeDirs) { if ($parts -contains $d) { return $false } }
    # tests/ fixtures are non-production values: excluded unless -IncludeTests
    if (-not $IncludeTests -and ($parts -contains 'tests')) { return $false }
    $ext = ([System.IO.Path]::GetExtension($full)).ToLowerInvariant()
    if ($excludeExts -contains $ext) { return $false }
    return $true
}

$patterns = @(
    @{ Name = 'private-key-block';   Regex = '-----BEGIN [A-Z ]*PRIVATE KEY-----' },
    @{ Name = 'aws-access-key-id';   Regex = 'AKIA[0-9A-Z]{16}' },
    @{ Name = 'github-token';        Regex = 'gh[pousr]_[A-Za-z0-9]{20,}' },
    @{ Name = 'openai-style-sk';     Regex = 'sk-[A-Za-z0-9]{20,}' }
)

# Sensitive-name stem: matches Password, PASSWORD, MOCK_PASSWORD, KINGDEE_PASSWORD,
# ClientSecret, appSecret, api_key, accessKey ... but never a mid-word fragment.
$keyPat = '(?i)(?:^|[^A-Za-z0-9_])(?:[A-Za-z0-9_]*_)?(password|passwd|pwd|secret|token|apikey|api_key|accesskey|access_key|privatekey|private_key|clientid|client_id|clientsecret|client_secret)\b'

# Rule A - assignment to a quoted string literal. Applies to every file type and
# is the only rule used for code, where an unquoted right-hand side is an
# expression (`token = !0`) rather than a credential.
$kvQuoted = $keyPat + '[\t ]*[:=][\t ]*(["''])([^"''\r\n]+)\2'

# Rule B - bare KEY=value, used only for config/data/script file types, and only
# when the whole line is the assignment (keeps `password: x` in YAML while
# ignoring prose and code).
$kvBare = $keyPat + '[\t ]*[:=][\t ]*([^\s"''#;,]{4,})[\t ]*$'

# Rule C - XML appSettings style `<add key="Password" value="..."/>`: the secret
# lives in a value attribute, so the key=value rules above never see it. This is
# the shape a plaintext credential usually takes in App.config, hence dedicated.
# The key look-ahead names keys that actually hold a credential. A bare `secret`
# is deliberately NOT matched: keys such as SecretStore / SecretsFile only select
# *where* a secret is read from, they do not carry one.
$xmlAdd = '<add\s+key\s*=\s*"(?=[^"]*(?i)(password|passwd|pwd|client[-_]?id|client[-_]?secret|app[-_]?secret|secret[-_]?key|api[-_]?key|apikey|access[-_]?key|private[-_]?key|token))[^"]*"\s+value\s*=\s*"([^"]*)"'

# Values that are clearly not credentials: key names, placeholder delimiters,
# template markers. Keep this list limited to strings that cannot be a real
# credential - never add an actual credential value here.
# NOTE: the SNAKE_CASE test must be case-SENSITIVE. PowerShell's -match is
# case-insensitive by default, which would let any lowercase alphanumeric
# credential (e.g. a password like `demo123`) sail through as a "key name".
function Test-AllowedValue([string]$value) {
    $t = ($value.Trim('"', "'").Trim())
    if ([string]::IsNullOrEmpty($t)) { return $true }
    if ($t -cmatch '^[A-Z][A-Z0-9_]*$') { return $true }    # ALL_CAPS name / config key
    if ($t -match '[{}<>]') { return $true }                # placeholder delimiters
    if ($t -match '[()]') { return $true }                  # method call / expression, not a literal
    # A pure variable reference forwards an existing value; it is not itself a
    # plaintext credential ($VAR, ${VAR}, $(cmd), $env:VAR, %VAR%).
    if ($t -match '^\$(env:)?[A-Za-z_][A-Za-z0-9_]*$') { return $true }   # $VAR / $env:VAR
    if ($t -match '^\$\{[A-Za-z_][A-Za-z0-9_]*\}$') { return $true }      # ${VAR}
    if ($t -match '^\$\(.*\)$') { return $true }                          # $(command)
    if ($t -match '^%[A-Za-z_][A-Za-z0-9_]*%$') { return $true }          # %VAR% (cmd)
    if ($t -match '(?i)(CHANGE_ME|PLACEHOLDER|YOUR_|example)') { return $true }
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
    $lines = @(Get-Content -LiteralPath $f -Encoding UTF8)
    if ($lines.Count -eq 0) { continue }
    $rel = $f.Substring($Root.Length).TrimStart('\', '/')
    $ext = ([System.IO.Path]::GetExtension($f)).ToLowerInvariant()
    $lineLimit = 0
    if ($generatedBundles -contains ($rel -replace '\\', '/')) { $lineLimit = $bundleMaxLineLength }

    $text = [string]::Join("`n", $lines)
    foreach ($p in $patterns) {
        $m = [regex]::Match($text, $p.Regex)
        if ($m.Success) {
            Add-Hit $rel 0 $p.Name $m.Value
        }
    }

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($lineLimit -gt 0 -and $line.Length -gt $lineLimit) { continue }

        foreach ($m in [regex]::Matches($line, $kvQuoted)) {
            Add-Hit $rel ($i + 1) 'key-value' $m.Groups[$m.Groups.Count - 1].Value
        }

        if ($bareValueExts -contains $ext) {
            foreach ($m in [regex]::Matches($line, $kvBare)) {
                Add-Hit $rel ($i + 1) 'key-value-bare' $m.Groups[$m.Groups.Count - 1].Value
            }
        }

        foreach ($m in [regex]::Matches($line, $xmlAdd)) {
            Add-Hit $rel ($i + 1) 'xml-appsetting' $m.Groups[2].Value
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