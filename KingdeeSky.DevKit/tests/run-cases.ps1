param(
  [string]$Case = '',
  [string]$Group = '',
  [switch]$List,
  [switch]$All
)
$ErrorActionPreference = 'Stop'
# =============================================================================
# KingdeeSky.DevKit - L1 code-level case runner (62 cases, TC-L1-001..062)
# =============================================================================
# Built-in mapping: TC-L1 id -> group (A..E) -> test method (Type.Method).
# Groups: A=AuthProviderTests(001-016) B=SecretProviderTests(017-031)
#         C=ConfigTests(032-040) D=ClientTests(041-046) E=TransportSecurityTests(047-062)
#
# Usage (PowerShell 5.1):
#   .\run-cases.ps1                     run all 62 cases (same as -All)
#   .\run-cases.ps1 -All                run all 62 cases
#   .\run-cases.ps1 -Case TC-L1-001     run a single case by id
#   .\run-cases.ps1 -Case Password_Name run a single case by short/long method name
#   .\run-cases.ps1 -Group A            run one group (A, B, C, D or E)
#   .\run-cases.ps1 -List               print id/group/method mapping (62 rows), no test run
#   .\run-cases.bat -Case TC-L1-001     same via batch entry
#
# Exit codes: 0 = all selected cases passed, 1 = at least one failed,
#             2 = invalid arguments / unknown case / unknown group
# =============================================================================

