/* ==============================================================================
   KingdeeSky.DevKit - 审核审计日志表（SQL Server）
   执行位置：目标“账套数据库”（不是管理中心库），在 SSMS 中选择对应业务库后执行。
   作用：PurchaseOrderAuditOpPlugin.EndOperationTransaction 会向本表写入审核记录。
   表名加 T_SKY_ 前缀，避免与金蝶标准表 T_ 前缀冲突；二开自定义表推荐独立前缀。
   ============================================================================== */

IF OBJECT_ID(N'dbo.T_SKY_AUDITLOG', N'U') IS NOT NULL
BEGIN
    PRINT N'表 dbo.T_SKY_AUDITLOG 已存在，跳过创建。';
END
ELSE
BEGIN
    CREATE TABLE dbo.T_SKY_AUDITLOG
    (
        FId          BIGINT         IDENTITY(1, 1) NOT NULL CONSTRAINT PK_T_SKY_AUDITLOG PRIMARY KEY,
        FFormId      NVARCHAR(50)   NOT NULL,                  -- 业务对象 FormId，如 PUR_PurchaseOrder
        FBillNo      NVARCHAR(100)  NOT NULL,                  -- 单据编号
        FBillId      BIGINT         NULL,                      -- 单据内码
        FOpType      NVARCHAR(20)   NOT NULL,                  -- 操作类型：Audit/UnAudit ...
        FOpUserId    BIGINT         NULL,                      -- 操作人内码
        FOpUserName  NVARCHAR(100)  NULL,                      -- 操作人名称
        FOpTime      DATETIME       NOT NULL CONSTRAINT DF_T_SKY_AUDITLOG_FOpTime DEFAULT (GETDATE()),
        FRemark      NVARCHAR(500)  NULL
    );

    CREATE INDEX IX_T_SKY_AUDITLOG_BILL ON dbo.T_SKY_AUDITLOG (FFormId, FBillNo);
    CREATE INDEX IX_T_SKY_AUDITLOG_TIME ON dbo.T_SKY_AUDITLOG (FOpTime);

    PRINT N'表 dbo.T_SKY_AUDITLOG 创建完成。';
END
GO

-- 验证：
-- SELECT TOP 20 * FROM dbo.T_SKY_AUDITLOG ORDER BY FId DESC;
