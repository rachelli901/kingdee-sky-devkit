// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using System.Text;
using Kingdee.BOS.Util;
using Kingdee.BOS.Core.DynamicForm.PlugIn;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Orm.DataEntity;
using KingdeeSky.Plugins.Common;

namespace KingdeeSky.Plugins.PurchaseOrder
{
    /// <summary>
    /// 采购订单（PUR_PurchaseOrder）“审核”操作的操作服务插件 —— 服务端事务逻辑。
    ///
    /// 注册位置（BOS 设计器）：
    ///   扩展“采购订单”表单 → 操作列表 → “审核”操作 → 操作服务插件 → 新增 → 选择本类
    ///   KingdeeSky.Plugins.PurchaseOrder.PurchaseOrderAuditOpPlugin, KingdeeSky.Plugins
    ///
    /// 操作插件事件链（与本示例对应）：
    ///   OnPreparePropertys                 声明插件要用到的字段，平台只加载这些字段以节省开销
    ///   OnAddValidators                    可追加校验器（本示例未使用）
    ///   BeforeExecuteOperationTransaction 事务前校验，可设置 e.Cancel 阻断审核
    ///   BeginOperationTransaction          事务开始（每批数据一次）
    ///   EndOperationTransaction            事务内执行，本示例在此写审计表，随审核事务一起提交/回滚
    ///   AfterExecuteOperationTransaction   事务提交后执行（适合发通知、调外部接口）
    /// </summary>
    [HotUpdate]
    public class PurchaseOrderAuditOpPlugin : AbstractOperationServicePlugIn
    {
        public const string FormId = "PUR_PurchaseOrder";

        private const string EntityKey = "FEntity";
        private const string FBillNo = "FBillNo";
        private const string FNeedDate = "FNeedDate";

        /// <summary>声明本插件需要平台加载到数据包中的字段（表头字段直接加 Key，分录字段同样加 Key）。</summary>
        public override void OnPreparePropertys(PreparePropertysEventArgs e)
        {
            base.OnPreparePropertys(e);
            e.FieldKeys.Add(FBillNo);
            e.FieldKeys.Add(FNeedDate);
        }

        /// <summary>事务执行前的最后一道关卡：发现违规数据直接取消整个审核操作。</summary>
        public override void BeforeExecuteOperationTransaction(BeforeExecuteOperationTransaction e)
        {
            base.BeforeExecuteOperationTransaction(e);

            var errors = new StringBuilder();

            foreach (DynamicObject dataEntity in e.DataEntitys)
            {
                string billNo = FieldHelper.GetValue<string>(dataEntity, this.BillBusinessInfo, FBillNo, string.Empty);
                DynamicObjectCollection entry =
                    FieldHelper.GetEntry(dataEntity, this.BillBusinessInfo, EntityKey);
                if (entry == null) { continue; }

                for (int i = 0; i < entry.Count; i++)
                {
                    DateTime needDate = FieldHelper.GetValue<DateTime>(entry[i], this.BillBusinessInfo, FNeedDate);
                    if (needDate != default(DateTime) && needDate.Date < DateTime.Today)
                    {
                        errors.AppendLine(string.Format(
                            "单据[{0}]第 {1} 行需求日期 {2:yyyy-MM-dd} 早于今天，不允许审核。",
                            billNo, i + 1, needDate));
                    }
                }
            }

            if (errors.Length > 0)
            {
                e.Cancel = true;
                e.CancelMessage = errors.ToString();
            }
        }

        /// <summary>事务内：审核数据落库的同一事务中写审计日志，保证“审核成功必有日志、审核回滚日志也回滚”。</summary>
        public override void EndOperationTransaction(EndOperationTransactionArgs e)
        {
            base.EndOperationTransaction(e);

            // 部署前请先在账套库执行 deploy/Scripts/T_SKY_AUDITLOG.sql
            foreach (DynamicObject dataEntity in e.DataEntitys)
            {
                AuditLogHelper.WriteOperationLog(
                    this.Context, dataEntity, this.BillBusinessInfo, FormId, "Audit");
            }
        }
    }
}
