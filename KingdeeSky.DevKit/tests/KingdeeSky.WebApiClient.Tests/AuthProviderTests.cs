// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using KingdeeSky.WebApiClient.Security;

namespace KingdeeSky.WebApiClient.Tests
{
    /// <summary>鉴权实现测试：Password / OAuth2 / AppSec 与工厂。</summary>
    public static class AuthProviderTests
    {
        [Test]
        public static void Password_Name()
        {
            Assert.AreEqual("password", new PasswordAuthProvider().Name);
        }

        [Test]
        public static void Password_Authenticate_UsesSecretAndTransport()
        {
            var secrets = new FakeSecretProvider().Add("KINGDEE_PASSWORD", "s3cret");
            var ctx = new AuthContext { AcctId = "A1", UserName = "u", Lcid = 2052, Secrets = secrets };
            var t = new FakeTransport { JsonResponse = "{\"LoginResultType\":1,\"Message\":\"ok\",\"echo\":\"s3cret\"}" };
            LoginResult r = new PasswordAuthProvider().Authenticate(t, ctx);

            Assert.IsTrue(r.Success);
            Assert.AreEqual(1, r.LoginResultType);
            Assert.AreEqual(1, t.PostJsonCalls.Count);
            Assert.Contains(t.PostJsonCalls[0], PasswordAuthProvider.ValidateUserService);
            Assert.Contains(t.PostJsonCalls[0], "\"acctID\":\"A1\"");
            Assert.Contains(t.PostJsonCalls[0], "\"username\":\"u\"");
            // 请求体会携带密码（发给服务器），返回 RawJson 必须脱敏
            Assert.Contains(t.PostJsonCalls[0], "s3cret");
            Assert.NotContains(r.RawJson, "s3cret");
            Assert.Contains(r.RawJson, "REDACTED");
        }

        [Test]
        public static void Password_MissingSecret_Throws()
        {
            var ctx = new AuthContext { AcctId = "A1", UserName = "u", Secrets = new FakeSecretProvider() };
            Assert.Throws<SecretNotFoundException>(
                () => new PasswordAuthProvider().Authenticate(new FakeTransport(), ctx));
        }

        [Test]
        public static void Password_LoginFailed_ReturnsSuccessFalse()
        {
            var secrets = new FakeSecretProvider().Add("KINGDEE_PASSWORD", "s3cret");
            var ctx = new AuthContext { AcctId = "A1", UserName = "u", Secrets = secrets };
            var t = new FakeTransport { JsonResponse = "{\"LoginResultType\":0,\"Message\":\"bad creds\"}" };
            LoginResult r = new PasswordAuthProvider().Authenticate(t, ctx);
            Assert.IsFalse(r.Success);
            Assert.AreEqual(0, r.LoginResultType);
        }

        [Test]
        public static void OAuth2_NoTokenEndpoint_Throws()
        {
            var ctx = new AuthContext { Secrets = new FakeSecretProvider() };
            Assert.Throws<InvalidOperationException>(
                () => new OAuth2AuthProvider().Authenticate(new FakeTransport(), ctx));
        }

        [Test]
        public static void OAuth2_TokenFlow_Success()
        {
            var secrets = new FakeSecretProvider()
                .Add("KINGDEE_CLIENT_ID", "cid")
                .Add("KINGDEE_CLIENT_SECRET", "csecret");
            var ctx = new AuthContext { TokenEndpoint = "https://auth.example/token", Scope = "api", Secrets = secrets };
            var t = new FakeTransport { FormResponse = "{\"access_token\":\"tok123\",\"token_type\":\"Bearer\"}" };
            LoginResult r = new OAuth2AuthProvider().Authenticate(t, ctx);

            Assert.IsTrue(r.Success);
            Assert.AreEqual("tok123", t.BearerToken);
            Assert.AreEqual(1, t.PostFormCalls.Count);
            Assert.Contains(t.PostFormCalls[0], "https://auth.example/token");
            Assert.Contains(t.PostFormCalls[0], "grant_type=client_credentials");
            Assert.Contains(t.PostFormCalls[0], "client_id=cid");
            Assert.Contains(t.PostFormCalls[0], "client_secret=csecret");
            // 返回信息脱敏：不含 client_secret 与 access_token
            Assert.NotContains(r.RawJson, "csecret");
            Assert.NotContains(r.RawJson, "tok123");
            Assert.Contains(r.RawJson, "REDACTED");
        }

        [Test]
        public static void OAuth2_NoAccessToken_ReturnsFailure()
        {
            var secrets = new FakeSecretProvider()
                .Add("KINGDEE_CLIENT_ID", "cid")
                .Add("KINGDEE_CLIENT_SECRET", "csecret");
            var ctx = new AuthContext { TokenEndpoint = "https://auth.example/token", Secrets = secrets };
            var t = new FakeTransport { FormResponse = "{\"error\":\"invalid_grant\"}" };
            LoginResult r = new OAuth2AuthProvider().Authenticate(t, ctx);
            Assert.IsFalse(r.Success);
            Assert.IsNull(t.BearerToken);
        }

        [Test]
        public static void AppSec_NotSupported()
        {
            var ctx = new AuthContext { Secrets = new FakeSecretProvider() };
            Assert.Throws<NotSupportedException>(
                () => new AppIdAppSecAuthProvider().Authenticate(new FakeTransport(), ctx));
        }

