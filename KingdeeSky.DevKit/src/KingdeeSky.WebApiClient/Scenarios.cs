// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using KingdeeSky.WebApiClient.Security;

namespace KingdeeSky.WebApiClient
{
    /// <summary>
    /// App.config 中的连接与演示数据配置（仅非敏感项）。
    /// 鉴权方式与密钥来源在此按配置选择；密钥值由 ISecretProvider 提供，不写入配置文件。
    /// </summary>
    internal class Settings
    {
        // 部署环境（dev/test/prod；非敏感）。
        // 来源优先级：KDSKY_ENV 环境变量 > App.config "Environment" > 默认 dev。
        public string DeployEnv = "dev";
        public string DeployEnvSource = "default";

        // 站点与账套（非敏感）
        public string ServerUrl;
        public string AcctId;
        public string UserName;
        public int Lcid = 2052;

        // 鉴权与密钥来源（非敏感开关与键名）
        public string AuthMode = "password";
        public string SecretStore = "env";
        public string SecretsFile;
        public string DpapiFile;
        public string DpapiScope = "CurrentUser";
        public string KmsBaseUrl;
        public string KmsTokenKey = "KINGDEE_KMS_TOKEN";
        public string PasswordKey = "KINGDEE_PASSWORD";
        public string ClientIdKey = "KINGDEE_CLIENT_ID";
        public string ClientSecretKey = "KINGDEE_CLIENT_SECRET";
        public string AppIdKey = "KINGDEE_APP_ID";
        public string AppSecretKey = "KINGDEE_APP_SECRET";
        public string TokenEndpoint;
        public string Scope;
        public string AppSecSigningTemplate;

        // 传输安全（本机可落地）：默认强制 HTTPS，显式放行才允许明文 http 并告警
        public bool AllowInsecureHttp;
        public bool AllowInvalidCertificate;

        // 演示数据编号（非敏感）
        public string OrgNumber;
        public string SupplierNumber;
        public string MaterialNumber;
        public string BillTypeNumber;
        public string DeptNumber;
        public string EmpNumber;

        public static Settings Load()
        {
            var app = ConfigurationManager.AppSettings;
            var s = new Settings
            {
                ServerUrl = app["ServerUrl"],
                AcctId = app["AcctId"],
                UserName = app["UserName"],
                OrgNumber = app["OrgNumber"],
                SupplierNumber = app["SupplierNumber"],
                MaterialNumber = app["MaterialNumber"],
                BillTypeNumber = app["BillTypeNumber"],
                DeptNumber = app["DeptNumber"],
                EmpNumber = app["EmpNumber"]
            };
            int lcid;
            if (int.TryParse(app["Lcid"], out lcid)) { s.Lcid = lcid; }

            // 部署环境：KDSKY_ENV 环境变量优先，其次 App.config "Environment"，最后默认 dev
            string envVar = Env("KDSKY_ENV");
            string appEnv = app["Environment"];
            if (!string.IsNullOrWhiteSpace(envVar)) { s.DeployEnv = envVar.Trim(); s.DeployEnvSource = "environment"; }
            else if (!string.IsNullOrWhiteSpace(appEnv)) { s.DeployEnv = appEnv.Trim(); s.DeployEnvSource = "app.config"; }

            // 环境变量覆盖层：非敏感项按环境切换（dev/test/prod）。
            // 密钥值仍由 ISecretProvider 提供，不进入配置文件或环境变量文件（见 env/*.env.example）。
            s.ServerUrl = ValueOr(Env("KDSKY_SERVER_URL"), s.ServerUrl);
            s.AcctId = ValueOr(Env("KDSKY_ACCT_ID"), s.AcctId);
            s.UserName = ValueOr(Env("KDSKY_USER_NAME"), s.UserName);
            s.AuthMode = ValueOr(Env("KDSKY_AUTH_MODE"), ValueOr(app["AuthMode"], s.AuthMode));
            s.SecretStore = ValueOr(Env("KDSKY_SECRET_STORE"), ValueOr(app["SecretStore"], s.SecretStore));
            s.TokenEndpoint = ValueOr(Env("KDSKY_TOKEN_ENDPOINT"), app["TokenEndpoint"]);

            s.SecretsFile = app["SecretsFile"];
            s.DpapiFile = app["DpapiFile"];
            s.DpapiScope = ValueOr(app["DpapiScope"], s.DpapiScope);
            s.KmsBaseUrl = app["KmsBaseUrl"];
            s.KmsTokenKey = ValueOr(app["KmsTokenKey"], s.KmsTokenKey);
            s.PasswordKey = ValueOr(app["PasswordKey"], s.PasswordKey);
            s.ClientIdKey = ValueOr(app["ClientIdKey"], s.ClientIdKey);
            s.ClientSecretKey = ValueOr(app["ClientSecretKey"], s.ClientSecretKey);
            s.AppIdKey = ValueOr(app["AppIdKey"], s.AppIdKey);
            s.AppSecretKey = ValueOr(app["AppSecretKey"], s.AppSecretKey);
            s.Scope = app["Scope"];
            s.AppSecSigningTemplate = app["AppSecSigningTemplate"];
            // 传输安全开关：代码默认值均为 false（强制 HTTPS / 完整证书校验）；KDSKY_ALLOW_* 环境变量可覆盖
            s.AllowInsecureHttp = TransportSecurityPolicy.ParseFlag(app["AllowInsecureHttp"], false);
            s.AllowInvalidCertificate = TransportSecurityPolicy.ParseFlag(app["AllowInvalidCertificate"], false);
            string insecureRaw = Env("KDSKY_ALLOW_INSECURE_HTTP");
            if (!string.IsNullOrWhiteSpace(insecureRaw)) { s.AllowInsecureHttp = TransportSecurityPolicy.ParseFlag(insecureRaw, s.AllowInsecureHttp); }
            string certRaw = Env("KDSKY_ALLOW_INVALID_CERT");
            if (!string.IsNullOrWhiteSpace(certRaw)) { s.AllowInvalidCertificate = TransportSecurityPolicy.ParseFlag(certRaw, s.AllowInvalidCertificate); }
            return s;
        }

