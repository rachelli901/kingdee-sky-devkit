using System;

namespace KingdeeSky.WebApiClient.Security
{
    /// <summary>文本脱敏：把密钥从日志/异常信息中替换为固定占位，避免明文外泄。</summary>
    public static class SecretRedactor
    {
        public const string Mask = "***REDACTED***";

        /// <summary>生成短掩码用于展示（首尾各留 2 个字符）。</summary>
        public static string MaskShort(string value)
        {
            if (string.IsNullOrEmpty(value)) { return value; }
            if (value.Length <= 4) { return new string('*', value.Length); }
            return value.Substring(0, 2) + "****" + value.Substring(value.Length - 2);
        }

        /// <summary>把 text 中出现的所有指定密钥替换为占位符。</summary>
        public static string Redact(string text, params string[] secrets)
        {
            if (string.IsNullOrEmpty(text)) { return text; }
            string result = text;
            if (secrets != null)
            {
                foreach (string s in secrets)
                {
                    if (!string.IsNullOrEmpty(s) && s.Length >= 3) { result = result.Replace(s, Mask); }
                }
            }
            return result;
        }
    }
}