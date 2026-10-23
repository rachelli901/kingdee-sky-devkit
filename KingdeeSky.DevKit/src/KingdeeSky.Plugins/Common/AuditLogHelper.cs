// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

using System.Collections.Generic;
using System.Text;
using Kingdee.BOS;
using Kingdee.BOS.App.Data;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Orm.DataEntity;

namespace KingdeeSky.Plugins.Common
{
    /// <summary>
    /// 审核审计日志：把审核/反审核动作写入自定义表 dbo.T_SKY_AUDITLOG。
    /// 建表脚本见 deploy/Scripts/T_SKY_AUDITLOG.sql，需在目标账套库中先执行。
    /// 本方法设计为在操作插件的 EndOperationTransaction（数据库事务内）调用，
    /// 与单据审核同生共死：审核回滚，审计记录一并回滚。
    /// </summary>
    internal static class AuditLogHelper
    {
        public static void WriteOperationLog(Context ctx, DynamicObject dataEntity, BusinessInfo biz,
            string formId, string opType)
        {
            WriteOperationLog(ctx, dataEntity, biz, formId, opType, "KingdeeSky.Plugins 示例审计日志");
        }

        public static void WriteOperationLog(Context ctx, DynamicObject dataEntity, BusinessInfo biz,
            string formId, string opType, string remark)
        {
            string billNo = FieldHelper.GetValue<string>(dataEntity, biz, "FBillNo", string.Empty);
            long billId = FieldHelper.GetValue<long>(dataEntity, biz, "FId", 0L);

            var sql = new StringBuilder();
            sql.AppendLine("INSERT INTO dbo.T_SKY_AUDITLOG");
            sql.AppendLine("    (FFormId, FBillNo, FBillId, FOpType, FOpUserId, FOpUserName, FOpTime, FRemark)");
            sql.AppendLine("VALUES");
            sql.AppendLine("    (@FFormId, @FBillNo, @FBillId, @FOpType, @FOpUserId, @FOpUserName, GETDATE(), @FRemark)");

            // 参数化 SQL，防止单据编号等文本中含单引号导致注入/语法错误
            var parameters = new List<SqlParam>
            {
                new SqlParam("@FFormId", KDDbType.String, formId),
                new SqlParam("@FBillNo", KDDbType.String, billNo ?? string.Empty),
                new SqlParam("@FBillId", KDDbType.Int64, billId),
                new SqlParam("@FOpType", KDDbType.String, opType),
                new SqlParam("@FOpUserId", KDDbType.Int64, ctx.UserId),
                new SqlParam("@FOpUserName", KDDbType.String, ctx.UserName ?? string.Empty),
                new SqlParam("@FRemark", KDDbType.String, remark ?? string.Empty)
            };

            DBServiceHelper.Execute(ctx, sql.ToString(), parameters.ToArray());
        }
    }
}
