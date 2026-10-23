using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;

namespace KingdeeSky.WebApiClient
{
    /// <summary>App.config 中的连接与演示数据配置。</summary>
    internal class Settings
    {
        public string ServerUrl;
        public string AcctId;
        public string UserName;
        public string Password;
        public int Lcid = 2052;
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
                Password = app["Password"],
                OrgNumber = app["OrgNumber"],
                SupplierNumber = app["SupplierNumber"],
                MaterialNumber = app["MaterialNumber"],
                BillTypeNumber = app["BillTypeNumber"],
                DeptNumber = app["DeptNumber"],
                EmpNumber = app["EmpNumber"]
            };
            int lcid;
            if (int.TryParse(app["Lcid"], out lcid)) { s.Lcid = lcid; }
            return s;
        }

        /// <summary>配置中仍是占位文字时给出友好提示。</summary>
        public void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(ServerUrl) ||
                string.IsNullOrWhiteSpace(AcctId) || AcctId.Contains("请填写") ||
                string.IsNullOrWhiteSpace(Password) || Password.Contains("请填写"))
            {
                throw new InvalidOperationException(
                    "请先修改 App.config 中的 ServerUrl / AcctId / UserName / Password 后再运行。");
            }
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
