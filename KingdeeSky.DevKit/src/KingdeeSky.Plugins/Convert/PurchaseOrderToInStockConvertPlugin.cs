using System;
using System.ComponentModel;
using Kingdee.BOS.Util;
using Kingdee.BOS.Core.Metadata.ConvertElement;
using Kingdee.BOS.Core.Metadata.ConvertElement.PlugIn;
using Kingdee.BOS.Core.Metadata.ConvertElement.PlugIn.Args;
using Kingdee.BOS.Core.Metadata.EntityElement;
using Kingdee.BOS.Core.Metadata.FieldElement;
using Kingdee.BOS.Orm.DataEntity;

namespace KingdeeSky.Plugins.Convert
{
    /// <summary>
    /// 单据转换（下推）插件示例：采购订单 PUR_PurchaseOrder → 采购入库单 STK_InStock。
    ///
    /// 触发时机：在 BOS 配置的“转换规则”执行过程中触发。AfterConvert 在目标单数据包
    /// 全部字段映射、分录生成完毕后执行，是转换插件中最常用的钩子，适合做映射规则
    /// 无法表达的补充赋值（值更新事件在下推过程中不会触发，带不出的字段通常都在这里补）。
    ///
    /// 注册位置：BOS 设计器 → 业务流程/单据转换规则 → 打开“采购订单→采购入库单”规则
    /// （扩展/编辑）→ 插件（插件策略）→ 注册：
    /// KingdeeSky.Plugins.Convert.PurchaseOrderToInStockConvertPlugin, KingdeeSky.Plugins
    /// 注册后停用再启用一次规则，确保插件挂载生效。
    ///
    /// 其他常用钩子（按需重写，签名见官方社区《单据转换插件-插件基类》）：
    ///   OnQueryBuilderParemeter 声明查询源单时额外加载的字段（注意官方拼写为 Paremeter）
    ///   OnFieldMapping / AfterFieldMapping 字段映射前后
    ///   OnCreateTargetBill / AfterCreateTargetBill 目标单创建前后
    ///   AfterConvert 转换完成后（本示例使用）
    /// </summary>
    [HotUpdate]
    [Description("采购订单下推采购入库单-转换插件示例")]
    public class PurchaseOrderToInStockConvertPlugin : AbstractConvertPlugIn
    {
        private const string TargetFormId = "STK_InStock";
        private const string TargetEntryKey = "FInStockEntry"; // 采购入库单单据体实体 Key
        private const string FNote = "FNote";                   // 备注（表头/分录同名）

        public override void AfterConvert(AfterConvertEventArgs e)
        {
            base.AfterConvert(e);

            // FindByEntityKey("FBillHead") 取转换结果中的目标单“单据头”数据包集合
            ExtendedDataEntity[] heads = e.Result.FindByEntityKey("FBillHead");
            if (heads == null || heads.Length == 0) { return; }

            Field headNoteField = e.TargetBusinessInfo.GetField(FNote);
            Entity entryEntity = e.TargetBusinessInfo.GetEntity(TargetEntryKey);
            Field entryNoteField = e.TargetBusinessInfo.GetField(FNote);

            foreach (ExtendedDataEntity head in heads)
            {
                // 通过元数据的 DynamicProperty 索引数据包，避免硬编码 ORM 属性名（如 InStockEntry）
                DynamicObjectCollection entryRows = entryEntity == null
                    ? null
                    : head[entryEntity.DynamicProperty] as DynamicObjectCollection;
                int lineCount = entryRows == null ? 0 : entryRows.Count;

                // 1）表头备注追加转换标记
                if (headNoteField != null)
                {
                    string origin = System.Convert.ToString(head[headNoteField.DynamicProperty]);
                    string stamp = string.Format(
                        "【KingdeeSky 转换插件】由采购订单下推生成，共 {0} 行明细，转换时间 {1:yyyy-MM-dd HH:mm}",
                        lineCount, DateTime.Now);
                    head[headNoteField.DynamicProperty] =
                        string.IsNullOrEmpty(origin) ? stamp : origin + "；" + stamp;
                }

                // 2）分录备注为空时补默认值
                if (entryRows != null && entryNoteField != null)
                {
                    foreach (DynamicObject row in entryRows)
                    {
                        string note = System.Convert.ToString(row[entryNoteField.DynamicProperty]);
                        if (string.IsNullOrWhiteSpace(note))
                        {
                            row[entryNoteField.DynamicProperty] = "由采购订单下推（KingdeeSky 转换插件示例）";
                        }
                    }
                }
            }
        }
    }
}
