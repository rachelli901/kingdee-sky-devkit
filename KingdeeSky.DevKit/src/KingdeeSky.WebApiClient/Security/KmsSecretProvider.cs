// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace KingdeeSky.WebApiClient.Security
{
    /// <summary>
    /// 通过外部密钥管理服务（KMS）读取密钥的适配器，提供不依赖第三方库的最小实现。
    /// 默认契约（接入时按厂商文档调整）：GET {BaseUrl}/{key}，请求头 Authorization: Bearer {token}，
    /// 响应 JSON 形如 {"value":"..."} 或 {"Value":"..."}。
    /// 生产接入建议替换为厂商官方 SDK；只需继续实现 ISecretProvider，调用方无需改动。
    /// </summary>
    public sealed class KmsSecretProvider : ISecretProvider
    {
        private readonly string _baseUrl;
        private readonly string _token;

        public KmsSecretProvider(string baseUrl, string token)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) { throw new ArgumentException("KMS BaseUrl 未配置（SecretStore=kms 时必填）"); }
            _baseUrl = baseUrl.TrimEnd('/');
            _token = token;
        }

        public string Name { get { return "kms(" + _baseUrl + ")"; } }

        public bool TryGet(string key, out string value)
        {
            value = null;
            string url = _baseUrl + "/" + Uri.EscapeDataString(key);
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Accept = "application/json";
            request.Timeout = 15000;
            if (!string.IsNullOrEmpty(_token)) { request.Headers["Authorization"] = "Bearer " + _token; }
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    var json = new JavaScriptSerializer();
                    var dict = json.DeserializeObject(sr.ReadToEnd()) as IDictionary<string, object>;
                    if (dict == null) { return false; }
                    object v;
                    if (dict.TryGetValue("value", out v) || dict.TryGetValue("Value", out v))
                    {
                        value = Convert.ToString(v);
                        return !string.IsNullOrEmpty(value);
                    }
                    return false;
                }
            }
            catch (WebException)
            {
                // 网络或鉴权失败按“取不到”处理，避免把异常细节（可能含令牌）抛入日志
                return false;
            }
        }
    }
}