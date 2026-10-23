// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using System.Collections.Generic;

namespace KingdeeSky.WebApiClient.Security
{
    /// <summary>
    /// 鉴权方式抽象：账号密码、OAuth 2.0、AppID/AppSec 签名等实现可互换。
    /// 替换鉴权方式只需更换实现并通过配置选择，调用方（KingdeeWebApiClient 等）无需改动。
    /// </summary>
    public interface IAuthProvider
    {
        /// <summary>鉴权方式名称，用于日志与报错。</summary>
        string Name { get; }

        /// <summary>执行鉴权；成功后由传输层维持会话（Cookie 或 Bearer 令牌）。</summary>
        LoginResult Authenticate(IApiTransport transport, AuthContext context);
    }

    /// <summary>HTTP 传输抽象，由客户端实现，供鉴权提供者复用其连接与会话。</summary>
    public interface IApiTransport
    {
        /// <summary>以 JSON 方式 POST；pathOrUrl 可为 .kdsvc 相对路径或绝对 URL，返回响应正文。</summary>
        string PostJson(string pathOrUrl, string jsonBody);

        /// <summary>以表单方式 POST（用于 OAuth 令牌端点等），返回响应正文。</summary>
        string PostForm(string pathOrUrl, IDictionary<string, string> form);

        /// <summary>设置后续请求使用的 Bearer 令牌（OAuth 场景）。</summary>
        void UseBearerToken(string token);

        /// <summary>为后续所有请求附加自定义请求头（AppID/AppSec 签名场景，多次调用可叠加）。</summary>
        void SetRequestHeader(string name, string value);
    }

    /// <summary>鉴权上下文：非敏感标识 + 密钥来源 + 各鉴权方式所需配置。</summary>
    public sealed class AuthContext
    {
        public string AcctId;
        public string UserName;
        public int Lcid = 2052;
        public ISecretProvider Secrets;

        // OAuth 2.0 client_credentials
        public string TokenEndpoint;
        public string Scope;
        public string ClientIdKey = "KINGDEE_CLIENT_ID";
        public string ClientSecretKey = "KINGDEE_CLIENT_SECRET";

        // 账号密码
        public string PasswordKey = "KINGDEE_PASSWORD";

        // AppID / AppSec 签名
        public string AppIdKey = "KINGDEE_APP_ID";
        public string AppSecretKey = "KINGDEE_APP_SECRET";
    }
}