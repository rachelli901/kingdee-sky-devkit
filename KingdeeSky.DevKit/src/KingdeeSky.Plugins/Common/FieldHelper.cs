using System;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.Metadata.EntityElement;
using Kingdee.BOS.Core.Metadata.FieldElement;
using Kingdee.BOS.Orm.DataEntity;

namespace KingdeeSky.Plugins.Common
{
    /// <summary>
    /// 字段/分录取值辅助方法。
    /// 推荐统一通过 BusinessInfo 的元数据对象索引 DynamicObject，
    /// 避免直接写 ORM 属性名（ORM 属性名通常是字段 Key 去掉首字母 F，如 FQty→Qty，
    /// 但并非每个字段都严格遵守该规则，用元数据索引最稳妥）。
    /// </summary>
    internal static class FieldHelper
    {
        /// <summary>获取单据体（分录）行集合，entityKey 例如采购订单的 "FEntity"。</summary>
        public static DynamicObjectCollection GetEntry(DynamicObject dataEntity, BusinessInfo biz, string entityKey)
        {
            if (dataEntity == null || biz == null) { return null; }
            Entity entity = biz.GetEntity(entityKey);
            if (entity == null) { return null; }
            return dataEntity[entity] as DynamicObjectCollection;
        }

        /// <summary>按字段 Key 取标量值；字段不存在或为空时返回 defaultValue。</summary>
        public static T GetValue<T>(DynamicObject dataObject, BusinessInfo biz, string fieldKey, T defaultValue = default(T))
        {
            if (dataObject == null || biz == null) { return defaultValue; }
            Field field = biz.GetField(fieldKey);
            if (field == null) { return defaultValue; }
            object raw = dataObject[field];
            if (raw == null || raw == DBNull.Value) { return defaultValue; }
            if (raw is T typed) { return typed; }
            try
            {
                return (T)System.Convert.ChangeType(raw, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>取基础资料字段对象（如 FMaterialId、FSupplierId），再用 ["Number"]/["Name"] 取编号名称。</summary>
        public static DynamicObject GetBaseData(DynamicObject row, BusinessInfo biz, string fieldKey)
        {
            if (row == null || biz == null) { return null; }
            Field field = biz.GetField(fieldKey);
            return field == null ? null : row[field] as DynamicObject;
        }
    }
}