$CaseMap = @(
  @{ Id = 'TC-L1-001'; Group = 'A'; Method = 'AuthProviderTests.Password_Name' },
  @{ Id = 'TC-L1-002'; Group = 'A'; Method = 'AuthProviderTests.Password_Authenticate_UsesSecretAndTransport' },
  @{ Id = 'TC-L1-003'; Group = 'A'; Method = 'AuthProviderTests.Password_MissingSecret_Throws' },
  @{ Id = 'TC-L1-004'; Group = 'A'; Method = 'AuthProviderTests.Password_LoginFailed_ReturnsSuccessFalse' },
  @{ Id = 'TC-L1-005'; Group = 'A'; Method = 'AuthProviderTests.OAuth2_TokenFlow_Success' },
  @{ Id = 'TC-L1-006'; Group = 'A'; Method = 'AuthProviderTests.OAuth2_NoTokenEndpoint_Throws' },
  @{ Id = 'TC-L1-007'; Group = 'A'; Method = 'AuthProviderTests.OAuth2_NoAccessToken_ReturnsFailure' },
  @{ Id = 'TC-L1-008'; Group = 'A'; Method = 'AuthProviderTests.OAuth2_ErrorOnly_FallsBackToError' },
  @{ Id = 'TC-L1-009'; Group = 'A'; Method = 'AuthProviderTests.OAuth2_ErrorDescription_Reported' },
  @{ Id = 'TC-L1-010'; Group = 'A'; Method = 'AuthProviderTests.AppSec_NotSupported' },
  @{ Id = 'TC-L1-011'; Group = 'A'; Method = 'AuthProviderTests.AppSec_WithSigner_WritesHeaders' },
  @{ Id = 'TC-L1-012'; Group = 'A'; Method = 'AuthProviderTests.AppSec_MissingSecret_Throws' },
  @{ Id = 'TC-L1-013'; Group = 'A'; Method = 'AuthProviderTests.Factory_MapsModes' },
  @{ Id = 'TC-L1-014'; Group = 'A'; Method = 'AuthProviderTests.Factory_UnknownMode_Throws' },
  @{ Id = 'TC-L1-015'; Group = 'A'; Method = 'AuthProviderTests.Factory_AppSec_NoTemplate_Placeholder' },
  @{ Id = 'TC-L1-016'; Group = 'A'; Method = 'AuthProviderTests.Factory_AppSec_WithTemplate_CreatesSigner' },
  @{ Id = 'TC-L1-017'; Group = 'B'; Method = 'SecretProviderTests.Env_TryGet_HitsProcessVariable' },
  @{ Id = 'TC-L1-018'; Group = 'B'; Method = 'SecretProviderTests.File_ParsesKeyValueLines' },
  @{ Id = 'TC-L1-019'; Group = 'B'; Method = 'SecretProviderTests.File_MissingFile_ReturnsFalse' },
  @{ Id = 'TC-L1-020'; Group = 'B'; Method = 'SecretProviderTests.Dpapi_RoundTrip' },
  @{ Id = 'TC-L1-021'; Group = 'B'; Method = 'SecretProviderTests.Factory_Env_ReturnsEnvironmentProvider' },
  @{ Id = 'TC-L1-022'; Group = 'B'; Method = 'SecretProviderTests.Factory_File_RequiresPath' },
  @{ Id = 'TC-L1-023'; Group = 'B'; Method = 'SecretProviderTests.Factory_Dpapi_RequiresPath' },
  @{ Id = 'TC-L1-024'; Group = 'B'; Method = 'SecretProviderTests.Factory_Kms_RequiresBaseUrl' },
  @{ Id = 'TC-L1-025'; Group = 'B'; Method = 'SecretProviderTests.Factory_Unknown_Throws' },
  @{ Id = 'TC-L1-026'; Group = 'B'; Method = 'SecretProviderTests.Extensions_Get_Present_ReturnsValue' },
  @{ Id = 'TC-L1-027'; Group = 'B'; Method = 'SecretProviderTests.Extensions_Get_Missing_Throws' },
  @{ Id = 'TC-L1-028'; Group = 'B'; Method = 'SecretProviderTests.Redactor_ReplacesAndIgnoresShortValues' },
  @{ Id = 'TC-L1-029'; Group = 'B'; Method = 'SecretProviderTests.Redactor_MaskShort' },
  @{ Id = 'TC-L1-030'; Group = 'B'; Method = 'SecretProviderTests.Logger_Scrub_RegisteredSecret' },
  @{ Id = 'TC-L1-031'; Group = 'B'; Method = 'SecretProviderTests.Logger_FileOutput_Scrubbed' },
  @{ Id = 'TC-L1-032'; Group = 'C'; Method = 'ConfigTests.Load_ReadsNonSensitiveDefaults' },
  @{ Id = 'TC-L1-033'; Group = 'C'; Method = 'ConfigTests.Env_OverridesServerUrl' },
  @{ Id = 'TC-L1-034'; Group = 'C'; Method = 'ConfigTests.Env_SetsDeployEnvAndSource' },
  @{ Id = 'TC-L1-035'; Group = 'C'; Method = 'ConfigTests.DeployEnv_DefaultsToDev' },
  @{ Id = 'TC-L1-036'; Group = 'C'; Method = 'ConfigTests.CreateAuthContext_MapsFields' },
  @{ Id = 'TC-L1-037'; Group = 'C'; Method = 'ConfigTests.CreateSecretProvider_Env' },
  @{ Id = 'TC-L1-038'; Group = 'C'; Method = 'ConfigTests.CreateSecretProvider_CanSwitchByOptions' },
  @{ Id = 'TC-L1-039'; Group = 'C'; Method = 'ConfigTests.Validate_PassesWithRealValues' },
  @{ Id = 'TC-L1-040'; Group = 'C'; Method = 'ConfigTests.Validate_ThrowsOnPlaceholders' },
  @{ Id = 'TC-L1-041'; Group = 'D'; Method = 'ClientTests.Ctor_EmptyServerUrl_Throws' },
  @{ Id = 'TC-L1-042'; Group = 'D'; Method = 'ClientTests.Ctor_EmptyAcctId_Throws' },
  @{ Id = 'TC-L1-043'; Group = 'D'; Method = 'ClientTests.Ctor_NullAuthProvider_Throws' },
  @{ Id = 'TC-L1-044'; Group = 'D'; Method = 'ClientTests.Ctor_NullAuthContext_Throws' },
  @{ Id = 'TC-L1-045'; Group = 'D'; Method = 'ClientTests.Login_DelegatesToAuthProvider' },
  @{ Id = 'TC-L1-046'; Group = 'D'; Method = 'ClientTests.Login_PropagatesAuthResult' },
  @{ Id = 'TC-L1-047'; Group = 'E'; Method = 'TransportSecurityTests.Ctor_Https_Ok' },
  @{ Id = 'TC-L1-048'; Group = 'E'; Method = 'TransportSecurityTests.Ctor_Http_Default_Throws' },
  @{ Id = 'TC-L1-049'; Group = 'E'; Method = 'TransportSecurityTests.Ctor_Http_AllowInsecure_Ok' },
  @{ Id = 'TC-L1-050'; Group = 'E'; Method = 'TransportSecurityTests.EnsureHttps_Passes' },
  @{ Id = 'TC-L1-051'; Group = 'E'; Method = 'TransportSecurityTests.EnsureHttp_Allowed_Ok' },
  @{ Id = 'TC-L1-052'; Group = 'E'; Method = 'TransportSecurityTests.EnsureHttp_NotAllowed_Throws' },
  @{ Id = 'TC-L1-053'; Group = 'E'; Method = 'TransportSecurityTests.EnsureNonHttp_Throws' },
  @{ Id = 'TC-L1-054'; Group = 'E'; Method = 'TransportSecurityTests.EnsureEmptyOrInvalid_Throws' },
  @{ Id = 'TC-L1-055'; Group = 'E'; Method = 'TransportSecurityTests.ParseFlag_RecognizesBooleanToken' },
  @{ Id = 'TC-L1-056'; Group = 'E'; Method = 'TransportSecurityTests.ParseFlag_UnknownValue_FallsBackToDefault' },
  @{ Id = 'TC-L1-057'; Group = 'E'; Method = 'TransportSecurityTests.SetRequestHeader_StoresExtraHeader' },
  @{ Id = 'TC-L1-058'; Group = 'E'; Method = 'TransportSecurityTests.SetRequestHeader_EmptyName_Throws' },
  @{ Id = 'TC-L1-059'; Group = 'E'; Method = 'TransportSecurityTests.HmacSha256AppSecSigner_Deterministic_EmptyBody' },
  @{ Id = 'TC-L1-060'; Group = 'E'; Method = 'TransportSecurityTests.HmacSha256AppSecSigner_Deterministic_HelloBody' },
  @{ Id = 'TC-L1-061'; Group = 'E'; Method = 'TransportSecurityTests.HmacSha256AppSecSigner_EmptyTemplate_Throws' },
  @{ Id = 'TC-L1-062'; Group = 'E'; Method = 'TransportSecurityTests.HmacSha256AppSecSigner_NullContext_Throws' }
)

