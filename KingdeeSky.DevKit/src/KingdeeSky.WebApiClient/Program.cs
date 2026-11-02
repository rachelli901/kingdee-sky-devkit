// Author: rachelli901 <lir30481@gmail.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using KingdeeSky.WebApiClient.Logging;
using KingdeeSky.WebApiClient.Security;

namespace KingdeeSky.WebApiClient
{
    /// <summary>
    /// 金蝶云·星空 WebAPI 对接示例 —— 主程序（交互菜单）。
    /// 站点地址、账套、用户与鉴权方式/密钥来源在 App.config 配置；
    /// 密码等密钥由 ISecretProvider 提供（环境变量/密钥文件/DPAPI/KMS），不写入配置文件或源码；
    /// 日志输出对已登记密钥自动脱敏。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            // 星空公有云普遍为 HTTPS，显式启用 TLS 1.2（老框架默认可能不含）；证书校验默认开启，
            // 仅当显式配置 AllowInvalidCertificate=true（本地调试）时才放行任意证书
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "KingdeeSky.DevKit - 金蝶云·星空 WebAPI 示例";

            var logger = new SafeLogger(LogLevel.Info, null);

            // 自检模式：--selfcheck 输出结构化健康检查（配置/密钥源/鉴权组件/传输层/传输安全），不发起网络请求
            // 附加 --json 输出单行 JSON（含 passed/failed/elapsedMs 指标），便于监控采集
            bool selfCheck = false;
            bool jsonFormat = false;
            if (args != null)
            {
                foreach (string a in args)
                {
                    string t = (a ?? string.Empty).Trim().ToLowerInvariant();
                    if (t == "--selfcheck" || t == "/selfcheck" || t == "-selfcheck") { selfCheck = true; }
                    if (t == "--json" || t == "/json" || t == "-json") { jsonFormat = true; }
                }
            }
            if (selfCheck)
            {
                return Diagnostics.HealthCheck.Run(logger, jsonFormat);
            }

            Settings settings;
            try
            {
                settings = Settings.Load();
                settings.ValidateOrThrow();
            }
            catch (Exception ex)
            {
                Console.WriteLine("配置错误：" + logger.Scrub(ex.Message));
                Pause();
                return 1;
            }

