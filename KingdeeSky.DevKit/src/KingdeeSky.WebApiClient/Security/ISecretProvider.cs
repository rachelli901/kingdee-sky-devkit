// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;

namespace KingdeeSky.WebApiClient.Security
{
    /// <summary>
    /// 密钥来源抽象。实现可互换：开发用环境变量或本地密钥文件，生产用 Windows DPAPI 加密文件或 KMS。
    /// 调用方只依赖本接口，替换密钥来源不需要改动业务代码。
    /// </summary>
    public interface ISecretProvider
    {
        /// <summary>来源名称，用于日志与报错。</summary>
        string Name { get; }

        /// <summary>尝试读取密钥；缺失或为空返回 false。加载配置失败时抛出 InvalidOperationException。</summary>
        bool TryGet(string key, out string value);
    }

    /// <summary>密钥缺失异常。</summary>
    public class SecretNotFoundException : Exception
    {
        public SecretNotFoundException(string key, string provider)
            : base(string.Format("未能在密钥源“{0}”中找到密钥“{1}”。请检查环境变量、密钥文件或 DPAPI/KMS 配置。", provider, key))
        {
        }
    }

    /// <summary>ISecretProvider 便捷扩展。</summary>
    public static class SecretProviderExtensions
    {
        /// <summary>读取密钥，缺失时抛出 SecretNotFoundException。</summary>
        public static string Get(this ISecretProvider provider, string key)
        {
            string value;
            if (provider.TryGet(key, out value) && !string.IsNullOrEmpty(value)) { return value; }
            throw new SecretNotFoundException(key, provider.Name);
        }
    }
}