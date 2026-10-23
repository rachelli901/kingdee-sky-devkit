using System;

namespace KingdeeSky.WebApiClient.Security
{
    /// <summary>按配置创建鉴权实现，使鉴权方式可外部切换。</summary>
    public static class AuthProviderFactory
    {
        /// <summary>兼容旧调用：无签名模板时 appsec 走占位实现。</summary>
        public static IAuthProvider Create(string mode)
        {
            return Create(mode, null);
        }

        /// <param name="appSecSigningTemplate">AppID/AppSec 签名模板；仅 appsec 模式使用，空则保持占位实现。</param>
        public static IAuthProvider Create(string mode, string appSecSigningTemplate)
        {
            switch ((mode ?? "password").Trim().ToLowerInvariant())
            {
                case "":
                case "password":
                    return new PasswordAuthProvider();
                case "oauth":
                case "oauth2":
                    return new OAuth2AuthProvider();
                case "appsec":
                case "appid_appsec":
                    if (!string.IsNullOrWhiteSpace(appSecSigningTemplate))
                    {
                        return new AppIdAppSecAuthProvider(new HmacSha256AppSecSigner(appSecSigningTemplate));
                    }
                    return new AppIdAppSecAuthProvider();
                default:
                    throw new ArgumentException("未知的 AuthMode：" + mode + "（可选 password / oauth2 / appsec）");
            }
        }
    }
}