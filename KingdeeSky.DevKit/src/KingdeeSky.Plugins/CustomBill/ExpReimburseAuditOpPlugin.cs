// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System.Text;
using Kingdee.BOS.Util;
using Kingdee.BOS.Core.DynamicForm.PlugIn;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Orm.DataEntity;
using KingdeeSky.Plugins.Common;

namespace KingdeeSky.Plugins.CustomBill
{
    /// <summary>
    /// 差旅费报销单（SKY_ExpReimburse）审核操作插件：审核前总额校验 + 事务内写审计表。
    ///
    /// 注册：BOS 设计器打开 SKY_ExpReimburse → 操作列表 → “审核”操作 → 服务插件 →
    /// KingdeeSky.Plugins.CustomBill.ExpReimburseAuditOpPlugin, KingdeeSky.Plugins
    /// 审计表：deploy/Scripts/T_SKY_AUDITLOG.sql（与采购订单示例共用）。
    /// </summary>
    [HotUpdate]
    public class ExpReimburseAuditOpPlugin : AbstractOperationServicePlugIn
    {
        public const string FormId = "SKY_ExpReimburse";

        private const string FBillNo = "FBillNo";
        private const string FTotalAmount = "FTotalAmount";

        public override void OnPreparePropertys(PreparePropertysEventArgs e)
        {
            base.OnPreparePropertys(e);
            e.FieldKeys.Add(FBillNo);
            e.FieldKeys.Add(FTotalAmount);
        }

        public override void BeforeExecuteOperationTransaction(BeforeExecuteOperationTransaction e)
        {
            base.BeforeExecuteOperationTransaction(e);

            var errors = new StringBuilder();
            foreach (DynamicObject dataEntity in e.DataEntitys)
            {
                string billNo = FieldHelper.GetValue<string>(dataEntity, this.BillBusinessInfo, FBillNo, string.Empty);
                decimal total = FieldHelper.GetValue<decimal>(dataEntity, this.BillBusinessInfo, FTotalAmount);
                if (total <= 0m)
                {
                    errors.AppendLine(string.Format("报销单[{0}]报销总额为 0，不允许审核。", billNo));
                }
            }

            if (errors.Length > 0)
            {
                e.Cancel = true;
                e.CancelMessage = errors.ToString();
            }
        }

        public override void EndOperationTransaction(EndOperationTransactionArgs e)
        {
            base.EndOperationTransaction(e);
            foreach (DynamicObject dataEntity in e.DataEntitys)
            {
                AuditLogHelper.WriteOperationLog(
                    this.Context, dataEntity, this.BillBusinessInfo, FormId, "Audit");
            }
        }
    }
}