        [Test]
        public static void Factory_MapsModes()
        {
            Assert.InstanceOf<PasswordAuthProvider>(AuthProviderFactory.Create("password"));
            Assert.InstanceOf<PasswordAuthProvider>(AuthProviderFactory.Create(""));
            Assert.InstanceOf<PasswordAuthProvider>(AuthProviderFactory.Create(null));
            Assert.InstanceOf<OAuth2AuthProvider>(AuthProviderFactory.Create("oauth2"));
            Assert.InstanceOf<OAuth2AuthProvider>(AuthProviderFactory.Create("oauth"));
            Assert.InstanceOf<AppIdAppSecAuthProvider>(AuthProviderFactory.Create("appsec"));
            Assert.InstanceOf<AppIdAppSecAuthProvider>(AuthProviderFactory.Create("appid_appsec"));
        }

        [Test]
        public static void Factory_UnknownMode_Throws()
        {
            Assert.Throws<ArgumentException>(() => AuthProviderFactory.Create("bogus"));
        }

        [Test]
        public static void OAuth2_ErrorDescription_Reported()
        {
            // RFC 6749 错误响应应把服务端 error_description 透传，便于定位配置/授权问题
            var secrets = new FakeSecretProvider()
                .Add("KINGDEE_CLIENT_ID", "cid")
                .Add("KINGDEE_CLIENT_SECRET", "csecret");
            var ctx = new AuthContext { TokenEndpoint = "https://auth.example/token", Secrets = secrets };
            var t = new FakeTransport { FormResponse = "{\"error\":\"invalid_grant\",\"error_description\":\"bad scope\"}" };
            LoginResult r = new OAuth2AuthProvider().Authenticate(t, ctx);
            Assert.IsFalse(r.Success);
            Assert.Contains(r.Message, "bad scope");
        }

        [Test]
        public static void OAuth2_ErrorOnly_FallsBackToError()
        {
            var secrets = new FakeSecretProvider()
                .Add("KINGDEE_CLIENT_ID", "cid")
                .Add("KINGDEE_CLIENT_SECRET", "csecret");
            var ctx = new AuthContext { TokenEndpoint = "https://auth.example/token", Secrets = secrets };
            var t = new FakeTransport { FormResponse = "{\"error\":\"invalid_grant\"}" };
            LoginResult r = new OAuth2AuthProvider().Authenticate(t, ctx);
            Assert.IsFalse(r.Success);
            Assert.Contains(r.Message, "invalid_grant");
        }

        [Test]
        public static void AppSec_WithSigner_WritesHeaders()
        {
            var secrets = new FakeSecretProvider()
                .Add("KINGDEE_APP_ID", "app1")
                .Add("KINGDEE_APP_SECRET", "secr3t");
            var ctx = new AuthContext { Secrets = secrets };
            var signer = new FakeAppSecSigner();
            var t = new FakeTransport();
            LoginResult r = new AppIdAppSecAuthProvider(signer).Authenticate(t, ctx);

            Assert.IsTrue(r.Success);
            Assert.AreEqual(1, r.LoginResultType);
            Assert.AreEqual("sig-fixed", t.Headers["X-Kingdee-Signature"]);
            Assert.AreEqual("2026-01-01 00:00:00", t.Headers["X-Kingdee-Timestamp"]);
            Assert.AreEqual("n-123", t.Headers["X-Kingdee-Nonce"]);
            Assert.AreEqual("app1", signer.LastContext.AppId);
            Assert.AreEqual("secr3t", signer.LastContext.AppSecret);
            Assert.AreEqual("POST", signer.LastContext.Method);
            Assert.AreEqual("/K3Cloud/", signer.LastContext.Path);
        }

        [Test]
        public static void AppSec_MissingSecret_Throws()
        {
            var ctx = new AuthContext { Secrets = new FakeSecretProvider() };
            var signer = new FakeAppSecSigner();
            Assert.Throws<SecretNotFoundException>(
                () => new AppIdAppSecAuthProvider(signer).Authenticate(new FakeTransport(), ctx));
        }

        [Test]
        public static void Factory_AppSec_WithTemplate_CreatesSigner()
        {
            var secrets = new FakeSecretProvider()
                .Add("KINGDEE_APP_ID", "app1")
                .Add("KINGDEE_APP_SECRET", "secr3t");
            var ctx = new AuthContext { Secrets = secrets };
            var t = new FakeTransport();
            IAuthProvider p = AuthProviderFactory.Create("appsec", "{appId}|{appSecret}|{timestamp}|{nonce}|{method}|{path}|{bodyHash}");
            LoginResult r = p.Authenticate(t, ctx);
            Assert.IsTrue(r.Success);
            Assert.IsTrue(t.Headers.ContainsKey("X-Kingdee-Signature"));
            Assert.IsFalse(string.IsNullOrEmpty(t.Headers["X-Kingdee-Signature"]));
        }

        [Test]
        public static void Factory_AppSec_NoTemplate_Placeholder()
        {
            var ctx = new AuthContext { Secrets = new FakeSecretProvider() };
            IAuthProvider p = AuthProviderFactory.Create("appsec", "");
            Assert.Throws<NotSupportedException>(() => p.Authenticate(new FakeTransport(), ctx));
        }
    }
}