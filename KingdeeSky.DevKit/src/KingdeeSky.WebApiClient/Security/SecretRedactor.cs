// Author: rachelli901 <lir30481@gmail.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using System.Text;

namespace KingdeeSky.WebApiClient.Security
{
    /// <summary>文本脱敏：把密钥从日志/异常信息中替换为固定占位，避免明文外泄。</summary>
    public static class SecretRedactor
    {
        public const string Mask = "***REDACTED***";

        /// <summary>全局子串替换的最小密钥长度：低于该长度时不做整篇替换，避免误伤日志中随处可见的短串。</summary>
        public const int MinSubstringLength = 4;

        /// <summary>整词掩码的最小密钥长度：短密钥只要以独立词出现（两侧为非单词字符）仍会被掩码，避免极弱口令原文泄露。</summary>
        public const int MinTokenLength = 1;

        /// <summary>生成短掩码用于展示（首尾各留 2 个字符；不足则整串打码）。</summary>
        public static string MaskShort(string value)
        {
            if (string.IsNullOrEmpty(value)) { return value; }
            if (value.Length <= 4) { return new string('*', value.Length); }
            return value.Substring(0, 2) + "****" + value.Substring(value.Length - 2);
        }

        /// <summary>
        /// 把 text 中出现的所有指定密钥替换为占位符。
        /// 较长密钥（≥ <see cref="MinSubstringLength"/>）做全局子串替换；
        /// 较短密钥（≥ <see cref="MinTokenLength"/>）只做整词替换：既能掩掉以独立词出现的极弱口令，
        /// 又不会把日志中嵌在普通单词里的短串误伤成占位符。
        /// 空值（null / 空串）不做任何处理。
        /// </summary>
        public static string Redact(string text, params string[] secrets)
        {
            if (string.IsNullOrEmpty(text)) { return text; }
            string result = text;
            if (secrets == null) { return result; }
            foreach (string s in secrets)
            {
                if (string.IsNullOrEmpty(s)) { continue; }
                if (s.Length >= MinSubstringLength)
                {
                    result = result.Replace(s, Mask);
                }
                else if (s.Length >= MinTokenLength)
                {
                    result = RedactWholeWord(result, s);
                }
            }
            return result;
        }

        /// <summary>仅替换「两侧均为单词边界」出现的 token（用于不宜做全局子串替换的短密钥）。</summary>
        private static string RedactWholeWord(string text, string token)
        {
            var sb = new StringBuilder(text.Length);
            int i = 0;
            while (true)
            {
                int hit = text.IndexOf(token, i, StringComparison.Ordinal);
                if (hit < 0) { sb.Append(text, i, text.Length - i); break; }
                int after = hit + token.Length;
                bool startsAtBoundary = hit == 0 || !IsWordChar(text[hit - 1]);
                bool endsAtBoundary = after >= text.Length || !IsWordChar(text[after]);
                sb.Append(text, i, hit - i);
                if (startsAtBoundary && endsAtBoundary)
                {
                    sb.Append(Mask);
                }
                else
                {
                    // 不是独立单词：原样保留该次出现，从其末尾继续向后匹配
                    sb.Append(text, hit, token.Length);
                }
                i = after;
            }
            return sb.ToString();
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }
    }
}