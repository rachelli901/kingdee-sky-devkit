using System;
using Kingdee.BOS.Util;
using Kingdee.BOS.Core.Metadata.Validator;
using Kingdee.BOS.Orm.DataEntity;
using KingdeeSky.Plugins.Common;

namespace KingdeeSky.Plugins.PurchaseOrder
{
    /// <summary>
    /// 采购订单（PUR_PurchaseOrder）保存操作的业务校验插件 —— 服务端、强校验。
    ///
    /// 注册位置（BOS 设计器）：
    ///   扩展“采购订单”表单 → 操作列表 → “保存”操作 → 操作校验器 → 新增 → 选择本类
    ///   KingdeeSky.Plugins.PurchaseOrder.PurchaseOrderSaveValidator, KingdeeSky.Plugins
    ///
    /// 与表单插件的区别：表单插件只在当前用户的界面上生效，可被绕过（如 WebAPI 保存）；
    /// 校验插件在服务端执行，界面端与 WebAPI 端的保存都会被拦截。
    /// </summary>
    [HotUpdate]
    public class PurchaseOrderSaveValidator : AbstractValidator
    {
        private const string EntityKey = "FEntity";
        private const string FQty = "FQty";
        private const string FMaterialId = "FMaterialId";
        private const string FNeedDate = "FNeedDate";

        /// <summary>校验入口；对本次保存涉及的每张单据逐行检查，错误通过 AddError/AddErrorField 收集。</summary>
        public override void Validate()
        {
            // DataEntitys 为本次操作的单据对象集合（可能是多张）
            foreach (DynamicObject dataEntity in this.DataEntitys)
            {
                DynamicObjectCollection entry =
                    FieldHelper.GetEntry(dataEntity, this.BillBusinessInfo, EntityKey);
                if (entry == null) { continue; }

                for (int i = 0; i < entry.Count; i++)
                {
                    DynamicObject row = entry[i];

                    // 1）物料必填（标准产品通常已校验，此处演示如何在二开中强制兜底）
                    DynamicObject material = FieldHelper.GetBaseData(row, this.BillBusinessInfo, FMaterialId);
                    if (material == null)
                    {
                        this.AddErrorField(EntityKey, i, FMaterialId,
                            string.Format("第 {0} 行未录入物料。", i + 1));
                    }

                    // 2）采购数量必须大于 0
                    decimal qty = FieldHelper.GetValue<decimal>(row, this.BillBusinessInfo, FQty);
                    if (qty <= 0m)
                    {
                        this.AddErrorField(EntityKey, i, FQty,
                            string.Format("第 {0} 行采购数量必须大于 0。", i + 1));
                    }

                    // 3）需求日期若填写，不得早于当天
                    DateTime needDate = FieldHelper.GetValue<DateTime>(row, this.BillBusinessInfo, FNeedDate);
                    if (needDate != default(DateTime) && needDate.Date < DateTime.Today)
                    {
                        this.AddErrorField(EntityKey, i, FNeedDate,
                            string.Format("第 {0} 行需求日期不能早于今天（{1:yyyy-MM-dd}）。",
                                i + 1, DateTime.Today));
                    }
                }
            }
        }
    }
}
