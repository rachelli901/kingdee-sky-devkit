using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using KingdeeSky.WebApiClient.Security;

namespace KingdeeSky.WebApiClient.Tests
{
    /// <summary>密钥来源测试：环境变量 / 文件 / DPAPI / 工厂 / 脱敏与日志。</summary>
    public static class SecretProviderTests
    {
        private static string TempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "sky-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Test]
        public static void Env_TryGet_HitsProcessVariable()
        {
            string key = "SKY_TEST_ENV_" + Guid.NewGuid().ToString("N");
            try
            {
                Environment.SetEnvironmentVariable(key, "hello");
                var p = new Security.EnvironmentSecretProvider();
                string v;
                Assert.IsTrue(p.TryGet(key, out v));
                Assert.AreEqual("hello", v);
                Assert.IsFalse(p.TryGet(key + "_MISSING", out v));
                Assert.AreEqual("environment", p.Name);
            }
            finally
            {
                Environment.SetEnvironmentVariable(key, null);
            }
        }

        [Test]
        public static void File_ParsesKeyValueLines()
        {
            string dir = TempDir();
            string file = Path.Combine(dir, "secrets.env");
            try
            {
                File.WriteAllText(file, "# comment line\n\nKINGDEE_PASSWORD=demo123\nKINGDEE_CLIENT_ID= cid \n", Encoding.UTF8);
                var p = new Security.FileSecretProvider(file);
                string v;
                Assert.IsTrue(p.TryGet("KINGDEE_PASSWORD", out v));
                Assert.AreEqual("demo123", v);
                Assert.IsTrue(p.TryGet("KINGDEE_CLIENT_ID", out v));
                Assert.AreEqual("cid", v);
                Assert.IsFalse(p.TryGet("MISSING_KEY", out v));
                Assert.Contains(p.Name, "secrets.env");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public static void File_MissingFile_ReturnsFalse()
        {
            // path is intentionally non-existent; derive it so no machine-specific absolute path is hardcoded
            var p = new Security.FileSecretProvider(Path.Combine(Path.GetTempPath(), "ksd-missing-" + Guid.NewGuid().ToString("N"), "secrets.env"));
            string v;
            Assert.IsFalse(p.TryGet("KINGDEE_PASSWORD", out v));
        }

        [Test]
        public static void Dpapi_RoundTrip()
        {
            string dir = TempDir();
            string plain = Path.Combine(dir, "plain.txt");
            string enc = Path.Combine(dir, "enc.bin");
            try
            {
                File.WriteAllText(plain, "KINGDEE_PASSWORD=demo123\n", Encoding.UTF8);
                Security.DpapiSecretStore.Protect(plain, enc, DataProtectionScope.CurrentUser);
                var p = new Security.DpapiSecretProvider(enc, DataProtectionScope.CurrentUser);
                string v;
                Assert.IsTrue(p.TryGet("KINGDEE_PASSWORD", out v));
                Assert.AreEqual("demo123", v);
                Assert.Contains(p.Name, "dpapi");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public static void Factory_Env_ReturnsEnvironmentProvider()
        {
            Assert.InstanceOf<Security.EnvironmentSecretProvider>(
                Security.SecretProviderFactory.Create(new Security.SecretStoreOptions { Store = "env" }));
            Assert.InstanceOf<Security.EnvironmentSecretProvider>(
                Security.SecretProviderFactory.Create(null));
        }

        [Test]
        public static void Factory_File_RequiresPath()
        {
            Assert.Throws<InvalidOperationException>(
                () => Security.SecretProviderFactory.Create(new Security.SecretStoreOptions { Store = "file" }));
            Assert.InstanceOf<Security.FileSecretProvider>(
                Security.SecretProviderFactory.Create(new Security.SecretStoreOptions { Store = "file", SecretsFile = Path.Combine(Path.GetTempPath(), "ksd-fixture", "s.env") }));
        }

        [Test]
        public static void Factory_Dpapi_RequiresPath()
        {
            Assert.Throws<InvalidOperationException>(
                () => Security.SecretProviderFactory.Create(new Security.SecretStoreOptions { Store = "dpapi" }));
        }

        [Test]
        public static void Factory_Kms_RequiresBaseUrl()
        {
            Assert.Throws<ArgumentException>(
                () => Security.SecretProviderFactory.Create(new Security.SecretStoreOptions { Store = "kms" }));
        }

        [Test]
        public static void Factory_Unknown_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => Security.SecretProviderFactory.Create(new Security.SecretStoreOptions { Store = "bogus" }));
        }

        [Test]
        public static void Extensions_Get_Missing_Throws()
        {
            var p = new FakeSecretProvider();
            var ex = Assert.Throws<Security.SecretNotFoundException>(() => p.Get("KINGDEE_PASSWORD"));
            Assert.Contains(ex.Message, "KINGDEE_PASSWORD");
        }

        [Test]
        public static void Extensions_Get_Present_ReturnsValue()
        {
            var p = new FakeSecretProvider().Add("KINGDEE_PASSWORD", "abc");
            Assert.AreEqual("abc", p.Get("KINGDEE_PASSWORD"));
        }

        [Test]
        public static void Redactor_ReplacesAndIgnoresShortValues()
        {
            Assert.AreEqual("a " + Security.SecretRedactor.Mask + " b",
                Security.SecretRedactor.Redact("a secret-value b", "secret-value"));
            Assert.AreEqual("abc", Security.SecretRedactor.Redact("abc", "ab"));
            Assert.AreEqual(null, Security.SecretRedactor.Redact(null, "x"));
            Assert.AreEqual("", Security.SecretRedactor.Redact("", "x"));
        }

        [Test]
        public static void Redactor_MaskShort()
        {
            Assert.AreEqual("****", Security.SecretRedactor.MaskShort("abcd"));
            Assert.AreEqual("12****yz", Security.SecretRedactor.MaskShort("123456yz"));
            Assert.AreEqual(null, Security.SecretRedactor.MaskShort(null));
        }

        [Test]
        public static void Logger_Scrub_RegisteredSecret()
        {
            var logger = new Logging.SafeLogger(Logging.LogLevel.Info, null);
            logger.RegisterSecret("topsecret");
            logger.RegisterSecret("ab"); // 长度 < 3 忽略
            Assert.AreEqual("value=" + Security.SecretRedactor.Mask, logger.Scrub("value=topsecret"));
            Assert.AreEqual("plain", logger.Scrub("plain"));
        }

        [Test]
        public static void Logger_FileOutput_Scrubbed()
        {
            string dir = TempDir();
            string file = Path.Combine(dir, "app.log");
            try
            {
                var logger = new Logging.SafeLogger(Logging.LogLevel.Info, file);
                logger.RegisterSecret("hidden-value");
                logger.Info("login with hidden-value");
                string text = File.ReadAllText(file, Encoding.UTF8);
                Assert.Contains(text, "REDACTED");
                Assert.NotContains(text, "hidden-value");
                Assert.Contains(text, "[INFO]");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}