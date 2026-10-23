// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using KingdeeSky.WebApiClient.Logging;
using KingdeeSky.WebApiClient.Security;

namespace KingdeeSky.WebApiClient.Tests
{
    /// <summary>传输安全策略与 AppSec 签名器测试：默认强制 HTTPS、显式放行才允许明文并告警、HMAC-SHA256 确定性。</summary>
    public static class TransportSecurityTests
    {
        private static AuthContext Ctx()
        {
            return new AuthContext { AcctId = "A1", UserName = "u", Secrets = new FakeSecretProvider() };
        }

        [Test]
        public static void ParseFlag_RecognizesBooleanToken()
        {
            Assert.IsTrue(TransportSecurityPolicy.ParseFlag("true", false));
            Assert.IsTrue(TransportSecurityPolicy.ParseFlag("1", false));
            Assert.IsTrue(TransportSecurityPolicy.ParseFlag("yes", false));
            Assert.IsTrue(TransportSecurityPolicy.ParseFlag("on", false));
            Assert.IsTrue(TransportSecurityPolicy.ParseFlag("TRUE", false));
            Assert.IsFalse(TransportSecurityPolicy.ParseFlag("false", true));
            Assert.IsFalse(TransportSecurityPolicy.ParseFlag("0", true));
            Assert.IsFalse(TransportSecurityPolicy.ParseFlag("no", true));
            Assert.IsFalse(TransportSecurityPolicy.ParseFlag("off", true));
        }

        [Test]
        public static void ParseFlag_UnknownValue_FallsBackToDefault()
        {
            Assert.IsFalse(TransportSecurityPolicy.ParseFlag("", false));
            Assert.IsFalse(TransportSecurityPolicy.ParseFlag(null, false));
            Assert.IsFalse(TransportSecurityPolicy.ParseFlag("bogus", false));
            Assert.IsTrue(TransportSecurityPolicy.ParseFlag("bogus", true));
        }

        [Test]
        public static void EnsureHttps_Passes()
        {
            string url = "https://localhost/K3Cloud/";
            Assert.AreEqual(url, TransportSecurityPolicy.EnsureUrlAllowed(url, false, null, "ServerUrl"));
        }

        [Test]
        public static void EnsureHttp_NotAllowed_Throws()
        {
            Assert.Throws<InvalidOperationException>(
                () => TransportSecurityPolicy.EnsureUrlAllowed("http://localhost/K3Cloud/", false, null, "ServerUrl"));
        }

        [Test]
        public static void EnsureHttp_Allowed_Ok()
        {
            // 显式放行后返回原 URL，不抛异常（放行告警由 logger 输出，此处传 null 仅验证放行路径）
            string url = "http://localhost/K3Cloud/";
            Assert.AreEqual(url, TransportSecurityPolicy.EnsureUrlAllowed(url, true, null, "ServerUrl"));
        }

