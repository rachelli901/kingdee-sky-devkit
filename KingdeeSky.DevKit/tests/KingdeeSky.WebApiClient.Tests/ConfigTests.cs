// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using System.IO;
using KingdeeSky.WebApiClient.Security;

namespace KingdeeSky.WebApiClient.Tests
{
    /// <summary>配置分层测试：Settings 读取、占位校验、密钥源与鉴权上下文组装。</summary>
    public static class ConfigTests
    {
        [Test]
        public static void Load_ReadsNonSensitiveDefaults()
        {
            Settings s = Settings.Load();
            Assert.AreEqual("password", s.AuthMode);
            Assert.AreEqual("env", s.SecretStore);
            Assert.AreEqual("KINGDEE_PASSWORD", s.PasswordKey);
            Assert.AreEqual("KINGDEE_KMS_TOKEN", s.KmsTokenKey);
            Assert.AreEqual(2052, s.Lcid);
            Assert.IsNotNull(s.ServerUrl);
            Assert.IsNotNull(s.UserName);
        }

        [Test]
        public static void Validate_ThrowsOnPlaceholders()
        {
            Settings s = Settings.Load();
            Assert.Throws<InvalidOperationException>(() => s.ValidateOrThrow());
        }

        [Test]
        public static void Validate_PassesWithRealValues()
        {
            Settings s = Settings.Load();
            s.ServerUrl = "http://192.168.1.10/K3Cloud/";
            s.AcctId = "ABCDEFG";
            s.UserName = "Administrator";
            s.ValidateOrThrow(); // 不应抛
        }

        [Test]
        public static void CreateSecretProvider_Env()
        {
            Settings s = Settings.Load();
            Assert.InstanceOf<EnvironmentSecretProvider>(s.CreateSecretProvider());
        }

        [Test]
        public static void CreateSecretProvider_CanSwitchByOptions()
        {
            Settings s = Settings.Load();
            s.SecretStore = "file";
            s.SecretsFile = Path.Combine(Path.GetTempPath(), "ksd-fixture", "secrets.env");
            Assert.InstanceOf<FileSecretProvider>(s.CreateSecretProvider());
        }

        [Test]
        public static void CreateAuthContext_MapsFields()
        {
            Settings s = Settings.Load();
            var secrets = new FakeSecretProvider();
            AuthContext ctx = s.CreateAuthContext(secrets);
            Assert.AreEqual(s.AcctId, ctx.AcctId);
            Assert.AreEqual(s.UserName, ctx.UserName);
            Assert.AreEqual(s.Lcid, ctx.Lcid);
            Assert.AreEqual(secrets, ctx.Secrets);
            Assert.AreEqual("KINGDEE_PASSWORD", ctx.PasswordKey);
            Assert.AreEqual("KINGDEE_CLIENT_ID", ctx.ClientIdKey);
            Assert.AreEqual("KINGDEE_APP_ID", ctx.AppIdKey);
        }

        [Test]
        public static void Env_OverridesServerUrl()
        {
            string old = Environment.GetEnvironmentVariable("KDSKY_SERVER_URL");
            try
            {
                Environment.SetEnvironmentVariable("KDSKY_SERVER_URL", "https://env.example/K3Cloud/");
                Settings s = Settings.Load();
                Assert.AreEqual("https://env.example/K3Cloud/", s.ServerUrl);
            }
            finally
            {
                if (old == null) { Environment.SetEnvironmentVariable("KDSKY_SERVER_URL", null); }
                else { Environment.SetEnvironmentVariable("KDSKY_SERVER_URL", old); }
            }
        }

        [Test]
        public static void Env_SetsDeployEnvAndSource()
        {
            string old = Environment.GetEnvironmentVariable("KDSKY_ENV");
            try
            {
                Environment.SetEnvironmentVariable("KDSKY_ENV", "test");
                Settings s = Settings.Load();
                Assert.AreEqual("test", s.DeployEnv);
                Assert.AreEqual("environment", s.DeployEnvSource);
            }
            finally
            {
                if (old == null) { Environment.SetEnvironmentVariable("KDSKY_ENV", null); }
                else { Environment.SetEnvironmentVariable("KDSKY_ENV", old); }
            }
        }

        [Test]
        public static void DeployEnv_DefaultsToDev()
        {
            string old = Environment.GetEnvironmentVariable("KDSKY_ENV");
            try
            {
                Environment.SetEnvironmentVariable("KDSKY_ENV", null);
                Settings s = Settings.Load();
                Assert.AreEqual("dev", s.DeployEnv);
                Assert.AreEqual("default", s.DeployEnvSource);
            }
            finally
            {
                if (old != null) { Environment.SetEnvironmentVariable("KDSKY_ENV", old); }
            }
        }
    }
}