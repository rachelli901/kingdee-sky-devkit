// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;

namespace KingdeeSky.WebApiClient.Security
{
    /// <summary>
    /// AppID/AppSec 签名上下文：签名所需的全部输入。
    /// 星空不同版本的签名字段与算法以主控台【系统管理 → 系统管理 → 授权 → 第三方系统登录授权】为准，
    /// 本结构覆盖常见要素，接入真实环境时按规范增减字段即可。
    /// </summary>
    public sealed class AppSecSigningContext
    {
        public string AppId;
        public string AppSecret;
        public string Timestamp;
        public string Nonce;
        public string Method = "POST";
        public string Path;
        public string Body;
        public string BodyHash;
        public string Query;
    }

    /// <summary>签名结果：最终签名串与需要附加到请求的头。</summary>
    public sealed class AppSecSignatureResult
    {
        public string Signature;
        public string HeaderName = "X-Kingdee-Signature";
        public string Timestamp;
        public string Nonce;
    }

    /// <summary>AppID/AppSec 签名器抽象：按版本规范把请求要素拼成待签串并求签名。</summary>
    public interface IAppSecSigner
    {
        string Name { get; }

        AppSecSignatureResult Sign(AppSecSigningContext context);
    }
}