// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System;
using System.ComponentModel;
using Kingdee.BOS.BusinessEntity.BusinessFlow;
using Kingdee.BOS.Core.BusinessFlow.PlugIn;
using Kingdee.BOS.Core.BusinessFlow.PlugIn.Args;
using Kingdee.BOS.Core.Metadata.FieldElement;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Util;
using KingdeeSky.Plugins.Common;

namespace KingdeeSky.Plugins.Convert
{
    /// <summary>
    /// 反写（业务流程服务）插件示例：采购入库单 STK_InStock 保存/审核后，按反写规则
    /// 回写采购订单 PUR_PurchaseOrder 的关联数量等字段时，插入自定义审计逻辑。
    ///
    /// 注意星空 C# 的反写插件基类是 AbstractBusinessFlowServicePlugIn（命名空间
    /// Kingdee.BOS.Core.BusinessFlow.PlugIn）；网上 AbstractWriteBackPlugIn 的驼峰示例
    /// 是苍穹 Java 版，不能用于星空。
    ///
    /// 反写插件是“单据级公用”的：注册后会拦截该下游单据全部反写规则的执行，
    /// 因此必须用反写规则的 GUID（e.Rule.Id）过滤，只处理自己关心的规则。
    ///
    /// 注册位置：BOS 设计器扩展下游单据（采购入库单）→ 表单的业务流程插件/反写插件节点
    /// （不同补丁版本在“编辑→插件注册→业务流程服务插件”或转换规则的插件策略处）→
    /// KingdeeSky.Plugins.Convert.PurchaseInStockWriteBackPlugin, KingdeeSky.Plugins
    ///
    /// 规则 GUID 获取：主控台搜索“反写规则”，打开目标规则，复制其内码/唯一标识，
    /// 替换下方 TargetRuleId；也可在 BeforeWriteBack 中断点查看 e.Rule.Id。
    /// </summary>
    [HotUpdate]
    [Description("采购入库单反写采购订单-反写插件示例")]
    public class PurchaseInStockWriteBackPlugin : AbstractBusinessFlowServicePlugIn
    {
        /// <summary>只处理该反写规则；请替换为你在“反写规则”中复制的真实规则 GUID。</summary>
        public const string TargetRuleId = "在此粘贴反写规则GUID";

        private bool _needHandle;
        private Field _targetField;
        private Field _sourceField;
        private string _ruleId;

        /// <summary>每条反写规则执行前触发：判断是否为目标规则，并缓存上下游字段元数据。</summary>
        public override void BeforeWriteBack(BeforeWriteBackEventArgs e)
        {
            base.BeforeWriteBack(e);
            _needHandle = false;

            if (e.Rule == null || e.Rule.Id != TargetRuleId) { return; }

            _ruleId = e.Rule.Id;
            // e.Rule.Formula：下游反写字段；e.Rule.SourceCommitFieldKey：上游被反写字段
            _targetField = this.BusinessInfo != null ? this.BusinessInfo.GetField(e.Rule.Formula) : null;
            _sourceField = e.SourceBusinessInfo != null
                ? e.SourceBusinessInfo.GetField(e.Rule.SourceCommitFieldKey)
                : null;

            _needHandle = _targetField != null && _sourceField != null;
        }

        /// <summary>
        /// 反写量提交到源单行之后触发：此处只做审计记录（标准反写值仍由平台按规则写入，
        /// 本插件不修改反写量；如需修正分配量，应在 AfterCalcWriteValue 中处理）。
        /// </summary>
        public override void AfterCommitAmount(AfterCommitAmountEventArgs e)
        {
            base.AfterCommitAmount(e);
            if (!_needHandle) { return; }

            // 下游当前行数据包。反写条目（WriteBackRuleRow 等）的具体类型与成员名在不同星空
            // 补丁版本中并不一致，因此这里不对它做编译期类型断言、也不直接访问具体属性，
            // 改为按成员名反射解析；解析不到就跳过审计写入，不影响平台标准反写行为。
            DynamicObject targetRow = ResolveTargetRow(e);

            string remark = string.Format(
                "反写规则 {0} 已执行（操作：{1}，下游字段：{2}，上游字段：{3}）",
                _ruleId, this.OperationNumber,
                _targetField != null ? _targetField.Key : "?",
                _sourceField != null ? _sourceField.Key : "?");

            // 复用审计表；反写场景行数据包取不到单据编号，FBillNo 以规则标识兜底
            if (targetRow != null)
            {
                AuditLogHelper.WriteOperationLog(this.Context, targetRow, this.BusinessInfo,
                    "STK_InStock", "WriteBack", remark);
            }
        }

        /// <summary>
        /// 从反写事件参数中解析出下游当前行数据包。
        /// 逐层按常见成员名尝试（条目 → 行对象 → 数据包对象），任一环取不到即返回 null。
        ///
        /// 之所以用反射而不是直接访问属性：反写事件参数与反写条目的类型在星空各补丁版本
        /// 之间存在差异（社区资料里的 WRule&lt;Id&gt;/Row 并非每个版本都成立），写死某一种
        /// 泛型类型或不存在的属性会导致插件在部分版本上无法编译或抛空引用；反射在编译期
        /// 不做任何类型假设，取不到时退化为「不写审计日志」的安全默认。
        ///
        /// 注：需要在真实星空环境用一次反写操作确认此处能取到行数据包（并确认审计表已落库）。
        /// </summary>
        private static DynamicObject ResolveTargetRow(object eventArgs)
        {
            if (eventArgs == null) { return null; }

            // 逐版本候选成员名：单条反写条目、目标数据包集合等不同叫法都试一遍，
            // 成员不存在只会取不到，不会导致编译失败。
            string[] candidateMembers = { "WriteBackRuleRow", "TargetDataEntities", "WriteBackRows" };
            for (int i = 0; i < candidateMembers.Length; i++)
            {
                DynamicObject row = ExtractRow(GetMember(eventArgs, candidateMembers[i]));
                if (row != null) { return row; }
            }

            return null;
        }

        /// <summary>
        /// 把「数据包本身 / 单条反写条目 / 条目集合」统一归一成行数据包，取不到返回 null。
        /// </summary>
        private static DynamicObject ExtractRow(object candidate)
        {
            if (candidate == null) { return null; }

            // 已经是数据包，直接用
            DynamicObject direct = candidate as DynamicObject;
            if (direct != null) { return direct; }

            if (candidate is string) { return null; }

            // 集合：逐项尝试，取到第一项即返回
            System.Collections.IEnumerable sequence = candidate as System.Collections.IEnumerable;
            if (sequence != null)
            {
                foreach (object item in sequence)
                {
                    DynamicObject row = ExtractRow(item);
                    if (row != null) { return row; }
                }
                return null;
            }

            // 条目 → 行对象 → 数据包对象
            object inner = GetMember(candidate, "Row");
            if (inner != null && !ReferenceEquals(inner, candidate))
            {
                DynamicObject fromInner = ExtractRow(inner);
                if (fromInner != null) { return fromInner; }
            }

            return GetMember(candidate, "DataEntity") as DynamicObject;
        }

        /// <summary>按成员名读取实例属性或字段；成员不存在或无取值器时返回 null。</summary>
        private static object GetMember(object instance, string memberName)
        {
            if (instance == null || string.IsNullOrEmpty(memberName)) { return null; }

            Type type = instance.GetType();

            System.Reflection.PropertyInfo property = type.GetProperty(memberName);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                return property.GetValue(instance, null);
            }

            System.Reflection.FieldInfo field = type.GetField(memberName);
            return field != null ? field.GetValue(instance) : null;
        }
    }
}
