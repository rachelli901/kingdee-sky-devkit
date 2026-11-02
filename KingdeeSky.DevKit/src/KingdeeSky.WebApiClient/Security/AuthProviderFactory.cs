// Author: rachelli901 <lir30481@gmail.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

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
            return Create(mode, appSecSigningTemplate, null);
        }

        /// <param name="appSecSigningTemplate">AppID/AppSec 签名模板；仅 appsec 模式使用，空则保持占位实现。</param>
        /// <param name="appSecTimestampFormat">AppSec 签名时间戳格式；留空取签名器默认值（yyyy-MM-dd HH:mm:ss）。</param>
        public static IAuthProvider Create(string mode, string appSecSigningTemplate, string appSecTimestampFormat)
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
                        return new AppIdAppSecAuthProvider(
                            new HmacSha256AppSecSigner(appSecSigningTemplate, null, null, appSecTimestampFormat));
                    }
                    return new AppIdAppSecAuthProvider();
                default:
                    throw new ArgumentException("未知的 AuthMode：" + mode + "（可选 password / oauth2 / appsec）");
            }
        }
    }
}