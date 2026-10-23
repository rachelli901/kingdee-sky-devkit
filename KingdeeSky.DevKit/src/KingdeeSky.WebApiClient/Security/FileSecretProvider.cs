// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using System.Collections.Generic;
using System.IO;

namespace KingdeeSky.WebApiClient.Security
{
    /// <summary>
    /// 从本地明文密钥文件（每行 key=value，# 开头为注释）读取，等价于 .NET User Secrets 的开发用法。
    /// 仅用于开发/调试：生产请改用 DPAPI 或 KMS。该文件必须位于仓库之外并加入忽略规则。
    /// </summary>
    public sealed class FileSecretProvider : ISecretProvider
    {
        private readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string _path;

        public FileSecretProvider(string path)
        {
            _path = path;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) { continue; }
                    int eq = line.IndexOf('=');
                    if (eq <= 0) { continue; }
                    _map[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
        }

        public string Name { get { return "file(" + _path + ")"; } }

        public bool TryGet(string key, out string value)
        {
            return _map.TryGetValue(key, out value) && !string.IsNullOrEmpty(value);
        }
    }
}