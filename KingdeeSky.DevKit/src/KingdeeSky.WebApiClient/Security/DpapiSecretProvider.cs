using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace KingdeeSky.WebApiClient.Security
{
    /// <summary>
    /// 读取 Windows DPAPI 加密的密钥文件（生产环境推荐：密文绑定机器或用户，明文不落盘）。
    /// 文件由 <see cref="DpapiSecretStore.Protect"/> 生成，内容为 key=value 行的 DPAPI 密文。
    /// </summary>
    public sealed class DpapiSecretProvider : ISecretProvider
    {
        private readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string _path;
        private readonly DataProtectionScope _scope;
        private readonly string _loadError;

        public DpapiSecretProvider(string path, DataProtectionScope scope)
        {
            _path = path;
            _scope = scope;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                _loadError = string.Format("DPAPI 密钥文件不存在：{0}", path);
                return;
            }
            try
            {
                byte[] protectedBytes = File.ReadAllBytes(path);
                byte[] plain = ProtectedData.Unprotect(protectedBytes, null, scope);
                string text = new UTF8Encoding(false).GetString(plain);
                string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string raw in lines)
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) { continue; }
                    int eq = line.IndexOf('=');
                    if (eq <= 0) { continue; }
                    _map[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch (Exception ex)
            {
                _loadError = string.Format("DPAPI 解密失败（scope={0}）：{1}", scope, ex.Message);
            }
        }

        public string Name { get { return "dpapi(" + _path + ")"; } }

        public bool TryGet(string key, out string value)
        {
            if (_loadError != null) { throw new InvalidOperationException(_loadError); }
            return _map.TryGetValue(key, out value) && !string.IsNullOrEmpty(value);
        }
    }

    /// <summary>DPAPI 密钥文件生成工具（供部署脚本或初始化工具调用）。</summary>
    public static class DpapiSecretStore
    {
        /// <summary>把明文 key=value 文本文件加密为 DPAPI 密文文件。</summary>
        public static void Protect(string sourceTextFile, string targetFile, DataProtectionScope scope)
        {
            string text = File.ReadAllText(sourceTextFile);
            byte[] plain = new UTF8Encoding(false).GetBytes(text);
            byte[] protectedBytes = ProtectedData.Protect(plain, null, scope);
            string dir = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) { Directory.CreateDirectory(dir); }
            File.WriteAllBytes(targetFile, protectedBytes);
        }

        /// <summary>把 DPAPI 密文文件解密为明文文本（仅用于排障，请勿长期保留明文）。</summary>
        public static string UnprotectToText(string protectedFile, DataProtectionScope scope)
        {
            byte[] protectedBytes = File.ReadAllBytes(protectedFile);
            byte[] plain = ProtectedData.Unprotect(protectedBytes, null, scope);
            return new UTF8Encoding(false).GetString(plain);
        }
    }
}