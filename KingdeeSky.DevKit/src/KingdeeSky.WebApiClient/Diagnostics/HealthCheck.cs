using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using KingdeeSky.WebApiClient.Logging;
using KingdeeSky.WebApiClient.Security;

namespace KingdeeSky.WebApiClient.Diagnostics
{
    /// <summary>
    /// 健康/自检：以结构化 key=value 输出配置、密钥源、鉴权组件、传输层与传输安全策略的检查结果。
    /// 默认输出人类可读文本；--json 时输出单行 JSON（含 passed/failed/elapsedMs 指标），便于监控采集。
    /// 只做本地检查、不发起真实网络请求；所有输出经 SafeLogger 脱敏，避免密钥值进入日志。
    /// 用法：KingdeeSky.WebApiClient.exe --selfcheck [--json]
    /// 返回码：0 = 全部通过；1 = 存在失败项（配置不完整或组件无法构造）。
    /// </summary>
    internal static class HealthCheck
    {
        private static int _failed;
        private static int _passed;
        private static bool _json;
        private static List<string> _jsonRows;

        public static int Run(SafeLogger logger)
        {
            return Run(logger, false);
        }

        public static int Run(SafeLogger logger, bool jsonFormat)
        {
            _json = jsonFormat;
            Settings settings;
            try
            {
                settings = Settings.Load();
            }
            catch (Exception ex)
            {
                if (_json)
                {
                    Console.WriteLine("{\"passed\":0,\"failed\":1,\"elapsedMs\":0,\"checks\":{\"配置读取\":{\"ok\":false,\"detail\":\"" + JsonEscape(logger.Scrub(ex.Message)) + "\"}}}");
                }
                else
                {
                    Console.WriteLine("============================================================");
                    Console.WriteLine("  自检结果：[FAIL] 配置读取失败：" + logger.Scrub(ex.Message));
                    Console.WriteLine("============================================================");
                }
                return 1;
            }
            return Run(settings, logger, jsonFormat);
        }