# -- list mode: just print the mapping -------------------------------------------------
if ($List) {
  Write-Output ('id | group | method (total ' + $CaseMap.Count + ')')
  Write-Output '--------------------------------------------'
  foreach ($c in $CaseMap) { Write-Output ($c.Id + ' | ' + $c.Group + ' | ' + $c.Method) }
  exit 0
}

# -- argument validation ----------------------------------------------------------------
$modeCount = 0
if ($Case -ne '') { $modeCount++ }
if ($Group -ne '') { $modeCount++ }
if ($All) { $modeCount++ }
if ($modeCount -gt 1) {
  [Console]::Error.WriteLine('Invalid arguments: -Case, -Group and -All are mutually exclusive.')
  exit 2
}
if ($modeCount -eq 0) { $All = $true }

$selected = @()
if ($All) {
  $selected = $CaseMap
} elseif ($Group -ne '') {
  $g = $Group.Trim().ToUpperInvariant()
  if ($g -notmatch '^[A-E]$') {
    [Console]::Error.WriteLine('Unknown group: ' + $Group + ' (expected A, B, C, D or E)')
    exit 2
  }
  $selected = @($CaseMap | Where-Object { $_.Group -eq $g })
} elseif ($Case -ne '') {
  $key = $Case.Trim()
  $hit = $null
  foreach ($c in $CaseMap) {
    if ($c.Id -eq $key) { $hit = $c; break }
    if ($c.Method -eq $key) { $hit = $c; break }
    $dot = $c.Method.LastIndexOf('.')
    if ($dot -gt 0 -and $c.Method.Substring($dot + 1) -eq $key) { $hit = $c; break }
  }
  if ($null -eq $hit) {
    [Console]::Error.WriteLine('Unknown case: ' + $key + ' (expected TC-L1-001..TC-L1-062 or a method name; use -List to see all)')
    exit 2
  }
  $selected = @($hit)
}