        /// <summary>读取进程级环境变量（存在且非空才视为覆盖值）。</summary>
        private static string Env(string name)
        {
            return Environment.GetEnvironmentVariable(name);
        }

        /// <summary>按配置创建密钥来源（env / file / dpapi / kms）。</summary>
        public ISecretProvider CreateSecretProvider()
        {
            return SecretProviderFactory.Create(new SecretStoreOptions
            {
                Store = SecretStore,
                SecretsFile = SecretsFile,
                DpapiFile = DpapiFile,
                DpapiScope = DpapiScope,
                KmsBaseUrl = KmsBaseUrl,
                KmsTokenKey = KmsTokenKey
            });
        }

        /// <summary>组装鉴权上下文（非敏感标识 + 密钥来源 + 键名）。</summary>
        public AuthContext CreateAuthContext(ISecretProvider secrets)
        {
            return new AuthContext
            {
                AcctId = AcctId,
                UserName = UserName,
                Lcid = Lcid,
                Secrets = secrets,
                TokenEndpoint = TokenEndpoint,
                Scope = Scope,
                PasswordKey = PasswordKey,
                ClientIdKey = ClientIdKey,
                ClientSecretKey = ClientSecretKey,
                AppIdKey = AppIdKey,
                AppSecretKey = AppSecretKey
            };
        }

