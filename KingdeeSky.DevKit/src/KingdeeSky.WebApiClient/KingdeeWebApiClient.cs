// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using KingdeeSky.WebApiClient.Logging;
using KingdeeSky.WebApiClient.Security;

namespace KingdeeSky.WebApiClient
{
    /// <summary>登录结果。星空约定 LoginResultType == 1 表示登录成功。</summary>
    public class LoginResult
    {
        public bool Success { get; set; }
        public int LoginResultType { get; set; }
        public string Message { get; set; }
        public string RawJson { get; set; }

        public override string ToString()
        {
            return string.Format("Success={0}, LoginResultType={1}, Message={2}", Success, LoginResultType, Message);
        }
    }

    /// <summary>Save/Submit/Audit 等写操作返回结果的简化解析。</summary>
    public class ApiCallResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public List<long> Ids { get; private set; }
        public List<string> Numbers { get; private set; }
        public List<string> Errors { get; private set; }
        public string RawJson { get; set; }

        public ApiCallResult()
        {
            Ids = new List<long>();
            Numbers = new List<string>();
            Errors = new List<string>();
        }
    }

    /// <summary>
    /// 金蝶云·星空 WebAPI 轻量客户端（零金蝶 DLL 依赖）。
    /// 实现 IApiTransport：完成后端 .kdsvc 端点或完整 URL 的 JSON/表单 POST，供选定的鉴权实现复用，
    /// 用 CookieContainer 维持登录会话（OAuth 场景附加 Bearer 令牌）。
    /// 官方说明：主控台【公共设置 → 动态服务定义 → WebAPI】可查看每个端点的参数与在线测试页。
    /// </summary>
    public class KingdeeWebApiClient : IDisposable, IApiTransport
    {
        // ---- 端点（经典星空 / K3Cloud 通用；公有云、私有部署仅 host 不同）----
        private const string SvcSave = "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.Save.common.kdsvc";
        private const string SvcSubmit = "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.Submit.common.kdsvc";
        private const string SvcAudit = "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.Audit.common.kdsvc";
        private const string SvcUnAudit = "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.UnAudit.common.kdsvc";
        private const string SvcDelete = "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.Delete.common.kdsvc";
        private const string SvcView = "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.View.common.kdsvc";
        private const string SvcExecuteBillQuery = "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.ExecuteBillQuery.common.kdsvc";
        private const string SvcPush = "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.Push.common.kdsvc";

        private readonly string _baseUrl;
        private readonly IAuthProvider _authProvider;
        private readonly AuthContext _authContext;
        private readonly SafeLogger _logger;
        private readonly CookieContainer _cookies = new CookieContainer();
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 };
        private readonly bool _allowInsecureHttp;
        private readonly Dictionary<string, string> _extraHeaders = new Dictionary<string, string>();
        private string _bearerToken;

        /// <summary>当前已附加的自定义请求头（AppID/AppSec 签名等；测试可见）。</summary>
        internal Dictionary<string, string> ExtraHeaders { get { return _extraHeaders; } }

        /// <param name="serverUrl">站点地址，如 http://192.168.1.10/K3Cloud/ 或 https://xxx.kdcloud.com/</param>
        /// <param name="acctId">数据中心（账套）ID</param>
        /// <param name="authProvider">鉴权实现（password / oauth2 / appsec），由配置选择</param>
        /// <param name="authContext">鉴权上下文（非敏感标识 + 密钥来源 + 键名）</param>
        /// <param name="logger">日志器；对已登记密钥自动脱敏，可为 null（此时内部自建）</param>
        /// <param name="allowInsecureHttp">是否放行明文 HTTP（默认 false = 强制 HTTPS；仅本地开发显式开启）</param>
        public KingdeeWebApiClient(string serverUrl, string acctId, IAuthProvider authProvider, AuthContext authContext, SafeLogger logger, bool allowInsecureHttp = false)
        {
            if (string.IsNullOrWhiteSpace(serverUrl)) { throw new ArgumentException("ServerUrl 不能为空"); }
            if (string.IsNullOrWhiteSpace(acctId)) { throw new ArgumentException("AcctId（账套ID）不能为空"); }
            if (authProvider == null) { throw new ArgumentNullException("authProvider"); }
            if (authContext == null) { throw new ArgumentNullException("authContext"); }
            _authProvider = authProvider;
            _authContext = authContext;
            _logger = logger ?? new SafeLogger(LogLevel.Info, null);
            _allowInsecureHttp = allowInsecureHttp;
            _baseUrl = TransportSecurityPolicy.EnsureUrlAllowed(serverUrl.TrimEnd('/'), allowInsecureHttp, _logger, "ServerUrl");
        }

        /// <summary>登录：委托当前选定的鉴权实现完成，成功后维持会话（Cookie 或 Bearer）。</summary>
        public LoginResult Login()
        {
            return _authProvider.Authenticate(this, _authContext);
        }

        /// <summary>保存（新增/修改）单据。model 为单据数据包对象，内部会序列化为 data JSON 字符串。</summary>
        public string Save(string formId, object model)
        {
            return FormAction(SvcSave, formId, _json.Serialize(model));
        }

        /// <summary>提交单据。</summary>
        public string Submit(string formId, IList<long> ids)
        {
            return FormAction(SvcSubmit, formId, BuildIdsPayload(ids));
        }

        /// <summary>审核单据。</summary>
        public string Audit(string formId, IList<long> ids)
        {
            return FormAction(SvcAudit, formId, BuildIdsPayload(ids));
        }

        /// <summary>反审核单据。</summary>
        public string UnAudit(string formId, IList<long> ids)
        {
            return FormAction(SvcUnAudit, formId, BuildIdsPayload(ids));
        }

        /// <summary>删除单据（仅草稿/已提交状态可删，已审核需先反审核）。</summary>
        public string Delete(string formId, IList<long> ids)
        {
            return FormAction(SvcDelete, formId, BuildIdsPayload(ids));
        }

        /// <summary>
        /// 下推（单据转换）：按转换规则把源单生成目标单（目标单通常为暂存/已保存状态，
        /// 成功后取返回内码再调 Submit/Audit，审核时触发反写）。
        /// pushParam 示例：new { Ids = "123", RuleId = "", TargetFormId = "STK_InStock",
        /// TargetBillTypeId = "", IsEnableDefaultRule = "true", CustomParams = new {} }。
        /// 具体字段以主控台【动态服务定义→WebAPI→Push】在线测试页为准。
        /// </summary>
        public string Push(string formId, object pushParam)
        {
            return FormAction(SvcPush, formId, _json.Serialize(pushParam));
        }

        /// <summary>按内码查看单据完整数据包。</summary>
        public string View(string formId, long pkId, long createOrgId = 0)
        {
            var data = new Dictionary<string, object>
            {
                { "CreateOrgId", createOrgId },
                { "Number", string.Empty },
                { "Id", pkId }
            };
            return FormAction(SvcView, formId, _json.Serialize(data));
        }

        /// <summary>
        /// 动态表单查询（轻量查询，返回二维数组，每行按 fieldKeys 顺序排列）。
        /// fieldKeys 示例："FBillNo,FDate,FSupplierId.FName,FDocumentStatus"
        /// </summary>
        public object[][] ExecuteBillQuery(string formId, string fieldKeys, string filterString,
            string orderString, int topRowCount = 0, int startRow = 0, int limit = 0)
        {
            var data = new Dictionary<string, object>
            {
                { "FormId", formId },
                { "FieldKeys", fieldKeys },
                { "FilterString", filterString ?? string.Empty },
                { "OrderString", orderString ?? string.Empty },
                { "TopRowCount", topRowCount },
                { "StartRow", startRow },
                { "Limit", limit }
            };
            string raw = Post(SvcExecuteBillQuery, new Dictionary<string, object>
            {
                { "data", _json.Serialize(data) }
            });

            var rows = _json.DeserializeObject(raw) as object[];
            if (rows == null) { return new object[0][]; }
            var result = new object[rows.Length][];
            for (int i = 0; i < rows.Length; i++)
            {
                result[i] = rows[i] as object[] ?? new object[0];
            }
            return result;
        }

        /// <summary>直接调用任意 .kdsvc 端点（用于自定义 WebAPI 或本封装未覆盖的标准服务）。</summary>
        public string Invoke(string serviceName, object payload)
        {
            return Post(serviceName, payload);
        }

        // ---------------- 内部实现 ----------------

        private string FormAction(string service, string formId, string dataJson)
        {
            var payload = new Dictionary<string, object>
            {
                { "formid", formId },
                { "data", dataJson }
            };
            return Post(service, payload);
        }

        private string BuildIdsPayload(IList<long> ids)
        {
            var data = new Dictionary<string, object>
            {
                { "CreateOrgId", 0 },
                { "Numbers", new string[0] },
                { "Ids", ids ?? new List<long>() },
                { "NetworkCtrl", string.Empty },
                { "IgnoreInterationFlag", string.Empty }
            };
            return _json.Serialize(data);
        }

        private string Post(string service, object payload)
        {
            return PostJson(service, _json.Serialize(payload));
        }

        /// <summary>以 JSON 方式提交到相对 .kdsvc 路径或完整 URL，返回响应正文（IApiTransport 实现）。</summary>
        public string PostJson(string pathOrUrl, string jsonBody)
        {
            var request = CreateRequest(pathOrUrl, "application/json; charset=utf-8");
            byte[] body = Encoding.UTF8.GetBytes(jsonBody ?? string.Empty);
            request.ContentLength = body.Length;
            using (Stream rs = request.GetRequestStream())
            {
                rs.Write(body, 0, body.Length);
            }
            return ReadResponseOrThrow(request);
        }

        /// <summary>以表单方式提交（OAuth 令牌端点等），返回响应正文（IApiTransport 实现）。</summary>
        public string PostForm(string pathOrUrl, IDictionary<string, string> form)
        {
            var request = CreateRequest(pathOrUrl, "application/x-www-form-urlencoded; charset=utf-8");
            var sb = new StringBuilder();
            if (form != null)
            {
                bool first = true;
                foreach (var kv in form)
                {
                    if (!first) { sb.Append('&'); }
                    first = false;
                    sb.Append(Uri.EscapeDataString(kv.Key ?? string.Empty));
                    sb.Append('=');
                    sb.Append(Uri.EscapeDataString(kv.Value ?? string.Empty));
                }
            }
            byte[] body = Encoding.UTF8.GetBytes(sb.ToString());
            request.ContentLength = body.Length;
            using (Stream rs = request.GetRequestStream())
            {
                rs.Write(body, 0, body.Length);
            }
            return ReadResponseOrThrow(request);
        }

        /// <summary>设置后续请求使用的 Bearer 令牌（OAuth 场景，IApiTransport 实现）。</summary>
        public void UseBearerToken(string token)
        {
            _bearerToken = token;
        }

        /// <summary>为后续请求附加自定义请求头（AppID/AppSec 签名、时间戳、Nonce 等；IApiTransport 实现）。</summary>
        public void SetRequestHeader(string name, string value)
        {
            if (string.IsNullOrEmpty(name)) { throw new ArgumentException("请求头名称不能为空", "name"); }
            _extraHeaders[name] = value ?? string.Empty;
        }

        private HttpWebRequest CreateRequest(string pathOrUrl, string contentType)
        {
            string url = pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                         pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? pathOrUrl
                : _baseUrl + "/" + pathOrUrl.TrimStart('/');
            // 每个请求都过一遍传输安全策略：https 放行；明文 http 仅在显式允许时放行并告警；否则拒绝
            TransportSecurityPolicy.EnsureUrlAllowed(url, _allowInsecureHttp, _logger, "请求地址");
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = contentType;
            request.Accept = "application/json";
            request.CookieContainer = _cookies;
            if (!string.IsNullOrEmpty(_bearerToken))
            {
                request.Headers[HttpRequestHeader.Authorization] = "Bearer " + _bearerToken;
            }
            foreach (KeyValuePair<string, string> kv in _extraHeaders)
            {
                request.Headers[kv.Key] = kv.Value;
            }
            request.Timeout = 60000;
            request.ReadWriteTimeout = 60000;
            return request;
        }

        private string ReadResponseOrThrow(HttpWebRequest request)
        {
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                // 星空业务错误/鉴权失败仍会返回带 JSON 的响应体，读出来一并抛出，便于定位
                string responseText = string.Empty;
                if (ex.Response != null)
                {
                    using (var sr = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8))
                    {
                        responseText = sr.ReadToEnd();
                    }
                }
                throw new InvalidOperationException(
                    string.Format("调用 {0} 失败：{1}；响应内容：{2}", request.RequestUri, ex.Message, _logger.Scrub(responseText)), ex);
            }
        }

        // ---------------- 响应解析辅助 ----------------

        /// <summary>解析 Save/Submit/Audit 等操作返回的 Result.ResponseStatus 结构。</summary>
        public static ApiCallResult InspectResult(string rawJson)
        {
            var result = new ApiCallResult { RawJson = rawJson, IsSuccess = false };
            IDictionary<string, object> root;
            try
            {
                root = AsDict(new JavaScriptSerializer().DeserializeObject(rawJson));
            }
            catch
            {
                result.Message = "返回内容不是合法 JSON：" + Truncate(rawJson, 500);
                return result;
            }

            IDictionary<string, object> resultNode = AsDict(root.ContainsKey("Result") ? root["Result"] : null);
            if (resultNode == null)
            {
                result.Message = "返回中没有 Result 节点";
                return result;
            }

            IDictionary<string, object> status = AsDict(resultNode.ContainsKey("ResponseStatus") ? resultNode["ResponseStatus"] : null);
            if (status != null)
            {
                object success;
                result.IsSuccess = status.TryGetValue("IsSuccess", out success) && success is bool && (bool)success;

                var errors = status.ContainsKey("Errors") ? status["Errors"] as object[] : null;
                if (errors != null)
                {
                    foreach (object err in errors)
                    {
                        var errDict = AsDict(err);
                        string msg = errDict != null && errDict.ContainsKey("Message") ? Convert.ToString(errDict["Message"]) : Convert.ToString(err);
                        if (!string.IsNullOrWhiteSpace(msg)) { result.Errors.Add(msg); }
                    }
                }

                var successEntitys = resultNode.ContainsKey("SuccessEntitys") ? resultNode["SuccessEntitys"] as object[] : null;
                if (successEntitys != null)
                {
                    foreach (object item in successEntitys)
                    {
                        var d = AsDict(item);
                        if (d == null) { continue; }
                        if (d.ContainsKey("Id"))
                        {
                            long id;
                            if (long.TryParse(Convert.ToString(d["Id"]), out id)) { result.Ids.Add(id); }
                        }
                        if (d.ContainsKey("Number")) { result.Numbers.Add(Convert.ToString(d["Number"])); }
                    }
                }
            }

            if (result.Errors.Count > 0)
            {
                result.Message = string.Join("; ", result.Errors);
            }
            else if (status != null && status.ContainsKey("Msg"))
            {
                result.Message = Convert.ToString(status["Msg"]);
            }
            return result;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) { return s; }
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        private static IDictionary<string, object> AsDict(object o)
        {
            return o as IDictionary<string, object>;
        }

        private static int GetInt(IDictionary<string, object> d, string key)
        {
            if (d != null && d.ContainsKey(key) && d[key] != null)
            {
                int v;
                return int.TryParse(Convert.ToString(d[key]), out v) ? v : 0;
            }
            return 0;
        }

        private static string GetString(IDictionary<string, object> d, string key)
        {
            if (d != null && d.ContainsKey(key) && d[key] != null) { return Convert.ToString(d[key]); }
            return null;
        }

        public void Dispose()
        {
            // 当前仅持有 CookieContainer/HttpWebRequest 短连接，无需要显式释放的资源；保留以支持 using 写法
        }
    }
}
