// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using KingdeeSky.WebApiClient.Security;

namespace KingdeeSky.WebApiClient.Tests
{
    /// <summary>客户端调用链测试：构造校验与 Login 委托。</summary>
    public static class ClientTests
    {
        private static AuthContext Ctx()
        {
            return new AuthContext { AcctId = "A1", UserName = "u", Secrets = new FakeSecretProvider() };
        }

        [Test]
        public static void Ctor_EmptyServerUrl_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new KingdeeWebApiClient("", "A1", new FakeAuthProvider(), Ctx(), null));
        }

        [Test]
        public static void Ctor_EmptyAcctId_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new KingdeeWebApiClient("http://localhost/K3Cloud/", "", new FakeAuthProvider(), Ctx(), null));
        }

        [Test]
        public static void Ctor_NullAuthProvider_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new KingdeeWebApiClient("http://localhost/K3Cloud/", "A1", null, Ctx(), null));
        }

        [Test]
        public static void Ctor_NullAuthContext_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new KingdeeWebApiClient("http://localhost/K3Cloud/", "A1", new FakeAuthProvider(), null, null));
        }

        [Test]
        public static void Login_DelegatesToAuthProvider()
        {
            var fakeAuth = new FakeAuthProvider();
            using (var client = new KingdeeWebApiClient("https://localhost/K3Cloud/", "A1", fakeAuth, Ctx(), null))
            {
                LoginResult r = client.Login();
                Assert.IsTrue(fakeAuth.Called);
                Assert.IsTrue(r.Success);
                Assert.AreEqual("fake ok", r.Message);
            }
        }

        [Test]
        public static void Login_PropagatesAuthResult()
        {
            var fakeAuth = new FakeAuthProvider
            {
                Result = new LoginResult { Success = false, LoginResultType = 0, Message = "denied" }
            };
            using (var client = new KingdeeWebApiClient("https://localhost/K3Cloud/", "A1", fakeAuth, Ctx(), null))
            {
                LoginResult r = client.Login();
                Assert.IsFalse(r.Success);
                Assert.AreEqual(0, r.LoginResultType);
            }
        }
    }
}