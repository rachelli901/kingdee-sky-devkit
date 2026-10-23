using System;
using System.Net;

namespace KingdeeSky.WebApiClient
{
    /// <summary>
    /// 金蝶云·星空 WebAPI 对接示例 —— 主程序（交互菜单）。
    /// 运行前请先修改 App.config 中的站点地址、账套 ID、账号密码与基础资料编号。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            // 星空公有云普遍为 HTTPS，显式启用 TLS 1.2（老框架默认可能不含）
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            ServicePointManager.ServerCertificateValidationCallback = (a, b, c, d) => true;

            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "KingdeeSky.DevKit - 金蝶云·星空 WebAPI 示例";

            Settings settings;
            try
            {
                settings = Settings.Load();
                settings.ValidateOrThrow();
            }
            catch (Exception ex)
            {
                Console.WriteLine("配置错误：" + ex.Message);
                Pause();
                return 1;
            }

            Console.WriteLine("============================================================");
            Console.WriteLine("  KingdeeSky.DevKit  金蝶云·星空 WebAPI 对接示例（非官方）");
            Console.WriteLine("  站点：{0}", settings.ServerUrl);
            Console.WriteLine("  账套：{0}    用户：{1}", settings.AcctId, settings.UserName);
            Console.WriteLine("============================================================");

            // 启动即登录一次，验证地址/账套/账号是否可用
            using (var client = new KingdeeWebApiClient(
                settings.ServerUrl, settings.AcctId, settings.UserName, settings.Password, settings.Lcid))
            {
                Console.WriteLine("正在登录验证 ...");
                try
                {
                    var login = client.Login();
                    Console.WriteLine("登录结果：{0}", login);
                    Console.WriteLine("原始返回：{0}", login.RawJson);
                    if (!login.Success)
                    {
                        Console.WriteLine("登录失败，请检查 ServerUrl / AcctId / 账号密码 / 该用户的 WebAPI 授权。");
                        Pause();
                        return 2;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("登录请求异常：" + ex.Message);
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
                        Console.WriteLine("调用异常：" + ex.Message);
                    }
                }
            }

            return 0;
        }

        private static void Pause()
        {
            Console.WriteLine();
            Console.WriteLine("按回车键退出 ...");
            Console.ReadLine();
        }
    }
}
