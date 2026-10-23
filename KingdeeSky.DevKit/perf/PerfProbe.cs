// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

// PerfProbe: 客户端侧性能冒烟探针，与 KingdeeSky.WebApiClient 源码一起编译（零金蝶 DLL 依赖）。
// 场景：登录 1 次 + ExecuteBillQuery 循环 N 次，统计 min/avg/p95/max 延迟与失败数。
// 用法：PerfProbe.exe [-url <serverUrl>] [-iterations <N>] [-thresholdMs <N>]
// 输出 key=value 指标；退出码：0=PASS, 1=查询失败, 2=登录失败, 3=超过 p95 阈值。
// 密钥经环境变量 KINGDEE_PASSWORD 注入（由 run-perf.ps1 负责），输出自动脱敏。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using KingdeeSky.WebApiClient;
using KingdeeSky.WebApiClient.Logging;
using KingdeeSky.WebApiClient.Security;

namespace KingdeeSky.Perf
{
    internal static class PerfProbe
    {
        private const string DefaultUrl = "http://127.0.0.1:18081/K3Cloud/";

        private static int Main(string[] args)
        {
            string serverUrl = DefaultUrl;
            int iterations = 20;
            int thresholdMs = 5000;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "-url" && i + 1 < args.Length) { serverUrl = args[++i]; }
                else if (a == "-iterations" && i + 1 < args.Length) { iterations = ParseInt(args[++i], iterations); }
                else if (a == "-thresholdMs" && i + 1 < args.Length) { thresholdMs = ParseInt(args[++i], thresholdMs); }
            }
            if (iterations < 1) { iterations = 1; }
            if (thresholdMs < 1) { thresholdMs = 5000; }

            SafeLogger logger = new SafeLogger(LogLevel.Warn, null);
            logger.RegisterSecret("demo123");

            AuthContext context = new AuthContext();
            context.AcctId = "PERFACCT";
            context.UserName = "Administrator";
            context.Lcid = 2052;
            context.Secrets = new EnvironmentSecretProvider();

            try
            {
                using (KingdeeWebApiClient client = new KingdeeWebApiClient(
                    serverUrl, "PERFACCT", new PasswordAuthProvider(), context, logger, true))
                {
                    Stopwatch loginSw = Stopwatch.StartNew();
                    LoginResult login = client.Login();
                    loginSw.Stop();
                    if (login == null || !login.Success)
                    {
                        Console.WriteLine("perf.login = fail");
                        Console.WriteLine("perf.login-message = " + (login == null ? "null" : logger.Scrub(login.Message)));
                        Console.WriteLine("perf.result = FAIL");
                        return 2;
                    }

                    Console.WriteLine("perf.login = ok");
                    Console.WriteLine("perf.login-ms = " + loginSw.ElapsedMilliseconds);
                    Console.WriteLine("perf.server-url = " + serverUrl);
                    Console.WriteLine("perf.iterations = " + iterations);

                    List<double> times = new List<double>(iterations);
                    int failures = 0;
                    int totalRows = 0;
                    for (int i = 0; i < iterations; i++)
                    {
                        Stopwatch sw = Stopwatch.StartNew();
                        try
                        {
                            object[][] rows = client.ExecuteBillQuery(
                                "PUR_PurchaseOrder", "FBillNo,FDate,FDocumentStatus", "", "", 10, 0, 10);
                            sw.Stop();
                            times.Add(sw.Elapsed.TotalMilliseconds);
                            totalRows += rows == null ? 0 : rows.Length;
                        }
                        catch (Exception ex)
                        {
                            sw.Stop();
                            failures++;
                            Console.WriteLine("perf.error[" + i + "] = " + logger.Scrub(ex.Message));
                        }
                    }

                    Console.WriteLine("perf.success = " + (iterations - failures));
                    Console.WriteLine("perf.failed = " + failures);
                    Console.WriteLine("perf.min-ms = " + Format(Min(times)));
                    Console.WriteLine("perf.avg-ms = " + Format(Avg(times)));
                    Console.WriteLine("perf.p95-ms = " + Format(P95(times)));
                    Console.WriteLine("perf.max-ms = " + Format(Max(times)));
                    Console.WriteLine("perf.avg-rows = " + Format((double)totalRows / iterations));
                    Console.WriteLine("perf.threshold-ms = " + thresholdMs);

                    if (failures > 0)
                    {
                        Console.WriteLine("perf.result = FAIL");
                        return 1;
                    }
                    if (P95(times) > thresholdMs)
                    {
                        Console.WriteLine("perf.result = FAIL_THRESHOLD");
                        return 3;
                    }
                    Console.WriteLine("perf.result = PASS");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("perf.result = FAIL");
                Console.WriteLine("perf.fatal = " + logger.Scrub(ex.Message));
                return 1;
            }
        }

        private static int ParseInt(string s, int def)
        {
            int v;
            return int.TryParse(s, out v) ? v : def;
        }

        private static double Min(List<double> xs)
        {
            if (xs.Count == 0) { return 0; }
            double m = xs[0];
            for (int i = 1; i < xs.Count; i++) { if (xs[i] < m) { m = xs[i]; } }
            return m;
        }

        private static double Max(List<double> xs)
        {
            if (xs.Count == 0) { return 0; }
            double m = xs[0];
            for (int i = 1; i < xs.Count; i++) { if (xs[i] > m) { m = xs[i]; } }
            return m;
        }

        private static double Avg(List<double> xs)
        {
            if (xs.Count == 0) { return 0; }
            double s = 0;
            for (int i = 0; i < xs.Count; i++) { s += xs[i]; }
            return s / xs.Count;
        }

        private static double P95(List<double> xs)
        {
            if (xs.Count == 0) { return 0; }
            List<double> sorted = new List<double>(xs);
            sorted.Sort();
            int idx = (int)Math.Ceiling(0.95 * sorted.Count) - 1;
            if (idx < 0) { idx = 0; }
            if (idx >= sorted.Count) { idx = sorted.Count - 1; }
            return sorted[idx];
        }

        private static string Format(double v)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}