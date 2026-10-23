// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace KingdeeSky.WebApiClient.Security
{
    /// <summary>
    /// OAuth 2.0 鉴权（client_credentials，服务间集成、无人值守），按标准流程实现：
    /// 向 TokenEndpoint 提交 grant_type/client_id/client_secret，取回 access_token 后交由传输层附加 Bearer 令牌。
    /// 注意：TokenEndpoint 与字段命名需按实际星空版本或统一认证网关确认；未配置 TokenEndpoint 时本实现会明确报错，
    /// 此时请改用 AuthMode=password。本实现用于验证“鉴权方式可配置替换”的骨架，接入真实环境前需联调。
    /// </summary>
    public sealed class OAuth2AuthProvider : IAuthProvider
    {
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 };

        public string Name { get { return "oauth2"; } }

        public LoginResult Authenticate(IApiTransport transport, AuthContext context)
        {
            if (string.IsNullOrWhiteSpace(context.TokenEndpoint))
            {
                throw new InvalidOperationException(
                    "OAuth 2.0 鉴权需要配置 TokenEndpoint（星空或统一认证网关的令牌端点）。" +
                    "若当前环境不提供令牌端点，请改用 AuthMode=password。");
            }

            string clientId = context.Secrets.Get(context.ClientIdKey);
            string clientSecret = context.Secrets.Get(context.ClientSecretKey);

            var form = new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", clientId },
                { "client_secret", clientSecret }
            };
            if (!string.IsNullOrWhiteSpace(context.Scope)) { form["scope"] = context.Scope; }

            string raw = transport.PostForm(context.TokenEndpoint, form);
            string safeRaw = SecretRedactor.Redact(raw, clientSecret);

            string token = null;
            string error = null;
            string errorDescription = null;
            var dict = _json.DeserializeObject(raw) as IDictionary<string, object>;
            if (dict != null)
            {
                object v;
                if (dict.TryGetValue("access_token", out v)) { token = Convert.ToString(v); }
                if (dict.TryGetValue("error", out v)) { error = Convert.ToString(v); }
                if (dict.TryGetValue("error_description", out v)) { errorDescription = Convert.ToString(v); }
            }

            if (string.IsNullOrEmpty(token))
            {
                // RFC 6749 错误响应：error + error_description；把服务端返回的具体原因带进报错，便于定位配置/授权问题
                string detail = string.IsNullOrWhiteSpace(errorDescription) ? error : errorDescription;
                string message = string.IsNullOrWhiteSpace(detail)
                    ? "OAuth 2.0 令牌端点未返回 access_token，请核对 TokenEndpoint 与字段契约。"
                    : "OAuth 2.0 令牌端点返回错误：" + detail +
                      "，请核对 TokenEndpoint、client_id/client_secret 与授权范围。";
                return new LoginResult
                {
                    Success = false,
                    LoginResultType = 0,
                    Message = message,
                    RawJson = safeRaw
                };
            }

            transport.UseBearerToken(token);
            return new LoginResult
            {
                Success = true,
                LoginResultType = 1,
                Message = "OAuth 2.0 client_credentials 鉴权成功",
                RawJson = SecretRedactor.Redact(safeRaw, token)
            };
        }
    }
}