        [Test]
        public static void EnsureEmptyOrInvalid_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => TransportSecurityPolicy.EnsureUrlAllowed("", false, null, "ServerUrl"));
            Assert.Throws<ArgumentException>(
                () => TransportSecurityPolicy.EnsureUrlAllowed("not-a-url", false, null, "ServerUrl"));
        }

        [Test]
        public static void EnsureNonHttp_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => TransportSecurityPolicy.EnsureUrlAllowed("ftp://files.example/x", false, null, "ServerUrl"));
        }

        [Test]
        public static void Ctor_Https_Ok()
        {
            using (var client = new KingdeeWebApiClient("https://localhost/K3Cloud/", "A1", new FakeAuthProvider(), Ctx(), null))
            {
                Assert.IsNotNull(client);
            }
        }

        [Test]
        public static void Ctor_Http_Default_Throws()
        {
            // 默认强制 HTTPS：未显式放行明文 http 时，构造即失败（fail-fast）
            Assert.Throws<InvalidOperationException>(
                () => new KingdeeWebApiClient("http://localhost/K3Cloud/", "A1", new FakeAuthProvider(), Ctx(), null));
        }

        [Test]
        public static void Ctor_Http_AllowInsecure_Ok()
        {
            using (var client = new KingdeeWebApiClient("http://localhost/K3Cloud/", "A1", new FakeAuthProvider(), Ctx(), null, true))
            {
                Assert.IsNotNull(client);
            }
        }

        [Test]
        public static void SetRequestHeader_StoresExtraHeader()
        {
            using (var client = new KingdeeWebApiClient("https://localhost/K3Cloud/", "A1", new FakeAuthProvider(), Ctx(), null))
            {
                client.SetRequestHeader("X-Kingdee-Signature", "abc123");
                Assert.IsTrue(client.ExtraHeaders.ContainsKey("X-Kingdee-Signature"));
                Assert.AreEqual("abc123", client.ExtraHeaders["X-Kingdee-Signature"]);
            }
        }

        [Test]
        public static void SetRequestHeader_EmptyName_Throws()
        {
            using (var client = new KingdeeWebApiClient("https://localhost/K3Cloud/", "A1", new FakeAuthProvider(), Ctx(), null))
            {
                Assert.Throws<ArgumentException>(() => client.SetRequestHeader("", "v"));
                Assert.Throws<ArgumentException>(() => client.SetRequestHeader(null, "v"));
            }
        }

        [Test]
        public static void HmacSha256AppSecSigner_Deterministic_EmptyBody()
        {
            var signer = new HmacSha256AppSecSigner(
                "{appId}|{appSecret}|{timestamp}|{nonce}|{method}|{path}|{bodyHash}",
                () => new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                () => "n-123456");

            AppSecSignatureResult r = signer.Sign(new AppSecSigningContext
            {
                AppId = "app1",
                AppSecret = "secr3t",
                Method = "POST",
                Path = "/K3Cloud/",
                Body = ""
            });

            // 期望值由独立 Python 计算：HMAC-SHA256(appSecret, toSign)，toSign 为模板替换结果
            Assert.AreEqual("20e51a49edb51400b30f170e992e50fe9ff7742cfd9abcade0b9f50407876182", r.Signature);
            Assert.AreEqual("2026-01-01 00:00:00", r.Timestamp);
            Assert.AreEqual("n-123456", r.Nonce);
            Assert.AreEqual("X-Kingdee-Signature", r.HeaderName);
        }

        [Test]
        public static void HmacSha256AppSecSigner_Deterministic_HelloBody()
        {
            var signer = new HmacSha256AppSecSigner(
                "{appId}|{appSecret}|{timestamp}|{nonce}|{method}|{path}|{bodyHash}",
                () => new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                () => "n-123456");

            AppSecSignatureResult r = signer.Sign(new AppSecSigningContext
            {
                AppId = "app1",
                AppSecret = "secr3t",
                Method = "POST",
                Path = "/K3Cloud/",
                Body = "hello"
            });

            Assert.AreEqual("d10f2da72125ee11e25161d2dd6e0ea0474b1eeaeba23805efaa4ba988bd419e", r.Signature);
        }

        [Test]
        public static void HmacSha256AppSecSigner_EmptyTemplate_Throws()
        {
            // 空模板 = 未实现，应抛 NotSupportedException（带降级指引），而不是误报凭据缺失
            var signer = new HmacSha256AppSecSigner("");
            Assert.Throws<NotSupportedException>(
                () => signer.Sign(new AppSecSigningContext { AppId = "app1", AppSecret = "secr3t" }));

            var signer2 = new HmacSha256AppSecSigner(null);
            Assert.Throws<NotSupportedException>(
                () => signer2.Sign(new AppSecSigningContext { AppId = "app1", AppSecret = "secr3t" }));
        }

        [Test]
        public static void HmacSha256AppSecSigner_NullContext_Throws()
        {
            var signer = new HmacSha256AppSecSigner("{appId}");
            Assert.Throws<ArgumentNullException>(() => signer.Sign(null));
        }
    }
}