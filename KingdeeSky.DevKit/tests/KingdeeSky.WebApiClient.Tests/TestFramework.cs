// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace KingdeeSky.WebApiClient.Tests
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestAttribute : Attribute
    {
    }

    public sealed class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }

    /// <summary>零依赖迷你断言：C# 5 兼容，供本测试工程使用（不引入外部测试框架）。</summary>
    public static class Assert
    {
        public static void IsTrue(bool condition, string message = null)
        {
            if (!condition) { throw new AssertionException(message ?? "expected True but was False"); }
        }

        public static void IsFalse(bool condition, string message = null)
        {
            if (condition) { throw new AssertionException(message ?? "expected False but was True"); }
        }

        public static void AreEqual(object expected, object actual, string message = null)
        {
            if (!object.Equals(expected, actual))
            {
                throw new AssertionException((message ?? "values differ") + " expected=<" + expected + "> actual=<" + actual + ">");
            }
        }

        public static void IsNotNull(object value, string message = null)
        {
            if (value == null) { throw new AssertionException(message ?? "expected non-null"); }
        }

        public static void IsNull(object value, string message = null)
        {
            if (value != null) { throw new AssertionException((message ?? "expected null") + " but was <" + value + ">"); }
        }

        public static void Contains(string text, string sub, string message = null)
        {
            if (text == null || !text.Contains(sub))
            {
                throw new AssertionException((message ?? "substring not found") + " sub=<" + sub + "> in text=<" + text + ">");
            }
        }

        public static void NotContains(string text, string sub, string message = null)
        {
            if (text != null && text.Contains(sub))
            {
                throw new AssertionException((message ?? "substring should not appear") + " sub=<" + sub + "> in text=<" + text + ">");
            }
        }

        public static Exception Throws<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T ex) { return ex; }
            catch (Exception ex)
            {
                throw new AssertionException("expected exception " + typeof(T).Name + " but got " + ex.GetType().Name + ": " + ex.Message);
            }
            throw new AssertionException("expected exception " + typeof(T).Name + " but none thrown");
        }

        public static T InstanceOf<T>(object value) where T : class
        {
            T t = value as T;
            if (t == null)
            {
                throw new AssertionException("expected instance of " + typeof(T).Name + " but got " +
                    (value == null ? "null" : value.GetType().Name));
            }
            return t;
        }
    }

    /// <summary>反射发现 [Test] 方法并运行；任一失败返回非 0。</summary>
    public static class TestRunner
    {
        public static int RunAll(params Type[] types)
        {
            return RunAll(types, null, null);
        }

        public static int RunAll(Type[] types, string classNameFilter, string methodNameFilter)
        {
            int passed = 0;
            int failed = 0;
            var failures = new List<string>();
            foreach (Type t in types)
            {
                if (!string.IsNullOrEmpty(classNameFilter) &&
                    !string.Equals(t.Name, classNameFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .Where(m => m.GetCustomAttributes(typeof(TestAttribute), false).Length > 0)
                    .OrderBy(m => m.Name);
                foreach (MethodInfo m in methods)
                {
                    if (!string.IsNullOrEmpty(methodNameFilter) &&
                        !string.Equals(m.Name, methodNameFilter, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(t.Name + "." + m.Name, methodNameFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    object instance = m.IsStatic ? null : Activator.CreateInstance(t);
                    try
                    {
                        m.Invoke(instance, null);
                        passed++;
                        Console.WriteLine("  [PASS] " + t.Name + "." + m.Name);
                    }
                    catch (TargetInvocationException tie)
                    {
                        failed++;
                        Exception inner = tie.InnerException ?? tie;
                        failures.Add(t.Name + "." + m.Name + " -> " + inner.GetType().Name + ": " + inner.Message);
                        Console.WriteLine("  [FAIL] " + t.Name + "." + m.Name + " -> " + inner.Message);
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        failures.Add(t.Name + "." + m.Name + " -> " + ex.GetType().Name + ": " + ex.Message);
                        Console.WriteLine("  [FAIL] " + t.Name + "." + m.Name + " -> " + ex.Message);
                    }
                }
            }
            Console.WriteLine();
            Console.WriteLine("===== SUMMARY: passed=" + passed + " failed=" + failed + " =====");
            foreach (string f in failures) { Console.WriteLine("FAILED: " + f); }
            return failed == 0 ? 0 : 1;
        }
    }
}