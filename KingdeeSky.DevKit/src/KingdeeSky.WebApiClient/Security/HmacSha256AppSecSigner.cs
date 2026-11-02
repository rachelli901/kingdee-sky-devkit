// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace KingdeeSky.WebApiClient.Security
{
    /// <summary>
    /// 模板化 HMAC-SHA256 签名器：按模板把请求要素拼成待签串，用 appSecret 作 HMAC 密钥。
    /// 模板支持占位符：{appId} {appSecret} {timestamp} {nonce} {method} {path} {body} {bodyHash} {query}。
    /// 模板为空或仅含占位时抛 NotSupportedException 并给出降级指引，避免把“未实现”误报为“凭据缺失”。
    /// 接入星空真实环境时，把【第三方系统登录授权】给出的签名字段/拼接顺序/时间戳格式整理成模板即可。
    /// </summary>
    public sealed class HmacSha256AppSecSigner : IAppSecSigner
    {
        /// <summary>默认时间戳格式（与占位说明中的「年-月-日 时:分:秒」一致）。</summary>
        public const string DefaultTimestampFormat = "yyyy-MM-dd HH:mm:ss";

        /// <summary>时间戳格式特殊取值：Unix 秒整数戳。</summary>
        public const string UnixSecondsFormat = "unix";

        /// <summary>时间戳格式特殊取值：Unix 毫秒整数戳。</summary>
        public const string UnixMillisecondsFormat = "unix-ms";

        private readonly string _template;
        private readonly Func<DateTime> _clock;
        private readonly Func<string> _nonceFactory;
        private readonly string _timestampFormat;

        public HmacSha256AppSecSigner(string template)
            : this(template, null, null, null)
        {
        }

        /// <param name="clock">时间戳工厂（默认 UtcNow，测试可注入固定时间）。</param>
        /// <param name="nonceFactory">随机串工厂（默认 GUID，测试可注入固定值）。</param>
        public HmacSha256AppSecSigner(string template, Func<DateTime> clock, Func<string> nonceFactory)
            : this(template, clock, nonceFactory, null)
        {
        }

        /// <param name="timestampFormat">
        /// 时间戳格式（抽为可配置项，对应 App.config 的 AppSecTimestampFormat）：留空取 <see cref="DefaultTimestampFormat"/>；
        /// 可填协议要求的自定义 .NET 格式串，或用 <see cref="UnixSecondsFormat"/> / <see cref="UnixMillisecondsFormat"/> 输出秒/毫秒整数戳。
        /// 真实签名必须以账套【第三方系统登录授权】的在线测试样本对齐，勿凭猜测。
        /// </param>
        public HmacSha256AppSecSigner(string template, Func<DateTime> clock, Func<string> nonceFactory, string timestampFormat)
        {
            _template = template ?? string.Empty;
            _clock = clock ?? (() => DateTime.UtcNow);
            _nonceFactory = nonceFactory ?? (() => Guid.NewGuid().ToString("N"));
            _timestampFormat = string.IsNullOrWhiteSpace(timestampFormat) ? DefaultTimestampFormat : timestampFormat.Trim();
        }

        /// <summary>当前生效的时间戳格式。</summary>
        public string TimestampFormat { get { return _timestampFormat; } }

        public string Name { get { return "hmac-sha256"; } }

        public AppSecSignatureResult Sign(AppSecSigningContext context)
        {
            if (context == null) { throw new ArgumentNullException("context"); }
            if (string.IsNullOrWhiteSpace(_template))
            {
                throw new NotSupportedException(
                    "AppID/AppSec 签名模板未配置（AppSecSigningTemplate 为空）。" +
                    "请按星空主控台【系统管理 → 系统管理 → 授权 → 第三方系统登录授权】的规范填写签名模板，" +
                    "或改用 AuthMode=password / oauth2。");
            }

            string nonce = string.IsNullOrWhiteSpace(context.Nonce)
                ? _nonceFactory()
                : context.Nonce;
            string timestamp = string.IsNullOrWhiteSpace(context.Timestamp)
                ? FormatTimestamp(_clock())
                : context.Timestamp;
            string body = context.Body ?? string.Empty;
            string bodyHash = Sha256Hex(body);
            string method = string.IsNullOrWhiteSpace(context.Method) ? "POST" : context.Method;

            string toSign = _template
                .Replace("{appId}", context.AppId ?? string.Empty)
                .Replace("{appSecret}", context.AppSecret ?? string.Empty)
                .Replace("{timestamp}", timestamp)
                .Replace("{nonce}", nonce)
                .Replace("{method}", method)
                .Replace("{path}", context.Path ?? string.Empty)
                .Replace("{body}", body)
                .Replace("{bodyHash}", bodyHash)
                .Replace("{query}", context.Query ?? string.Empty);

            string signature = HmacSha256Hex(context.AppSecret ?? string.Empty, toSign);
            return new AppSecSignatureResult
            {
                Signature = signature,
                HeaderName = "X-Kingdee-Signature",
                Timestamp = timestamp,
                Nonce = nonce
            };
        }

        /// <summary>
        /// 按配置格式生成时间戳：unix / unix-ms 输出秒/毫秒整数戳，其余按 .NET 格式串输出。
        /// 一律用固定区域性（InvariantCulture），避免不同区域设置产生格式差异。
        /// </summary>
        private string FormatTimestamp(DateTime now)
        {
            if (string.Equals(_timestampFormat, UnixSecondsFormat, StringComparison.OrdinalIgnoreCase))
            {
                return ToUnixSeconds(now).ToString(CultureInfo.InvariantCulture);
            }
            if (string.Equals(_timestampFormat, UnixMillisecondsFormat, StringComparison.OrdinalIgnoreCase))
            {
                return ToUnixMilliseconds(now).ToString(CultureInfo.InvariantCulture);
            }
            return now.ToString(_timestampFormat, CultureInfo.InvariantCulture);
        }

        private static long ToUnixSeconds(DateTime value)
        {
            DateTime utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            return (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        private static long ToUnixMilliseconds(DateTime value)
        {
            DateTime utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            return (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }

        private static string Sha256Hex(string input)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input ?? string.Empty));
                return BytesToHex(hash);
            }
        }

        private static string HmacSha256Hex(string key, string input)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key ?? string.Empty)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input ?? string.Empty));
                return BytesToHex(hash);
            }
        }

        private static string BytesToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) { sb.Append(b.ToString("x2")); }
            return sb.ToString();
        }
    }
}