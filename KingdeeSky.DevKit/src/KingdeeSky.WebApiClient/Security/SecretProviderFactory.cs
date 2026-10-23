// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;

namespace KingdeeSky.WebApiClient.Security
{
    /// <summary>密钥来源选项（对应配置文件中的非敏感参数）。</summary>
    public sealed class SecretStoreOptions
    {
        /// <summary>env | file | dpapi | kms。</summary>
        public string Store = "env";
        public string SecretsFile;
        public string DpapiFile;
        /// <summary>CurrentUser | LocalMachine。</summary>
        public string DpapiScope = "CurrentUser";
        public string KmsBaseUrl;
        /// <summary>存放 KMS 访问令牌的环境变量名（令牌本身也是密钥，不落配置文件）。</summary>
        public string KmsTokenKey = "KINGDEE_KMS_TOKEN";
    }

    /// <summary>按配置创建密钥来源实现，使密钥存储方式可外部切换。</summary>
    public static class SecretProviderFactory
    {
        public static ISecretProvider Create(SecretStoreOptions options)
        {
            if (options == null) { options = new SecretStoreOptions(); }
            string store = (options.Store ?? "env").Trim().ToLowerInvariant();
            switch (store)
            {
                case "env":
                case "environment":
                    return new EnvironmentSecretProvider();
                case "file":
                case "usersecrets":
                    if (string.IsNullOrWhiteSpace(options.SecretsFile))
                    {
                        throw new InvalidOperationException("SecretStore=file 时必须配置 SecretsFile 路径。");
                    }
                    return new FileSecretProvider(options.SecretsFile);
                case "dpapi":
                    if (string.IsNullOrWhiteSpace(options.DpapiFile))
                    {
                        throw new InvalidOperationException("SecretStore=dpapi 时必须配置 DpapiFile 路径。");
                    }
                    return new DpapiSecretProvider(options.DpapiFile, ParseScope(options.DpapiScope));
                case "kms":
                    string token = Environment.GetEnvironmentVariable(options.KmsTokenKey);
                    return new KmsSecretProvider(options.KmsBaseUrl, token);
                default:
                    throw new ArgumentException("未知的 SecretStore：" + options.Store + "（可选 env / file / dpapi / kms）");
            }
        }

        private static System.Security.Cryptography.DataProtectionScope ParseScope(string scope)
        {
            if (string.Equals(scope, "LocalMachine", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scope, "Machine", StringComparison.OrdinalIgnoreCase))
            {
                return System.Security.Cryptography.DataProtectionScope.LocalMachine;
            }
            return System.Security.Cryptography.DataProtectionScope.CurrentUser;
        }
    }
}