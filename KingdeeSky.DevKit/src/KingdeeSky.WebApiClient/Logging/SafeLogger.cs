using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KingdeeSky.WebApiClient.Security;

namespace KingdeeSky.WebApiClient.Logging
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3
    }

    /// <summary>
    /// 最小化结构化日志：控制台 + 可选文件；对已登记密钥自动脱敏，避免明文写入日志。
    /// </summary>
    public sealed class SafeLogger
    {
        private readonly List<string> _secrets = new List<string>();
        private readonly string _logFile;
        private readonly LogLevel _minLevel;
        private readonly object _gate = new object();

        public SafeLogger(LogLevel minLevel, string logFile)
        {
            _minLevel = minLevel;
            _logFile = logFile;
        }

        /// <summary>登记需要脱敏的密钥（值不落文件，仅在内存用于替换）。</summary>
        public void RegisterSecret(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 3) { return; }
            lock (_gate)
            {
                if (!_secrets.Contains(value)) { _secrets.Add(value); }
            }
        }

        /// <summary>对文本按已登记密钥脱敏。</summary>
        public string Scrub(string text)
        {
            lock (_gate)
            {
                return SecretRedactor.Redact(text, _secrets.ToArray());
            }
        }

        public void Debug(string message) { Write(LogLevel.Debug, message); }
        public void Info(string message) { Write(LogLevel.Info, message); }
        public void Warn(string message) { Write(LogLevel.Warn, message); }
        public void Error(string message) { Write(LogLevel.Error, message); }

        public void Error(string message, Exception ex)
        {
            Write(LogLevel.Error, message + " | " + (ex == null ? string.Empty : ex.GetType().Name + ": " + ex.Message));
        }

        private void Write(LogLevel level, string message)
        {
            if (level < _minLevel) { return; }
            string safe = Scrub(message);
            string line = string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] {2}",
                DateTime.Now, level.ToString().ToUpperInvariant(), safe);
            lock (_gate)
            {
                Console.WriteLine(line);
                if (!string.IsNullOrEmpty(_logFile))
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(_logFile);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) { Directory.CreateDirectory(dir); }
                        File.AppendAllText(_logFile, line + Environment.NewLine, new UTF8Encoding(false));
                    }
                    catch
                    {
                        // 日志落盘失败不影响主流程
                    }
                }
            }
        }
    }
}