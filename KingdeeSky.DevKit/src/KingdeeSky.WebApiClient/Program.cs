// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using System.Net;
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
                logger.Warn("AllowInvalidCertificate=true 已放行任意服务器证书，仅限本地调试；生产环境必须保持 false 并使用有效证书。");
                ServicePointManager.ServerCertificateValidationCallback = (a, b, c, d) => true;
            }

            ISecretProvider secrets;
            IAuthProvider authProvider;
            try
            {
                secrets = settings.CreateSecretProvider();
                authProvider = AuthProviderFactory.Create(settings.AuthMode, settings.AppSecSigningTemplate);
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

        private static void Pause()
        {
            Console.WriteLine();
            Console.WriteLine("按回车键退出 ...");
            Console.ReadLine();
        }
    }
}