        /// <summary>配置中仍是占位文字时给出友好提示（不再要求配置文件里有密码）。</summary>
        public void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(ServerUrl) ||
                string.IsNullOrWhiteSpace(AcctId) || AcctId.Contains("请填写") ||
                string.IsNullOrWhiteSpace(UserName))
            {
                throw new InvalidOperationException(
                    "请先修改 App.config 中的 ServerUrl / AcctId / UserName；密码等密钥请通过环境变量、本地密钥文件、DPAPI 或 KMS 提供（见 App.config 说明与 README）。");
            }
        }

        private static string ValueOr(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }

    /// <summary>采购订单（PUR_PurchaseOrder）端到端场景。</summary>
    internal static class Scenarios
    {
        private const string FormId = "PUR_PurchaseOrder";

        /// <summary>场景 1：查询最近 10 张采购订单（ExecuteBillQuery，不依赖任何演示数据编号）。</summary>
        public static void QueryRecentPurchaseOrders(KingdeeWebApiClient client)
        {
            Console.WriteLine("→ 查询最近 10 张采购订单 ...");
            const string fieldKeys =
                "FBillNo,FDate,FSupplierId.FNumber,FSupplierId.FName,FPurchaseOrgId.FName,FDocumentStatus,FCreatorName";
            object[][] rows = client.ExecuteBillQuery(
                FormId, fieldKeys, string.Empty, "FDate DESC", topRowCount: 10);

            if (rows.Length == 0)
            {
                Console.WriteLine("  （没有查询到采购订单）");
                return;
            }

            Console.WriteLine("  单据编号    | 单据日期   | 供应商编号 | 供应商名称            | 采购组织 | 状态 | 创建人");
            Console.WriteLine("  -----------|------------|-----------|----------------------|---------|------|------");
            foreach (object[] r in rows)
            {
                Console.WriteLine("  {0,-11} | {1,-10} | {2,-9} | {3,-20} | {4,-7} | {5,-4} | {6}",
                    Cell(r, 0), Cell(r, 1), Cell(r, 2), Cell(r, 3), Cell(r, 4), Cell(r, 5), Cell(r, 6));
            }
            Console.WriteLine("  共 {0} 行。", rows.Length);
        }

        /// <summary>场景 2：保存一张采购订单（草稿状态），返回新单内码；失败返回 null。</summary>
        public static long? CreatePurchaseOrder(KingdeeWebApiClient client, Settings s)
        {
            Console.WriteLine("→ 新建采购订单（供应商 {0}，物料 {1}，数量 10，含税单价 113，税率 13%）...",
                s.SupplierNumber, s.MaterialNumber);

            // 基础资料统一用 { "FNumber": "编号" } 传参，避免内码随环境变化
            var model = new Dictionary<string, object>
            {
                { "Id", 0 },
                { "FBillNo", string.Empty },
                { "FBillTypeID", NumberObject(s.BillTypeNumber) },
                { "FDate", DateTime.Today.ToString("yyyy-MM-dd") },
                { "FPurchaseOrgId", NumberObject(s.OrgNumber) },
                { "FStockOrgId", NumberObject(s.OrgNumber) },
                { "FSupplierId", NumberObject(s.SupplierNumber) },
                { "FNote", "KingdeeSky.WebApiClient 示例单据，可删除" },
                {
                    "FEntity", new[]
                    {
                        new Dictionary<string, object>
                        {
                            { "FMaterialId", NumberObject(s.MaterialNumber) },
                            { "FQty", 10 },
                            { "FPrice", 100 },
                            { "FTaxPrice", 113 },
                            { "FEntryTaxRate", 13 },
                            { "FNeedDate", DateTime.Today.AddDays(7).ToString("yyyy-MM-dd") },
                            { "FNote", "示例分录" }
                        }
                    }
                }
            };

            string raw = client.Save(FormId, model);
            ApiCallResult result = KingdeeWebApiClient.InspectResult(raw);
            if (!result.IsSuccess)
            {
                Console.WriteLine("  保存失败：{0}", result.Message);
                Console.WriteLine("  原始返回：{0}", Truncate(raw, 800));
                return null;
            }

            long? newId = result.Ids.Count > 0 ? result.Ids[0] : (long?)null;
            string newNo = result.Numbers.Count > 0 ? result.Numbers[0] : string.Empty;
            Console.WriteLine("  保存成功：内码 {0}，单据编号 {1}",
                newId.HasValue ? newId.ToString() : "(未返回)", newNo);
            return newId;
        }

        /// <summary>场景 3：提交并审核指定单据。</summary>
        public static bool SubmitAndAudit(KingdeeWebApiClient client, long id)
        {
            var ids = new List<long> { id };

            Console.WriteLine("→ 提交单据 {0} ...", id);
            string submitRaw = client.Submit(FormId, ids);
            var submit = KingdeeWebApiClient.InspectResult(submitRaw);
            if (!submit.IsSuccess)
            {
                Console.WriteLine("  提交失败：{0}", submit.Message);
                return false;
            }
            Console.WriteLine("  提交成功。");

            Console.WriteLine("→ 审核单据 {0} ...", id);
            string auditRaw = client.Audit(FormId, ids);
            var audit = KingdeeWebApiClient.InspectResult(auditRaw);
            if (!audit.IsSuccess)
            {
                Console.WriteLine("  审核失败：{0}", audit.Message);
                Console.WriteLine("  原始返回：{0}", Truncate(auditRaw, 800));
                return false;
            }
            Console.WriteLine("  审核成功。");
            return true;
        }

        /// <summary>场景 4：查看单据完整数据包（打印前若干字符）。</summary>
        public static void ViewBill(KingdeeWebApiClient client, long id)
        {
            Console.WriteLine("→ 查看单据 {0} 的完整数据包 ...", id);
            string raw = client.View(FormId, id);
            Console.WriteLine(Truncate(raw, 2000));
        }

        /// <summary>
        /// 场景 5：保存一张自定义单据“差旅费报销单”（FormId：SKY_ExpReimburse）。
        /// 该单据需先按《deploy/自定义单据开发指南.md》在 BOS 中创建并发布。
        /// 部门/员工编号在 App.config 留空时不传这两个可选字段。
        /// </summary>
        public static void CreateExpReimburse(KingdeeWebApiClient client, Settings s)
        {
            const string customFormId = "SKY_ExpReimburse";
            Console.WriteLine("→ 新建自定义单据：差旅费报销单 {0} ...", customFormId);

            var model = new Dictionary<string, object>
            {
                { "Id", 0 },
                { "FBillNo", string.Empty },
                { "FDate", DateTime.Today.ToString("yyyy-MM-dd") },
                { "FNote", "KingdeeSky.WebApiClient 创建的差旅报销单" },
                {
                    "FEntity", new[]
                    {
                        new Dictionary<string, object>
                        {
                            { "FExpType", "交通费" },
                            { "FAmount", 320.50m },
                            { "FDescription", "高铁往返" }
                        },
                        new Dictionary<string, object>
                        {
                            { "FExpType", "住宿费" },
                            { "FAmount", 800.00m },
                            { "FDescription", "两晚" }
                        }
                    }
                }
            };
            if (!string.IsNullOrWhiteSpace(s.DeptNumber))
            {
                model["FDeptId"] = NumberObject(s.DeptNumber);
            }
            if (!string.IsNullOrWhiteSpace(s.EmpNumber))
            {
                model["FApplicantId"] = NumberObject(s.EmpNumber);
            }

            string raw = client.Save(customFormId, model);
            ApiCallResult result = KingdeeWebApiClient.InspectResult(raw);
            if (!result.IsSuccess)
            {
                Console.WriteLine("  保存失败：{0}", result.Message);
                Console.WriteLine("  请确认已按《自定义单据开发指南》在 BOS 中创建并发布 SKY_ExpReimburse，且字段 Key 与指南一致。");
                Console.WriteLine("  原始返回：{0}", Truncate(raw, 800));
                return;
            }
            string newId = result.Ids.Count > 0 ? result.Ids[0].ToString() : "(未返回)";
            string newNo = result.Numbers.Count > 0 ? result.Numbers[0] : string.Empty;
            Console.WriteLine("  保存成功：内码 {0}，单据编号 {1}", newId, newNo);
        }

        /// <summary>一键全流程：登录 → 保存 → 提交 → 审核 → 查看。</summary>
        public static void RunFullFlow(KingdeeWebApiClient client, Settings s)
        {
            QueryRecentPurchaseOrders(client);
            Console.WriteLine();

            long? newId = CreatePurchaseOrder(client, s);
            if (!newId.HasValue)
            {
                Console.WriteLine("建单失败，全流程终止。请按错误提示检查 App.config 中的基础资料编号。");
                return;
            }
            Console.WriteLine();

            if (SubmitAndAudit(client, newId.Value))
            {
                Console.WriteLine();
                ViewBill(client, newId.Value);
            }
        }

        private static Dictionary<string, object> NumberObject(string number)
        {
            return new Dictionary<string, object> { { "FNumber", number } };
        }

        private static string Cell(object[] row, int index)
        {
            if (index >= row.Length || row[index] == null) { return string.Empty; }
            // 星空日期在 JSON 中为 /Date(毫秒)/，JavaScriptSerializer 已自动转为 DateTime
            var dt = row[index] as DateTime?;
            if (dt.HasValue) { return dt.Value.ToString("yyyy-MM-dd"); }
            return Convert.ToString(row[index]);
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) { return s; }
            return s.Length <= max ? s : s.Substring(0, max) + " ...（已截断）";
        }
    }
}
