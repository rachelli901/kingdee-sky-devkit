using System;
using Kingdee.BOS.Util;
using Kingdee.BOS.Core.Bill.PlugIn;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;

namespace KingdeeSky.Plugins.PurchaseOrder
{
    /// <summary>
    /// 采购订单（PUR_PurchaseOrder）表单/单据插件 —— 负责界面层交互。
    ///
    /// 注册位置（BOS 设计器）：
    ///   扩展“采购订单”表单 → 表单插件 → 添加 → 选择本类
    ///   KingdeeSky.Plugins.PurchaseOrder.PurchaseOrderFormPlugin, KingdeeSky.Plugins
    ///
    /// 演示的事件：
    ///   AfterCreateNewData 新建后赋默认值
    ///   EntryRowCreated    新增分录行时赋默认值
    ///   DataChanged        字段值改变后联动计算 / 即时校验
    ///   BarItemClick       工具栏按钮点击时拦截
    /// </summary>
    [HotUpdate] // Kingdee.BOS.Util.HotUpdate：开启插件热更新（需站点 Common.config 启用 IsEnablePlugInHotUpdate；仅 1 小时内更新的 DLL 生效，首次部署仍需重启/回收）
    public class PurchaseOrderFormPlugin : AbstractBillPlugIn
    {
        // 采购订单单据体（分录）实体 Key、常用字段 Key
        private const string EntityKey = "FEntity";
        private const string FQty = "FQty";               // 采购数量
        private const string FTaxPrice = "FTaxPrice";     // 含税单价
        private const string FAllAmount = "FAllAmount";   // 价税合计
        private const string FNeedDate = "FNeedDate";     // 需求日期
        private const string FNote = "FNote";             // 备注（表头）

        /// <summary>新建单据、数据包创建完成后触发，适合放表头默认值。</summary>
        public override void AfterCreateNewData(EventArgs e)
        {
            base.AfterCreateNewData(e);
            this.View.Model.SetValue(FNote, "KingdeeSky.Plugins 表单插件已加载（新建默认备注，可手工删除）");
        }

        /// <summary>分录新增行后触发，给新行一个默认需求日期（今天 +7 天）。</summary>
        public override void EntryRowCreated(EntryRowCreatedEventArgs e)
        {
            base.EntryRowCreated(e);
            if (e.EntityKey == EntityKey)
            {
                this.View.Model.SetValue(FNeedDate, DateTime.Today.AddDays(7), e.Row);
            }
        }

        /// <summary>字段值改变后触发：数量/含税单价联动价税合计；数量非法即时提示。</summary>
        public override void DataChanged(DataChangedEventArgs e)
        {
            base.DataChanged(e);

            // 表头字段 e.Row 为 0/-1；分录字段 e.Row 为分录行号（从 0 开始）
            if (e.Row < 0) { return; }

            if (e.Field.Key == FQty || e.Field.Key == FTaxPrice)
            {
                decimal qty = ToDecimal(this.View.Model.GetValue(FQty, e.Row));
                decimal taxPrice = ToDecimal(this.View.Model.GetValue(FTaxPrice, e.Row));

                if (qty < 0)
                {
                    this.View.ShowMessage(string.Format("第 {0} 行采购数量不能为负数，请检查。", e.Row + 1));
                    this.View.Model.SetValue(FQty, 0m, e.Row);
                    qty = 0m;
                }

                // 界面层即时重算，仅为交互体验；保存时服务端标准逻辑仍会按价税算法重算一遍
                this.View.Model.SetValue(FAllAmount, Math.Round(qty * taxPrice, 2), e.Row);
            }
        }

        /// <summary>工具栏按钮点击：点“审核”前做一次界面层前置检查（服务端操作插件会再校验一次）。</summary>
        public override void BarItemClick(BarItemClickEventArgs e)
        {
            base.BarItemClick(e);
            if (e.Cancel || e.BarItemKey != "tbAudit") { return; }

            int rowCount = this.View.Model.GetEntryRowCount(EntityKey);
            for (int i = 0; i < rowCount; i++)
            {
                object raw = this.View.Model.GetValue(FNeedDate, i);
                DateTime? needDate = raw as DateTime?;
                if (needDate == null || needDate.Value.Date < DateTime.Today)
                {
                    e.Cancel = true;
                    this.View.ShowMessage(string.Format(
                        "第 {0} 行需求日期为空或早于今天，不能审核，请修正后再试。", i + 1));
                    return;
                }
            }
        }

        private static decimal ToDecimal(object value)
        {
            if (value == null || value == DBNull.Value) { return 0m; }
            decimal result;
            return decimal.TryParse(value.ToString(), out result) ? result : 0m;
        }
    }
}