# -- compile once (same contract as run-tests.ps1) ---------------------------------------
# repo root: derived from this script's location (tests/) -> works for local and CI checkouts
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'src\KingdeeSky.WebApiClient'
$testsDir = Join-Path $root 'tests\KingdeeSky.WebApiClient.Tests'
$outDir = Join-Path $testsDir 'bin\Debug'
$exe = Join-Path $outDir 'KingdeeSky.WebApiClient.Tests.exe'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { [Console]::Error.WriteLine('csc.exe (NET Framework 4.8) not found'); exit 3 }

$refs = @(
  '/r:System.dll',
  '/r:System.Core.dll',
  '/r:System.Configuration.dll',
  '/r:System.Security.dll',
  '/r:System.Web.Extensions.dll',
  '/r:System.Xml.dll'
)
$testFiles = @(
  'TestProgram.cs','TestFramework.cs','Helpers.cs',
  'AuthProviderTests.cs','SecretProviderTests.cs','ConfigTests.cs','ClientTests.cs','TransportSecurityTests.cs'
)
$srcFiles = @(
  'KingdeeWebApiClient.cs','Scenarios.cs','Logging\SafeLogger.cs',
  'Security\AppIdAppSecAuthProvider.cs','Security\AuthProviderFactory.cs','Security\DpapiSecretProvider.cs',
  'Security\EnvironmentSecretProvider.cs','Security\FileSecretProvider.cs','Security\HmacSha256AppSecSigner.cs',
  'Security\IAuthProvider.cs','Security\IAppSecSigner.cs','Security\ISecretProvider.cs',
  'Security\KmsSecretProvider.cs','Security\OAuth2AuthProvider.cs','Security\PasswordAuthProvider.cs',
  'Security\SecretProviderFactory.cs','Security\SecretRedactor.cs','Security\TransportSecurityPolicy.cs'
)

Write-Output '==> Compiling tests (csc, .NET Framework 4.8, zero external deps)'
$cscArgs = @('/nologo','/target:exe',('/out:' + $exe)) + $refs
foreach ($f in $testFiles) { $cscArgs += (Join-Path $testsDir $f) }
foreach ($f in $srcFiles) { $cscArgs += (Join-Path $src $f) }
& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { [Console]::Error.WriteLine('COMPILE FAILED exit=' + $LASTEXITCODE); exit $LASTEXITCODE }
Write-Output ('==> Compiled: ' + $exe)
Copy-Item (Join-Path $src 'App.config') (Join-Path $outDir 'KingdeeSky.WebApiClient.Tests.exe.config') -Force

# -- run each selected case through TestProgram.exe --method --------------------------------
Write-Output ('==> Running ' + $selected.Count + ' case(s)')
$passed = 0
$failed = 0
$failLines = @()
foreach ($c in $selected) {
  $out = & $exe --method $c.Method 2>&1
  $rc = $LASTEXITCODE
  $passLine = $out | Select-String -SimpleMatch '[PASS]'
  $failLine = $out | Select-String -SimpleMatch '[FAIL]'
  if ($rc -eq 0 -and $null -ne $passLine) {
    $passed++
    Write-Output ('  [PASS] ' + $c.Id + ' ' + $c.Method)
  } else {
    $failed++
    $failLines += ($c.Id + ' ' + $c.Method)
    Write-Output ('  [FAIL] ' + $c.Id + ' ' + $c.Method)
    if ($null -ne $failLine) { Write-Output ('         ' + $failLine.ToString().Trim()) }
    else { Write-Output ('         exit=' + $rc) }
  }
}
Write-Output ''
Write-Output ('===== RUN-CASES SUMMARY: total=' + $selected.Count + ' passed=' + $passed + ' failed=' + $failed + ' =====')
foreach ($f in $failLines) { Write-Output ('FAILED: ' + $f) }
if ($failed -gt 0) {
  Write-Output 'RUN-CASES FAILED (gate blocked)'
  exit 1
}
Write-Output 'ALL RUN-CASES PASSED'
exit 0