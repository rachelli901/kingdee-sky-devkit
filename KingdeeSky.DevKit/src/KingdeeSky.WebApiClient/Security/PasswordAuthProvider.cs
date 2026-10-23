// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace KingdeeSky.WebApiClient.Security
{
    /// <summary>
    /// 账号密码鉴权：调用星空 ValidateUser 端点（acctID/username/password/lcid），成功后由传输层维持 Cookie 会话。
    /// 密码经 ISecretProvider 获取，不写在配置文件里。
    /// </summary>
    public sealed class PasswordAuthProvider : IAuthProvider
    {
        public const string ValidateUserService =
            "Kingdee.BOS.WebApi.ServicesStub.AuthService.ValidateUser.common.kdsvc";

        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 };

        public string Name { get { return "password"; } }

        public LoginResult Authenticate(IApiTransport transport, AuthContext context)
        {
            string password = context.Secrets.Get(context.PasswordKey);
            var payload = new Dictionary<string, object>
            {
                { "acctID", context.AcctId },
                { "username", context.UserName },
                { "password", password },
                { "lcid", context.Lcid }
            };
            string raw = transport.PostJson(ValidateUserService, _json.Serialize(payload));

            var dict = _json.DeserializeObject(raw) as IDictionary<string, object>;
            int loginType = 0;
            string message = null;
            if (dict != null)
            {
                object t;
                if (dict.TryGetValue("LoginResultType", out t)) { int.TryParse(Convert.ToString(t), out loginType); }
                object m;
                if (dict.TryGetValue("Message", out m)) { message = Convert.ToString(m); }
            }
            return new LoginResult
            {
                Success = loginType == 1,
                LoginResultType = loginType,
                Message = message,
                RawJson = SecretRedactor.Redact(raw, password)
            };
        }
    }
}