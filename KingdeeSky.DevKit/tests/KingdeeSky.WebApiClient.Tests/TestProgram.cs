// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using System.Linq;
using System.Reflection;

namespace KingdeeSky.WebApiClient.Tests
{
    /// <summary>
    /// 测试入口：无参运行全部 [Test] 方法（门禁）;
    /// --class &lt;TypeName&gt; 按测试类（分组）运行;
    /// --method &lt;Type.Method&gt; 或 &lt;Method&gt; 按单个用例运行;
    /// --list 列出全部已发现的 [Test] 方法。
    /// 退出码：0 = 全部通过，1 = 有失败（门禁拦截），2 = 参数错误。
    /// </summary>
    public static class Program
    {
        private static readonly Type[] TestTypes = new Type[]
        {
            typeof(AuthProviderTests),
            typeof(SecretProviderTests),
            typeof(ConfigTests),
            typeof(ClientTests),
            typeof(TransportSecurityTests)
        };

        public static int Main(string[] args)
        {
            Console.WriteLine("KingdeeSky.WebApiClient test suite (zero external deps)");
            Console.WriteLine("--------------------------------------------------------");
            string className = null;
            string methodName = null;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string a = (args[i] ?? string.Empty).Trim().ToLowerInvariant();
                    if (a == "--class" || a == "/class" || a == "-class" || a == "--group" || a == "/group")
                    {
                        if (i + 1 >= args.Length || string.IsNullOrEmpty(args[i + 1]))
                        {
                            Console.WriteLine("Missing value for " + args[i]);
                            Usage();
                            return 2;
                        }
                        className = args[i + 1].Trim();
                        i++;
                    }
                    else if (a == "--method" || a == "/method" || a == "-method" || a == "--case" || a == "/case")
                    {
                        if (i + 1 >= args.Length || string.IsNullOrEmpty(args[i + 1]))
                        {
                            Console.WriteLine("Missing value for " + args[i]);
                            Usage();
                            return 2;
                        }
                        methodName = args[i + 1].Trim();
                        i++;
                    }
                    else if (a == "--list" || a == "/list")
                    {
                        ListTests();
                        return 0;
                    }
                    else if (a == "--help" || a == "/?" || a == "-h" || a == "/help")
                    {
                        Usage();
                        return 0;
                    }
                    else
                    {
                        Console.WriteLine("Unknown argument: " + args[i]);
                        Usage();
                        return 2;
                    }
                }
            }

            if (!string.IsNullOrEmpty(className) &&
                !TestTypes.Any(t => string.Equals(t.Name, className, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("Unknown test class: " + className);
                Usage();
                return 2;
            }

            if (!string.IsNullOrEmpty(methodName) && !MethodExists(methodName))
            {
                Console.WriteLine("Unknown test method: " + methodName);
                Usage();
                return 2;
            }

            int code = TestRunner.RunAll(TestTypes, className, methodName);
            Console.WriteLine();
            Console.WriteLine(code == 0 ? "ALL TESTS PASSED" : "TESTS FAILED (gate blocked)");
            return code;
        }

        private static bool MethodExists(string methodName)
        {
            foreach (Type t in TestTypes)
            {
                if (t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .Any(m => m.GetCustomAttributes(typeof(TestAttribute), false).Length > 0 &&
                        (string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(t.Name + "." + m.Name, methodName, StringComparison.OrdinalIgnoreCase))))
                {
                    return true;
                }
            }
            return false;
        }

        private static void ListTests()
        {
            foreach (Type t in TestTypes)
            {
                var methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .Where(m => m.GetCustomAttributes(typeof(TestAttribute), false).Length > 0)
                    .OrderBy(m => m.Name);
                foreach (MethodInfo m in methods)
                {
                    Console.WriteLine(t.Name + "." + m.Name);
                }
            }
        }

        private static void Usage()
        {
            Console.WriteLine("Usage: KingdeeSky.WebApiClient.Tests.exe [options]");
            Console.WriteLine("  (no args)           run all tests (full gate, exit 0/1)");
            Console.WriteLine("  --class <TypeName>  run only one test class / group (e.g. AuthProviderTests)");
            Console.WriteLine("  --method <Filter>   run a single test method (Type.Method or Method)");
            Console.WriteLine("  --list              list discovered [Test] methods");
            Console.WriteLine("  --help              show this help");
            Console.WriteLine("Exit codes: 0 = all passed, 1 = at least one failed, 2 = invalid arguments");
        }
    }
}