            if (settings.AllowInvalidCertificate)
            {
                // 放宽范围收敛：只对回环地址（本机 mock / 本地调试站点）放行证书错误，其余主机一律按默认策略拒绝；
                // 并把真实证书链错误打印出来，避免「静默通过」掩盖中间人/证书过期等问题。
                // 注意：ServerCertificateValidationCallback 是进程级静态设置，一旦注册本轮进程内所有 HTTPS 请求都会走它，
                // 因此这里的回环判定必须保留——它让非本机请求重新落到「拒绝」分支上。
                logger.Warn("AllowInvalidCertificate=true：仅对回环地址（localhost / 127.0.0.1 / ::1）放行证书错误，其余主机仍按默认策略校验；生产环境必须保持 false。");
                ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    if (sslPolicyErrors == SslPolicyErrors.None) { return true; }
                    string host = TryGetRequestHost(sender);
                    string detail = DescribeCertificateErrors(chain, sslPolicyErrors);
                    if (!IsLoopbackHost(host))
                    {
                        logger.Error(string.Format("服务器证书校验失败（{0}）：{1}；已按默认策略拒绝该连接。", host ?? "未知主机", detail));
                        return false;
                    }
                    logger.Error(string.Format("已放行回环地址 {0} 的证书错误（AllowInvalidCertificate=true）：{1}", host, detail));
                    return true;
                };
            }

            ISecretProvider secrets;
            IAuthProvider authProvider;
            try
            {
                secrets = settings.CreateSecretProvider();
                authProvider = AuthProviderFactory.Create(settings.AuthMode, settings.AppSecSigningTemplate, settings.AppSecTimestampFormat);
            }
            catch (Exception ex)
            {
                Console.WriteLine("初始化鉴权/密钥来源失败：" + logger.Scrub(ex.Message));
                Pause();
                return 1;
            }
            RegisterSecrets(logger, settings, secrets);
            AuthContext authContext = settings.CreateAuthContext(secrets);

            Console.WriteLine("============================================================");
            Console.WriteLine("  KingdeeSky.DevKit  金蝶云·星空 WebAPI 对接示例（非官方）");
            Console.WriteLine("  站点：{0}", settings.ServerUrl);
            Console.WriteLine("  账套：{0}    用户：{1}", settings.AcctId, settings.UserName);
            Console.WriteLine("  鉴权：{0}    密钥来源：{1}", authProvider.Name, secrets.Name);
            Console.WriteLine("============================================================");

            // 启动即登录一次，验证地址/账套/账号是否可用（最后一个参数传入 AllowInsecureHttp，默认 false=强制 HTTPS）
            using (var client = new KingdeeWebApiClient(settings.ServerUrl, settings.AcctId, authProvider, authContext, logger, settings.AllowInsecureHttp))
            {
                Console.WriteLine("正在登录验证 ...");
                try
                {
                    var login = client.Login();
                    Console.WriteLine("登录结果：{0}", login);
                    Console.WriteLine("原始返回：{0}", logger.Scrub(login.RawJson));
                    if (!login.Success)
                    {
                        Console.WriteLine("登录失败，请检查 ServerUrl / AcctId / 账号密码 / 该用户的 WebAPI 授权。");
                        Pause();
                        return 2;
                    }
                }
                catch (SecretNotFoundException ex)
                {
                    Console.WriteLine("密钥缺失：" + logger.Scrub(ex.Message));
                    Pause();
                    return 4;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("登录请求异常：" + logger.Scrub(ex.Message));
                    Pause();
                    return 3;
                }

                bool running = true;
                while (running)
                {
                    Console.WriteLine();
                    Console.WriteLine("---------------- 功能菜单 ----------------");
                    Console.WriteLine("  1 = 查询最近 10 张采购订单");
                    Console.WriteLine("  2 = 新建一张采购订单（保存为草稿）");
                    Console.WriteLine("  3 = 对刚创建的单据执行 提交 + 审核");
                    Console.WriteLine("  4 = 一键全流程（查询 → 建单 → 提交 → 审核 → 查看）");
                    Console.WriteLine("  5 = 新建自定义单据：差旅费报销单 SKY_ExpReimburse");
                    Console.WriteLine("  0 = 退出");
                    Console.Write("请选择：");

                    string choice = Console.ReadLine();
                    long? lastId = null;
                    try
                    {
                        switch (choice)
                        {
                            case "1":
                                Scenarios.QueryRecentPurchaseOrders(client);
                                break;
                            case "2":
                                lastId = Scenarios.CreatePurchaseOrder(client, settings);
                                if (lastId.HasValue) { Console.WriteLine("（请记下内码 {0}，可在菜单 3 中手工改代码审核）", lastId); }
                                break;
                            case "3":
                                Console.Write("请输入要提交并审核的单据内码：");
                                long id;
                                if (long.TryParse(Console.ReadLine(), out id) && id > 0)
                                {
                                    Scenarios.SubmitAndAudit(client, id);
                                }
                                else
                                {
                                    Console.WriteLine("内码无效。");
                                }
                                break;
                            case "4":
                                Scenarios.RunFullFlow(client, settings);
                                break;
                            case "5":
                                Scenarios.CreateExpReimburse(client, settings);
                                break;
                            case "0":
                                running = false;
                                break;
                            default:
                                Console.WriteLine("无效选项。");
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("调用异常：" + logger.Scrub(ex.Message));
                    }
                }
            }

            return 0;
        }

        /// <summary>把当前鉴权方式实际用到的密钥登记到日志脱敏器（仅内存副本，不落盘）。</summary>
        private static void RegisterSecrets(SafeLogger logger, Settings settings, ISecretProvider secrets)
        {
            string mode = (settings.AuthMode ?? "password").Trim().ToLowerInvariant();
            if (mode == "password")
            {
                RegisterIfPresent(logger, secrets, settings.PasswordKey);
            }
            else if (mode == "oauth" || mode == "oauth2")
            {
                RegisterIfPresent(logger, secrets, settings.ClientIdKey);
                RegisterIfPresent(logger, secrets, settings.ClientSecretKey);
            }
            else if (mode == "appsec" || mode == "appid_appsec")
            {
                RegisterIfPresent(logger, secrets, settings.AppIdKey);
                RegisterIfPresent(logger, secrets, settings.AppSecretKey);
            }
            // KMS 令牌可能在密钥源初始化时被读取，一并登记以便脱敏
            RegisterIfPresent(logger, secrets, settings.KmsTokenKey);
        }

        private static void RegisterIfPresent(SafeLogger logger, ISecretProvider secrets, string key)
        {
            if (string.IsNullOrEmpty(key)) { return; }
            string value;
            if (secrets.TryGet(key, out value) && !string.IsNullOrEmpty(value)) { logger.RegisterSecret(value); }
        }

        /// <summary>取发起 TLS 握手的主机名（HttpWebRequest 优先，其余 WebRequest 退化到 RequestUri）。</summary>
        private static string TryGetRequestHost(object sender)
        {
            var request = sender as HttpWebRequest;
            if (request != null && request.Address != null) { return request.Address.Host; }
            var generic = sender as WebRequest;
            if (generic != null && generic.RequestUri != null) { return generic.RequestUri.Host; }
            return null;
        }

        /// <summary>是否回环地址（localhost / 127.0.0.0/8 / ::1）。</summary>
        private static bool IsLoopbackHost(string host)
        {
            if (string.IsNullOrEmpty(host)) { return false; }
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) { return true; }
            IPAddress address;
            if (IPAddress.TryParse(host, out address)) { return IPAddress.IsLoopback(address); }
            return false;
        }

        /// <summary>把证书链里的真实错误汇总为一行可读文本（含每个链元素的错误状态）。</summary>
        private static string DescribeCertificateErrors(X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("SslPolicyErrors=").Append(sslPolicyErrors);
            if (chain != null)
            {
                foreach (X509ChainElement element in chain.ChainElements)
                {
                    foreach (X509ChainStatus status in element.ChainElementStatus)
                    {
                        if (status.Status == X509ChainStatusFlags.NoError) { continue; }
                        sb.Append("；").Append(element.Certificate != null ? element.Certificate.Subject : "(未知证书)")
                          .Append(" → ").Append(status.Status).Append("：").Append(status.StatusInformation);
                    }
                }
            }
            return sb.ToString();
        }

        private static void Pause()
        {
            Console.WriteLine();
            Console.WriteLine("按回车键退出 ...");
            Console.ReadLine();
        }
    }
}
