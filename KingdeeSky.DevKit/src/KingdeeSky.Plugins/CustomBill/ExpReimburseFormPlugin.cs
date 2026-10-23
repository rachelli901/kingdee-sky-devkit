// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using Kingdee.BOS.Util;
using Kingdee.BOS.Core.Bill.PlugIn;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;

namespace KingdeeSky.Plugins.CustomBill
{
    /// <summary>
    /// 自定义单据“差旅费报销单”（FormId：SKY_ExpReimburse，按《deploy/自定义单据开发指南.md》在 BOS 中创建）
    /// 的表单插件：分录金额实时汇总到表头报销总额、保存前界面拦截。
    ///
    /// 注册：BOS 设计器打开 SKY_ExpReimburse → 编辑 → 表单插件 →
    /// KingdeeSky.Plugins.CustomBill.ExpReimburseFormPlugin, KingdeeSky.Plugins
    /// </summary>
    [HotUpdate]
    public class ExpReimburseFormPlugin : AbstractBillPlugIn
    {
        private const string EntityKey = "FEntity";
        private const string FAmount = "FAmount";           // 分录：费用金额
        private const string FExpType = "FExpType";         // 分录：费用项目（文本）
        private const string FTotalAmount = "FTotalAmount"; // 表头：报销总额

        /// <summary>分录新增行后触发：金额默认 0，保证汇总不出现空值。</summary>
        public override void EntryRowCreated(EntryRowCreatedEventArgs e)
        {
            base.EntryRowCreated(e);
            if (e.EntityKey == EntityKey)
            {
                this.View.Model.SetValue(FAmount, 0m, e.Row);
            }
        }

        /// <summary>分录金额改变后，重新汇总表头总额。</summary>
        public override void DataChanged(DataChangedEventArgs e)
        {
            base.DataChanged(e);
            if (e.Row < 0) { return; }
            if (e.Field.Key == FAmount || e.Field.Key == FExpType)
            {
                RecalcTotal();
            }
        }

        /// <summary>分录行删除后也需要重算（删除行同样触发数据变化，重算一次成本极低，统一在此兜底）。</summary>
        public override void AfterEntryRowDelete(AfterEntryRowDeleteEventArgs e)
        {
            base.AfterEntryRowDelete(e);
            if (e.EntityKey == EntityKey)
            {
                RecalcTotal();
            }
        }

        /// <summary>点击“保存”前：至少一行、每行费用项目与金额合法，界面层先拦一道。</summary>
        public override void BarItemClick(BarItemClickEventArgs e)
        {
            base.BarItemClick(e);
            if (e.Cancel || e.BarItemKey != "tbSave") { return; }

            int rowCount = this.View.Model.GetEntryRowCount(EntityKey);
            if (rowCount == 0)
            {
                e.Cancel = true;
                this.View.ShowMessage("请至少录入一行费用明细后再保存。");
                return;
            }

            decimal total = 0m;
            for (int i = 0; i < rowCount; i++)
            {
                object typeRaw = this.View.Model.GetValue(FExpType, i);
                if (typeRaw == null || string.IsNullOrWhiteSpace(System.Convert.ToString(typeRaw)))
                {
                    e.Cancel = true;
                    this.View.ShowMessage(string.Format("第 {0} 行未填写费用项目。", i + 1));
                    return;
                }
                decimal amount = ToDecimal(this.View.Model.GetValue(FAmount, i));
                if (amount <= 0m)
                {
                    e.Cancel = true;
                    this.View.ShowMessage(string.Format("第 {0} 行费用金额必须大于 0。", i + 1));
                    return;
                }
                total += amount;
            }

            if (total <= 0m)
            {
                e.Cancel = true;
                this.View.ShowMessage("报销总额必须大于 0。");
            }
        }

        private void RecalcTotal()
        {
            int rowCount = this.View.Model.GetEntryRowCount(EntityKey);
            decimal total = 0m;
            for (int i = 0; i < rowCount; i++)
            {
                total += ToDecimal(this.View.Model.GetValue(FAmount, i));
            }
            this.View.Model.SetValue(FTotalAmount, Math.Round(total, 2));
        }

        private static decimal ToDecimal(object value)
        {
            if (value == null || value == DBNull.Value) { return 0m; }
            decimal result;
            return decimal.TryParse(value.ToString(), out result) ? result : 0m;
        }
    }
}