        public static int Run(Settings settings, SafeLogger logger, bool jsonFormat = false)
        {
            _json = jsonFormat;
            _failed = 0;
            _passed = 0;
            _jsonRows = new List<string>();
            Stopwatch sw = Stopwatch.StartNew();

            if (!_json)
            {
                Console.WriteLine("============================================================");
                Console.WriteLine("  KingdeeSky.WebApiClient 自检（SelfCheck）");
                Console.WriteLine("  仅本地检查，不发起网络请求；密钥值不输出、已登记项自动脱敏");
                Console.WriteLine("============================================================");
            }

            // ---- 版本与运行环境 ----
            RecordLine(logger, "product", "KingdeeSky.WebApiClient");
            RecordLine(logger, "deploy-env", settings.DeployEnv);
            RecordLine(logger, "deploy-env-source", settings.DeployEnvSource);
            RecordLine(logger, "clr-version", Environment.Version.ToString());
            string assemblyVersion = "未知";
            try
            {
                AssemblyName an = Assembly.GetExecutingAssembly().GetName();
                if (an != null && an.Version != null) { assemblyVersion = an.Version.ToString(); }
            }
            catch { /* 版本获取失败不阻塞自检 */ }
            RecordLine(logger, "assembly-version", assemblyVersion);

            // ---- 1. 配置检查 ----
            Section("配置检查");
            Uri uri;
            bool urlOk = Uri.TryCreate(settings.ServerUrl, UriKind.Absolute, out uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            RecordCheck(logger, "ServerUrl 合法", urlOk, settings.ServerUrl);

            bool acctOk = !string.IsNullOrWhiteSpace(settings.AcctId) && !settings.AcctId.Contains("请填写");
            RecordCheck(logger, "AcctId 已配置", acctOk, acctOk ? "(已配置)" : (settings.AcctId ?? string.Empty));

            bool userOk = !string.IsNullOrWhiteSpace(settings.UserName);
            RecordCheck(logger, "UserName 已配置", userOk, settings.UserName);

            string authMode = (settings.AuthMode ?? "password").Trim().ToLowerInvariant();
            bool authModeOk = authMode == "password" || authMode == "oauth" || authMode == "oauth2"
                || authMode == "appsec" || authMode == "appid_appsec";
            RecordCheck(logger, "AuthMode 支持", authModeOk, settings.AuthMode);

            string store = (settings.SecretStore ?? "env").Trim().ToLowerInvariant();
            bool storeOk = store == "env" || store == "file" || store == "dpapi" || store == "kms";
            RecordCheck(logger, "SecretStore 支持", storeOk, settings.SecretStore);

            // ---- 2. 密钥源检查 ----
            Section("密钥源检查");
            ISecretProvider secrets = null;
            try
            {
                secrets = settings.CreateSecretProvider();
                RecordCheck(logger, "密钥源可构造", true, secrets.Name);
            }
            catch (Exception ex)
            {
                RecordFail("密钥源可构造", ex, logger);
            }

            if (secrets != null)
            {
                string[] required = RequiredKeys(settings);
                if (required != null && required.Length > 0)
                {
                    if (!_json)
                    {
                        Console.WriteLine("  当前 {0} 模式所需密钥：{1}", authMode, string.Join(", ", required));
                    }
                    foreach (string key in required)
                    {
                        RecordKeyCheck(secrets, key);
                    }
                }
            }

            // ---- 3. 鉴权组件检查 ----
            Section("鉴权组件检查");
            try
            {
                IAuthProvider provider = AuthProviderFactory.Create(settings.AuthMode);
                RecordCheck(logger, "鉴权组件可构造", true, provider.Name);
            }
            catch (Exception ex)
            {
                RecordFail("鉴权组件可构造", ex, logger);
            }

            // ---- 4. 传输层检查（格式级，不发起请求） ----
            Section("传输层检查");
            if (urlOk)
            {
                const string loginPath = "Kingdee.BOS.WebApi.ServicesStub.AuthService.ValidateUser.common.kdsvc";
                string detail = settings.ServerUrl.TrimEnd('/') + "/" + loginPath;
                if (_json)
                {
                    RecordLine(logger, "login-endpoint", detail);
                }
                else
                {
                    Console.WriteLine("  [ OK ] 登录端点格式：{0}（未发起请求，连通性请在主程序验证）", logger.Scrub(detail));
                }
            }
            else
            {
                RecordCheck(logger, "登录端点", false, "ServerUrl 不合法，无法拼装登录端点");
            }

            // ---- 5. 传输安全策略（https 放行；http 仅在显式允许时 WARN 放行，否则 FAIL） ----
            Section("传输安全策略");
            string scheme = string.Empty;
            if (urlOk && uri != null) { scheme = uri.Scheme.ToLowerInvariant(); }
            if (scheme == "https")
            {
                RecordCheck(logger, "传输协议 HTTPS", true, settings.ServerUrl);
            }
            else if (scheme == "http")
            {
                if (settings.AllowInsecureHttp)
                {
                    // WARN 不计失败：允许放行但标记 warn，便于监控据此告警
                    _passed++;
                    if (_json)
                    {
                        _jsonRows.Add("\"传输协议 HTTP\":{\"ok\":false,\"warn\":true,\"detail\":\"" + JsonEscape(logger.Scrub(settings.ServerUrl)) + "\"}");
                    }
                    else
                    {
                        Console.WriteLine("  [WARN] 传输协议 HTTP（明文）：{0}（AllowInsecureHttp=true 已显式放行；生产环境必须关闭并改用 HTTPS）",
                            logger.Scrub(settings.ServerUrl));
                    }
                }
                else
                {
                    RecordCheck(logger, "传输协议 HTTP（明文）", false,
                        settings.ServerUrl + "（默认拒绝：请改用 https，或本地开发显式配置 AllowInsecureHttp=true）");
                }
            }
            else
            {
                RecordCheck(logger, "传输协议识别", false, settings.ServerUrl + "（ServerUrl 不合法）");
            }
            RecordLine(logger, "AllowInsecureHttp", settings.AllowInsecureHttp ? "true" : "false");
            RecordLine(logger, "AllowInvalidCertificate", settings.AllowInvalidCertificate ? "true" : "false");
            RecordLine(logger, "AppSecSigningTemplate", settings.AppSecSigningTemplate ?? string.Empty);

            sw.Stop();

            if (_json)
            {
                Console.WriteLine("{\"passed\":" + _passed
                    + ",\"failed\":" + _failed
                    + ",\"elapsedMs\":" + sw.ElapsedMilliseconds
                    + ",\"checks\":{" + string.Join(",", _jsonRows.ToArray()) + "}}");
            }
            else
            {
                Console.WriteLine("============================================================");
                Console.WriteLine("  自检结果：{0}", _failed == 0 ? "全部通过 (0)" : (_failed + " 项失败 (1)"));
                Console.WriteLine("  提示：自检不发起网络请求；连通性与登录请运行主程序验证。");
            }
            return _failed == 0 ? 0 : 1;
        }

        /// <summary>当前鉴权方式实际需要的密钥键；KMS 场景追加访问令牌键。</summary>
        private static string[] RequiredKeys(Settings settings)
        {
            string mode = (settings.AuthMode ?? "password").Trim().ToLowerInvariant();
            var keys = new List<string>();
            if (mode == "oauth" || mode == "oauth2")
            {
                AddIf(keys, settings.ClientIdKey);
                AddIf(keys, settings.ClientSecretKey);
            }
            else if (mode == "appsec" || mode == "appid_appsec")
            {
                AddIf(keys, settings.AppIdKey);
                AddIf(keys, settings.AppSecretKey);
            }
            else
            {
                AddIf(keys, settings.PasswordKey);
            }
            string store = (settings.SecretStore ?? "env").Trim().ToLowerInvariant();
            if (store == "kms") { AddIf(keys, settings.KmsTokenKey); }
            return keys.ToArray();
        }

        private static void AddIf(List<string> list, string key)
        {
            if (!string.IsNullOrWhiteSpace(key)) { list.Add(key); }
        }

        private static void RecordKeyCheck(ISecretProvider secrets, string key)
        {
            string value;
            bool present = secrets.TryGet(key, out value) && !string.IsNullOrWhiteSpace(value);
            string name = "密钥 " + key;
            string detail = present ? string.Empty : "（未提供，请按 README 注入到当前密钥源）";
            if (present) { _passed++; } else { _failed++; }
            if (_json)
            {
                _jsonRows.Add("\"" + name + "\":{\"ok\":" + (present ? "true" : "false")
                    + ",\"detail\":\"" + JsonEscape(detail) + "\"}");
            }
            else
            {
                Console.WriteLine("  [{0}] {1}{2}", present ? " OK " : "FAIL", key, detail);
            }
        }

        private static void RecordCheck(SafeLogger logger, string name, bool ok, string detail)
        {
            if (ok) { _passed++; } else { _failed++; }
            string safe = logger.Scrub(detail ?? string.Empty);
            if (_json)
            {
                _jsonRows.Add("\"" + name + "\":{\"ok\":" + (ok ? "true" : "false")
                    + ",\"detail\":\"" + JsonEscape(safe) + "\"}");
            }
            else
            {
                Console.WriteLine("  [{0}] {1}：{2}", ok ? " OK " : "FAIL", name, safe);
            }
        }

        private static void RecordFail(string name, Exception ex, SafeLogger logger)
        {
            _failed++;
            string safe = logger.Scrub(ex == null ? string.Empty : ex.Message);
            if (_json)
            {
                _jsonRows.Add("\"" + name + "\":{\"ok\":false,\"detail\":\"" + JsonEscape(safe) + "\"}");
            }
            else
            {
                Console.WriteLine("  [FAIL] {0}：{1}", name, safe);
            }
        }

        private static void Section(string title)
        {
            if (!_json) { Console.WriteLine("-- " + title + " --"); }
        }

        private static void RecordLine(SafeLogger logger, string key, string value)
        {
            string safe = logger.Scrub(value ?? string.Empty);
            if (_json)
            {
                _jsonRows.Add("\"" + key + "\":\"" + JsonEscape(safe) + "\"");
            }
            else
            {
                Console.WriteLine("  {0} = {1}", key, safe);
            }
        }

        private static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) { return string.Empty; }
            var sb = new StringBuilder(s.Length + 16);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) { sb.Append("\\u").Append(((int)c).ToString("x4")); }
                        else { sb.Append(c); }
                        break;
                }
            }
            return sb.ToString();
        }
    }
}