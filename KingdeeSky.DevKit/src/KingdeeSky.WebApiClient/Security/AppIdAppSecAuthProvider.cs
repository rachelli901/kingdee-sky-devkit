using System;

namespace KingdeeSky.WebApiClient.Security
{
    /// <summary>
    /// AppID / AppSec 签名制鉴权（星空原生“第三方系统登录授权”）的可用骨架。
    /// 未注入签名器时保持占位行为（抛 NotSupportedException），避免把“功能未实现”误报为“凭据缺失”；
    /// 注入签名器（如 HmacSha256AppSecSigner）后，从密钥源读取 AppId/AppSecret、生成签名请求头并写入传输层，
    /// 后续每次业务请求自动附带。星空各版本的签名字段与算法以【第三方系统登录授权】规范为准，
    /// 真实环境联调时把规范整理成签名模板即可，无需改动调用方。
    /// </summary>
    public sealed class AppIdAppSecAuthProvider : IAuthProvider
    {
        private readonly IAppSecSigner _signer;

        /// <summary>占位构造：不注入签名器，Authenticate 抛 NotSupportedException。</summary>
        public AppIdAppSecAuthProvider()
            : this(null)
        {
        }

        /// <param name="signer">签名器；可传 null 保持占位行为。</param>
        public AppIdAppSecAuthProvider(IAppSecSigner signer)
        {
            _signer = signer;
        }

        public string Name { get { return "appsec"; } }

        public LoginResult Authenticate(IApiTransport transport, AuthContext context)
        {
            if (_signer == null)
            {
                // 占位实现：无论凭据是否已配置，一律提示未实现，避免把“功能未实现”误报为“凭据缺失”。
                throw new NotSupportedException(
                    "AppID/AppSec 签名制鉴权尚未实现：星空不同版本的签名参数字段与算法需按其开放平台规范确定。" +
                    "已提供 HmacSha256AppSecSigner 可用骨架，配置 App.config 的 AppSecSigningTemplate 即可启用；" +
                    "或改用 AuthMode=password / oauth2。");
            }

            string appId = context.Secrets.Get(context.AppIdKey);
            string appSecret = context.Secrets.Get(context.AppSecretKey);

            // 签名制无独立登录步骤：登录即预生成签名请求头，后续业务请求自动附带。
            var ctx = new AppSecSigningContext
            {
                AppId = appId,
                AppSecret = appSecret,
                Method = "POST",
                Path = "/K3Cloud/",
                Body = string.Empty
            };
            AppSecSignatureResult result = _signer.Sign(ctx);

            transport.SetRequestHeader(result.HeaderName, result.Signature);
            if (!string.IsNullOrWhiteSpace(result.Timestamp))
            {
                transport.SetRequestHeader("X-Kingdee-Timestamp", result.Timestamp);
            }
            if (!string.IsNullOrWhiteSpace(result.Nonce))
            {
                transport.SetRequestHeader("X-Kingdee-Nonce", result.Nonce);
            }

            return new LoginResult
            {
                Success = true,
                LoginResultType = 1,
                Message = "AppID/AppSec 签名已注入传输层（骨架实现，请按星空规范联调签名字段）"
            };
        }
    }
}