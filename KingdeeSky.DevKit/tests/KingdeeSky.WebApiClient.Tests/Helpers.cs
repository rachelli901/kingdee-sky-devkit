using System;
using System.Collections.Generic;
using System.Text;
using KingdeeSky.WebApiClient.Security;

namespace KingdeeSky.WebApiClient.Tests
{
    /// <summary>内存密钥源：用于测试 ISecretProvider 调用方。</summary>
    public sealed class FakeSecretProvider : ISecretProvider
    {
        private readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public FakeSecretProvider Add(string key, string value)
        {
            _map[key] = value;
            return this;
        }

        public string Name { get { return "fake"; } }

        public bool TryGet(string key, out string value)
        {
            return _map.TryGetValue(key, out value) && !string.IsNullOrEmpty(value);
        }
    }

    /// <summary>记录调用并返回预设响应的传输桩。</summary>
    public sealed class FakeTransport : IApiTransport
    {
        public readonly List<string> PostJsonCalls = new List<string>();
        public readonly List<string> PostFormCalls = new List<string>();
        public string JsonResponse = "{}";
        public string FormResponse = "{}";
        public string BearerToken;

        public string PostJson(string pathOrUrl, string jsonBody)
        {
            PostJsonCalls.Add(pathOrUrl + "|" + jsonBody);
            return JsonResponse;
        }

        public string PostForm(string pathOrUrl, IDictionary<string, string> form)
        {
            var sb = new StringBuilder();
            foreach (KeyValuePair<string, string> kv in form)
            {
                sb.Append(kv.Key).Append('=').Append(kv.Value).Append('&');
            }
            PostFormCalls.Add(pathOrUrl + "|" + sb);
            return FormResponse;
        }

        public void UseBearerToken(string token)
        {
            BearerToken = token;
        }

        public readonly Dictionary<string, string> Headers = new Dictionary<string, string>();

        public void SetRequestHeader(string name, string value)
        {
            Headers[name] = value;
        }
    }

    /// <summary>记录调用并返回预设结果的鉴权桩。</summary>
    public sealed class FakeAuthProvider : IAuthProvider
    {
        public bool Called;
        public LoginResult Result = new LoginResult { Success = true, LoginResultType = 1, Message = "fake ok" };

        public string Name { get { return "fake"; } }

        public LoginResult Authenticate(IApiTransport transport, AuthContext context)
        {
            Called = true;
            return Result;
        }
    }

    /// <summary>固定输出签名与请求头的签名器桩（AppID/AppSec 场景）。</summary>
    public sealed class FakeAppSecSigner : IAppSecSigner
    {
        public AppSecSigningContext LastContext;
        public string Signature = "sig-fixed";
        public string Timestamp = "2026-01-01 00:00:00";
        public string Nonce = "n-123";

        public string Name { get { return "fake-signer"; } }

        public AppSecSignatureResult Sign(AppSecSigningContext context)
        {
            LastContext = context;
            return new AppSecSignatureResult
            {
                Signature = Signature,
                HeaderName = "X-Kingdee-Signature",
                Timestamp = Timestamp,
                Nonce = Nonce
            };
        }
    }
}