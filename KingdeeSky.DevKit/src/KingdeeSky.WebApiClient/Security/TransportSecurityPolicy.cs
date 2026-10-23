// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using KingdeeSky.WebApiClient.Logging;

namespace KingdeeSky.WebApiClient.Security
{
    /// <summary>
    /// 传输安全策略：默认强制 HTTPS；仅当显式放行时才允许明文 HTTP，且放行时输出告警。
    /// 用于客户端“传输安全开关”的 fail-fast 校验：不允许的传输通道在构造/请求前即拒绝，避免静默明文外发。
    /// </summary>
    public static class TransportSecurityPolicy
    {
        /// <summary>App.config 中放行明文 HTTP 的开关键名。</summary>
        public const string AllowInsecureHttpKey = "AllowInsecureHttp";

        /// <summary>App.config 中放宽证书校验的开关键名（自签名/测试环境专用）。</summary>
        public const string AllowInvalidCertificateKey = "AllowInvalidCertificate";

        /// <summary>把配置串解析为布尔开关；无法识别时回退默认值（默认严格）。</summary>
        public static bool ParseFlag(string value, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value)) { return defaultValue; }
            string t = value.Trim().ToLowerInvariant();
            if (t == "1" || t == "true" || t == "yes" || t == "on") { return true; }
            if (t == "0" || t == "false" || t == "no" || t == "off") { return false; }
            return defaultValue;
        }

        /// <summary>
        /// 校验 URL 是否允许使用当前配置的传输通道。
        /// https 放行；http 仅在 allowInsecureHttp=true 时放行并告警，否则抛 InvalidOperationException；
        /// 非 http/https 协议抛 ArgumentException。label 用于报错与告警文案（如 ServerUrl / 请求地址）。
        /// </summary>
        public static string EnsureUrlAllowed(string url, bool allowInsecureHttp, SafeLogger logger, string label)
        {
            if (string.IsNullOrWhiteSpace(url)) { throw new ArgumentException(label + " 不能为空"); }

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                throw new ArgumentException(label + " 不是合法绝对 URL：" + url);
            }

            string scheme = (uri.Scheme ?? string.Empty).ToLowerInvariant();
            if (scheme == "https")
            {
                return url;
            }
            if (scheme == "http")
            {
                if (allowInsecureHttp)
                {
                    if (logger != null)
                    {
                        logger.Warn(label + " 使用明文 HTTP 传输（已显式放行 AllowInsecureHttp=true）：" + url +
                            "。生产环境必须关闭该开关并改用 HTTPS。");
                    }
                    return url;
                }
                throw new InvalidOperationException(
                    label + " 使用明文 HTTP，但 AllowInsecureHttp 未开启（默认强制 HTTPS）。" +
                    "请改用 https 地址；确属本地开发环境时，在 App.config 显式设置 AllowInsecureHttp=true 并知悉明文传输风险。");
            }

            throw new ArgumentException(label + " 仅支持 http/https，当前协议：" + (string.IsNullOrEmpty(scheme) ? "(空)" : scheme));
        }
    }
}