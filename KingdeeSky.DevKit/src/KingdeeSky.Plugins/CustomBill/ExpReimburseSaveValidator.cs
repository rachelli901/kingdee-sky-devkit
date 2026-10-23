using System;
using Kingdee.BOS.Util;
using Kingdee.BOS.Core.Metadata.Validator;
using Kingdee.BOS.Orm.DataEntity;
using KingdeeSky.Plugins.Common;

namespace KingdeeSky.Plugins.CustomBill
{
    /// <summary>
    /// 差旅费报销单（SKY_ExpReimburse）保存校验插件：服务端强校验，WebAPI 保存同样生效。
    ///
    /// 注册：BOS 设计器打开 SKY_ExpReimburse → 操作列表 → “保存”操作 → 校验器 →
    /// KingdeeSky.Plugins.CustomBill.ExpReimburseSaveValidator, KingdeeSky.Plugins
    /// </summary>
    [HotUpdate]
    public class ExpReimburseSaveValidator : AbstractValidator
    {
        private const string EntityKey = "FEntity";
        private const string FExpType = "FExpType";
        private const string FAmount = "FAmount";

        public override void Validate()
        {
            foreach (DynamicObject dataEntity in this.DataEntitys)
            {
                DynamicObjectCollection entry =
                    FieldHelper.GetEntry(dataEntity, this.BillBusinessInfo, EntityKey);

                if (entry == null || entry.Count == 0)
                {
                    this.AddError(FExpType, "差旅费报销单至少需要一行费用明细。");
                    continue;
                }

                for (int i = 0; i < entry.Count; i++)
                {
                    DynamicObject row = entry[i];

                    string expType = FieldHelper.GetValue<string>(row, this.BillBusinessInfo, FExpType, string.Empty);
                    if (string.IsNullOrWhiteSpace(expType))
                    {
                        this.AddErrorField(EntityKey, i, FExpType,
                            string.Format("第 {0} 行未填写费用项目。", i + 1));
                    }

                    decimal amount = FieldHelper.GetValue<decimal>(row, this.BillBusinessInfo, FAmount);
                    if (amount <= 0m)
                    {
                        this.AddErrorField(EntityKey, i, FAmount,
                            string.Format("第 {0} 行费用金额必须大于 0。", i + 1));
                    }
                }
            }
        }
    }
}
