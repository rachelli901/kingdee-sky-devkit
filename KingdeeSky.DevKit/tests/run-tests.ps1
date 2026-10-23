$ErrorActionPreference = 'Stop'
# KingdeeSky.WebApiClient 测试门禁：编译（csc, .NET Framework 4.8）-> 复制 App.config -> 运行，失败即返回非 0
# repo root: derived from this script's location (tests/) -> works for local and CI checkouts
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'src\KingdeeSky.WebApiClient'
$tests = Join-Path $root 'tests\KingdeeSky.WebApiClient.Tests'
$outDir = Join-Path $tests 'bin\Debug'
$exe = Join-Path $outDir 'KingdeeSky.WebApiClient.Tests.exe'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'csc.exe (NET Framework 4.8) not found' }

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
foreach ($f in $testFiles) { $cscArgs += (Join-Path $tests $f) }
foreach ($f in $srcFiles) { $cscArgs += (Join-Path $src $f) }
& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { Write-Error ('COMPILE FAILED exit=' + $LASTEXITCODE); exit $LASTEXITCODE }
Write-Output ('==> Compiled: ' + $exe)

# Settings.Load 依赖 app.config：复制 src 的 App.config 为测试 exe.config
Copy-Item (Join-Path $src 'App.config') (Join-Path $outDir 'KingdeeSky.WebApiClient.Tests.exe.config') -Force

Write-Output '==> Running tests'
& $exe
exit $LASTEXITCODE