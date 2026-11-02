// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using System.Collections.Generic;
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

        [Test]
        public static void Dispose_ClearsCredentialState_AndIsIdempotent()
        {
            var client = new KingdeeWebApiClient("https://localhost/K3Cloud/", "A1", new FakeAuthProvider(), Ctx(), null);
            client.UseBearerToken("tok");
            client.SetRequestHeader("X-Kingdee-Signature", "sig");
            Assert.IsFalse(client.IsDisposed);
            Assert.AreEqual(1, client.ExtraHeaders.Count);

            client.Dispose();
            client.Dispose(); // 幂等：重复释放不抛异常

            Assert.IsTrue(client.IsDisposed);
            Assert.AreEqual(0, client.ExtraHeaders.Count);
        }

        [Test]
        public static void DisposedClient_RejectsFurtherTransportUse()
        {
            var client = new KingdeeWebApiClient("https://localhost/K3Cloud/", "A1", new FakeAuthProvider(), Ctx(), null);
            client.Dispose();
            Assert.Throws<ObjectDisposedException>(() => client.UseBearerToken("token"));
            Assert.Throws<ObjectDisposedException>(() => client.SetRequestHeader("X-Test", "v"));
            Assert.Throws<ObjectDisposedException>(() => client.PostJson("health.kdsvc", "{}"));
        }

        [Test]
        public static void EmptyEndpoint_IsRejectedClearly()
        {
            using (var client = new KingdeeWebApiClient("https://localhost/K3Cloud/", "A1", new FakeAuthProvider(), Ctx(), null))
            {
                Assert.Throws<ArgumentException>(() => client.PostJson("", "{}"));
                Assert.Throws<ArgumentException>(() => client.PostForm("   ", new Dictionary<string, string>()));
            }
        }

        [Test]
        public static void InspectResult_AcceptsStringSuccessFlag()
        {
            var parsed = KingdeeWebApiClient.InspectResult(
                "{\"Result\":{\"ResponseStatus\":{\"IsSuccess\":\"true\",\"Errors\":[]}}}");
            Assert.IsTrue(parsed.IsSuccess);
        }

        [Test]
        public static void InspectResult_HandlesNullRoot()
        {
            var parsed = KingdeeWebApiClient.InspectResult("null");
            Assert.IsFalse(parsed.IsSuccess);
            Assert.Contains(parsed.Message, "根节点为空");
        }

        [Test]
        public static void BillOperations_RejectMissingFormId()
        {
            using (var client = new KingdeeWebApiClient("https://localhost/K3Cloud/", "A1", new FakeAuthProvider(), Ctx(), null))
            {
                Assert.Throws<ArgumentException>(() => client.Save(" ", new object()));
                Assert.Throws<ArgumentException>(() => client.ExecuteBillQuery(null, "FBillNo", "", ""));
                Assert.Throws<ArgumentException>(() => client.ExecuteBillQuery("PUR", " ", "", ""));
            }
        }

        [Test]
        public static void BillOperations_RejectEmptyOrInvalidIds()
        {
            using (var client = new KingdeeWebApiClient("https://localhost/K3Cloud/", "A1", new FakeAuthProvider(), Ctx(), null))
            {
                Assert.Throws<ArgumentException>(() => client.Submit("PUR", null));
                Assert.Throws<ArgumentException>(() => client.Audit("PUR", new List<long>()));
                Assert.Throws<ArgumentException>(() => client.Delete("PUR", new List<long> { 0 }));
            }
        }

        [Test]
        public static void BillQuery_RejectsNegativePaging()
        {
            using (var client = new KingdeeWebApiClient("https://localhost/K3Cloud/", "A1", new FakeAuthProvider(), Ctx(), null))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => client.ExecuteBillQuery("PUR", "FBillNo", "", "", -1));
                Assert.Throws<ArgumentOutOfRangeException>(() => client.ExecuteBillQuery("PUR", "FBillNo", "", "", 0, -1));
                Assert.Throws<ArgumentOutOfRangeException>(() => client.ExecuteBillQuery("PUR", "FBillNo", "", "", 0, 0, -1));
            }
        }

        [Test]
        public static void BillOperations_RejectDuplicateIds()
        {
            using (var client = new KingdeeWebApiClient("https://localhost/K3Cloud/", "A1", new FakeAuthProvider(), Ctx(), null))
            {
                Assert.Throws<ArgumentException>(() => client.Audit("PUR", new List<long> { 7, 7 }));
            }
        }
    }
}
