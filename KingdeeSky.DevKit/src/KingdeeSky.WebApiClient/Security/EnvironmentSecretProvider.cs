// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;

namespace KingdeeSky.WebApiClient.Security
{
    /// <summary>
    /// 从环境变量读取密钥。查找顺序：进程级 → 用户级 → 机器级。
    /// 说明：这是开发与容器化部署最常用的方式，密钥不落磁盘。
    /// </summary>
    public sealed class EnvironmentSecretProvider : ISecretProvider
    {
        public string Name { get { return "environment"; } }

        public bool TryGet(string key, out string value)
        {
            value = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrEmpty(value))
            {
                value = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User);
            }
            if (string.IsNullOrEmpty(value))
            {
                value = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Machine);
            }
            return !string.IsNullOrEmpty(value);
        }
    }
}