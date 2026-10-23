# KingdeeSky.DevKit —— 金蝶云·星空二次开发示例代码包

面向**经典金蝶云·星空（K/3 Cloud → 星空企业版/标准版，V6.x–V9.x，C# / .NET Framework 体系）**
的二次开发入门工程，包含一套可直接改造的**服务端插件示例**和一个**零依赖 WebAPI 对接客户端**。

> 适用对象：第一次接触星空二开、需要一个能编译、能注册、能跑通全链路的参考工程的 .NET 开发者。
> 配套背景知识见同目录上层的《金蝶云星空全景调研报告.html》（该调研报告为独立产物，不在本仓库内）。

---

## 合规声明

- 本代码包为**自主编写的学习示例**，不包含、不分发任何金蝶软件的二进制程序（DLL/EXE）、
  源码、元数据、图标或文档原文。
- “金蝶”“金蝶云·星空”“K/3 Cloud”等是金蝶国际软件集团的商标/产品名；
  `Kingdee.BOS.*` 程序集的著作权归金蝶所有，需从你**已合法授权**的星空环境中获取，仅限
  在授权范围内开发使用，不得反编译、反向工程或用于破解许可（《金蝶软件许可协议》明文禁止）。
- 示例代码可自由用于学习和你自有星空环境的二次开发，按“现状”提供，不含任何担保。

## 这是什么 / 不是什么

| 是 | 不是 |
|---|---|
| 星空 BOS 三类插件的标准写法范例（表单/校验/操作） | 金蝶官方 SDK，不随补丁发布 |
| 不依赖金蝶 DLL 即可编译的 WebAPI 调用范例 | 星空产品本身，不能替代星空环境运行 |
| 采购订单场景的端到端演示（界面联动→保存校验→审核拦截→审计落库→外部系统建单） | 苍穹/星瀚（Java，`kd.bos.*`）的示例——两者类名撞名但完全不通用 |

---

## 目录结构

```
KingdeeSky.DevKit/
├─ KingdeeSky.DevKit.sln              Visual Studio 解决方案（VS2019/2022 均可）
├─ README.md                          本文件
├─ CONTRIBUTING.md                    分支策略、提交规范、版本与发布约定
├─ build.bat                          一键构建：密钥扫描 → 编译 → 测试门禁 → 打包（委托 eng/build.ps1）
├─ scan-secrets.bat                   明文密钥/敏感信息静态扫描入口（委托 eng/scan-secrets.ps1）
├─ .gitignore                         忽略规则（密钥 / 构建产物 / 日志 / 真实 env 文件）
├─ .gitattributes                     行尾与二进制文件约定（文本统一 LF，Windows 脚本 CRLF）
├─ .github/workflows/ci.yml           CI 流水线（密钥扫描→构建→测试→打包，GitHub Actions）
├─ .vscode/                           共享的 VS Code 调试/任务配置（launch.json、tasks.json；个人偏好 settings.json 不入库）
├─ eng/
│  ├─ build.ps1                       生产构建脚本（密钥扫描 → 编译 WebApiClient → 测试门禁 → 可选 Plugins → 版本化打包）
│  ├─ scan-secrets.ps1                明文密钥/敏感信息静态扫描（CI/构建门禁，命中即失败）
│  ├─ run-env.ps1                     环境切换钩子：加载 env/<环境>.env 后运行 --selfcheck --json 或客户端（发布门禁）
│  └─ version.txt                     版本号（打包版本来源，build.bat 第一参数可覆盖）
├─ env/                               多环境分层配置模板（*.env.example 入库；真实 env/*.env 不入库）
│  ├─ dev.env.example                 dev：本地明文 HTTP、密码走环境变量密钥源
│  ├─ test.env.example                test：HTTPS、独立账套/账号、密码走环境变量密钥源
│  └─ prod.env.example                prod：HTTPS、DPAPI 密钥源、生产账号（真实值按密钥策略人工注入）
├─ lib/
│  ├─ README.md                       需要从星空服务器拷贝的 4 个 DLL 清单（V9.1 锁定）
│  └─ copy-kingdee-dlls.bat           一键从站点 bin 拷贝 DLL 的脚本
├─ src/
│  ├─ KingdeeSky.Plugins/             【工程1】服务端插件类库（.NET Framework 4.8；BOS 引用目录 KingdeeBinsDir 可配置）
│  │  ├─ Common/
│  │  │  ├─ FieldHelper.cs            元数据方式取字段/分录值（避免硬编码 ORM 属性名）
│  │  │  └─ AuditLogHelper.cs         参数化 SQL 写自定义审计表
│  │  ├─ PurchaseOrder/               场景一：标准单据（采购订单）三类插件
│  │  │  ├─ PurchaseOrderFormPlugin.cs    表单插件：默认值/字段联动/即时校验/按钮拦截
│  │  │  ├─ PurchaseOrderSaveValidator.cs 保存校验插件：服务端强校验
│  │  │  └─ PurchaseOrderAuditOpPlugin.cs  审核操作插件：事务前阻断 + 事务内写审计
│  │  ├─ CustomBill/                  场景二：自定义单据（差旅费报销单 SKY_ExpReimburse）
│  │  │  ├─ ExpReimburseFormPlugin.cs     表单插件：分录金额汇总、删行重算、保存前拦截
│  │  │  ├─ ExpReimburseSaveValidator.cs  保存校验：明细行/费用项目/金额服务端强校验
│  │  │  └─ ExpReimburseAuditOpPlugin.cs  审核操作：总额校验 + 写审计表
│  │  └─ Convert/                     场景三：下推转换与反写
│  │     ├─ PurchaseOrderToInStockConvertPlugin.cs 转换插件：AfterConvert 补表头/分录备注
│  │     └─ PurchaseInStockWriteBackPlugin.cs      反写插件：按规则 GUID 过滤 + 反写审计
│  └─ KingdeeSky.WebApiClient/        【工程2】WebAPI 控制台（零金蝶 DLL 依赖）
│     ├─ App.config                   非敏感配置（站点地址/账套ID/账号/密钥源开关；无明文密码）
│     ├─ App.config.example           配置模板（入库）
│     ├─ secrets.env.example          密钥键名模板（值留空；本文件入库，真实值写 secrets.env 不入库）
│     ├─ KingdeeWebApiClient.cs       登录/查询/查看/保存/提交/审核/反审核/删除/下推封装
│     ├─ Scenarios.cs                 采购订单场景 + 自定义单据建单场景（Settings/配置加载/KDSKY_* 环境变量覆盖层）
│     ├─ Program.cs                   交互菜单（鉴权与密钥源接线、日志脱敏）
│     ├─ Diagnostics/                 `--selfcheck` 健康检查（HealthCheck.cs：配置/密钥源/鉴权组件/传输层/传输安全策略，支持 `--json`）
│     ├─ Security/                    鉴权与密钥抽象（IAuthProvider/ISecretProvider 四实现/传输安全策略/AppSec 签名骨架/脱敏）
│     └─ Logging/                     结构化脱敏日志（SafeLogger）
├─ tests/
│  ├─ README.md                       用例执行脚本说明（run-tests / run-cases 用法、退出码与分组）
│  ├─ run-tests.bat / run-tests.ps1   测试门禁入口：全量 62 条（失败返回非 0）
│  ├─ run-cases.bat / run-cases.ps1   L1 用例逐条/按分组执行（-Case / -Group / -List）
│  └─ KingdeeSky.WebApiClient.Tests/  62 个零依赖用例（鉴权/密钥/配置/客户端链/传输安全/AppSec 签名/多环境覆盖）
├─ perf/                              客户端侧性能冒烟（零金蝶 DLL 依赖）
│  ├─ stub_server.py                  极简星空 WebAPI stub（仅 Python 标准库，固定响应）
│  ├─ PerfProbe.cs                    性能探针：登录 1 次 + ExecuteBillQuery 循环，统计 min/avg/p95/max
│  ├─ run-perf.ps1                    编排：起 stub → 编译 → 跑探针 → 存基线（perf-baseline-*.txt）
│  └─ perf-baseline-latest.txt        最近一次基线（运行 run-perf.ps1 生成，已 .gitignore，不入库）
└─ deploy/
   ├─ 部署与调试指南.md                编译、BOS 注册、部署、附加调试、FAQ（必读）
   ├─ 自定义单据开发指南.md            从零在 BOS 建差旅费报销单（字段/编号/发布/授权/菜单）
   ├─ 单据转换与反写插件指南.md        转换规则、反写规则配置与插件事件链、WebAPI 下推
   ├─ 鉴权降级与传输安全说明.md        AuthMode 一览与降级路径、AppSec 签名骨架、传输安全开关、密钥分层、FAQ
   ├─ 安全审计与漏洞扫描清单.md        已落地自动门禁、待接真实环境审计项、本机复核命令、CI 门禁顺序
   ├─ SLA与备份恢复预案.md             SLA 指标与 RTO/RPO、备份对象与策略、备份验证与恢复演练、监控告警、事故分级响应
   ├─ 多环境发布与变更管理.md         dev/test/prod 分层模型、KDSKY_* 覆盖、发布审批、灰度/金丝雀、回滚流程、变更记录模板
   ├─ 回归测试方案.md                  回归分层（L1 代码级 / L2 协议级 / L3 业务级）、用例分组与执行流程、进入与退出准则、缺陷分级与回归触发
   ├─ 回归测试用例执行记录模板.md      单次回归执行的留档模板（62 条逐条清单 + 执行记录栏）
   ├─ 回滚预案.md                      版本化产物保留、发布前检查清单、WebApiClient / 插件 / SQL 回滚步骤、事故复盘清单
   └─ Scripts/
      └─ T_SKY_AUDITLOG.sql           审计表建表脚本（在账套库执行，各审核插件共用）
```

---

## 两个工程分别解决什么问题

### 工程 1：KingdeeSky.Plugins（插件类库，编译需要金蝶 DLL）

包含三个递进场景，共 8 个插件类：

**场景一：标准单据采购订单 `PUR_PurchaseOrder` 的三类插件**（构成一条完整防线）

| 类 | 类型 | 触发时机 | 演示能力 |
|---|---|---|---|
| `PurchaseOrderFormPlugin` | 表单/单据插件（继承 `AbstractBillPlugIn`） | 用户在界面操作时 | 新建默认值、新增分录行默认需求日期、数量×含税单价联动价税合计、负数即时纠正、点“审核”前拦截 |
| `PurchaseOrderSaveValidator` | 操作校验器（继承 `AbstractValidator`） | 服务端执行“保存”时 | 物料必填兜底、数量必须 &gt; 0、需求日期不得早于今天；**WebAPI 保存同样被拦截** |
| `PurchaseOrderAuditOpPlugin` | 操作服务插件（继承 `AbstractOperationServicePlugIn`） | 服务端执行“审核”时 | `OnPreparePropertys` 声明字段、事务前 `e.Cancel` 阻断、事务内写审计表（随审核同提交同回滚） |

**场景二：从零新建自定义单据“差旅费报销单” `SKY_ExpReimburse`**（建单步骤见
《deploy/自定义单据开发指南.md》）

| 类 | 类型 | 演示能力 |
|---|---|---|
| `ExpReimburseFormPlugin` | 表单插件 | 分录金额实时汇总到表头总额（不可编辑）、删行重算、保存前界面拦截 |
| `ExpReimburseSaveValidator` | 保存校验器 | 至少一行明细、费用项目必填、每行金额 &gt; 0（WebAPI 同样生效） |
| `ExpReimburseAuditOpPlugin` | 审核操作插件 | 审核前总额校验、审核事务内写审计表 |

**场景三：下推转换与反写**（配置步骤与事件链见《deploy/单据转换与反写插件指南.md》）

| 类 | 类型 | 演示能力 |
|---|---|---|
| `PurchaseOrderToInStockConvertPlugin` | 转换插件（继承 `AbstractConvertPlugIn`） | 采购订单下推采购入库单，`AfterConvert` 中遍历目标单补表头/分录备注 |
| `PurchaseInStockWriteBackPlugin` | 反写插件（继承 `AbstractBusinessFlowServicePlugIn`） | 按反写规则 GUID 过滤（单据级公用插件必须过滤）、反写提交后写审计日志 |

### 工程 2：KingdeeSky.WebApiClient（控制台，无需金蝶 DLL）

用 `HttpWebRequest` 直接 POST 星空内置 `.kdsvc` 端点，`CookieContainer` 维持会话，
仅依赖 .NET Framework 自带程序集，**打开即可编译**。覆盖：

1. `ValidateUser` 登录（账套 ID + 账号 + lcid）
2. `ExecuteBillQuery` 查询最近采购订单
3. `Save` 新建采购订单（基础资料按 `FNumber` 传参）
4. `Submit` + `Audit` 提交并审核
5. `View` 查看单据完整数据包
6. `Push` 下推（单据转换，参数以动态服务定义在线测试页为准）
7. 另含 `UnAudit`、`Delete`、任意端点 `Invoke`（可用于调用自定义 WebAPI）

菜单 5 演示用同一套 Save 端点创建**自定义单据**差旅费报销单 `SKY_ExpReimburse`
（需先按《自定义单据开发指南》在 BOS 中建单发布；部门/员工编号在 App.config 留空即不传）。

---

## 快速开始

### A. 跑通 WebAPI 客户端（不需要在服务器上，5 分钟）

1. 用 Visual Studio 2019/2022 打开 `KingdeeSky.DevKit.sln`（需 .NET Framework 4.8 开发工具包）；
2. 修改 `src/KingdeeSky.WebApiClient/App.config` 的**非敏感项**：`ServerUrl`（星空地址）、`AcctId`（账套 ID）、`UserName`（登录账号）、`AuthMode`（password / oauth2 / appsec）；
   **密码等密钥不写入配置文件**，用环境变量注入，例如 `set KINGDEE_PASSWORD=你的密码`；
   也可按 `secrets.env.example` 使用本地密钥文件（`SecretStore=file`），或切换 `dpapi` / `kms`；
3. F5 运行 `KingdeeSky.WebApiClient`，启动时会自动登录并打印结果（页头显示鉴权方式与密钥来源），按菜单操作。
4. 菜单 4（一键全流程：查询 → 建单 → 提交 → 审核 → 查看）还需在 App.config 填入账套内真实的组织/供应商/物料/单据类型**编号**；
   菜单 1（查询）不依赖这些编号，建议先跑菜单 1 验证连通性；
   菜单 5（自定义单据）需先在 BOS 建好并发布 `SKY_ExpReimburse`，部门/员工编号可留空。

### B. 编译并部署插件（需要一台你有授权的星空应用服务器/本机演示环境）

1. 双击 `lib/copy-kingdee-dlls.bat`，按提示粘贴星空站点 `WebSite\bin` 路径，自动拷入 4 个 DLL；
2. 在 VS 中重新生成 `KingdeeSky.Plugins`，得到 `KingdeeSky.Plugins.dll`（和 .pdb）；
3. 在账套库执行 `deploy/Scripts/T_SKY_AUDITLOG.sql`；
4. 按《`deploy/部署与调试指南.md`》在 BOS 设计器中注册插件：
   - 采购订单三个插件：以“扩展”方式注册到标准单据（步骤见部署指南）；
   - 差旅费报销单：先按《`deploy/自定义单据开发指南.md`》从零建单并发布，再注册三个插件；
   - 转换/反写插件：按《`deploy/单据转换与反写插件指南.md`》配置转换规则、反写规则后注册
     （反写插件里的规则 GUID 占位符必须替换为真实值）；
5. 把 DLL（调试期含 PDB）放到站点 `bin`，附加 `w3wp.exe` 调试。

> 类注册字符串（复制到 BOS 插件注册框）：
>
> ```
> KingdeeSky.Plugins.PurchaseOrder.PurchaseOrderFormPlugin, KingdeeSky.Plugins
> KingdeeSky.Plugins.PurchaseOrder.PurchaseOrderSaveValidator, KingdeeSky.Plugins
> KingdeeSky.Plugins.PurchaseOrder.PurchaseOrderAuditOpPlugin, KingdeeSky.Plugins
> KingdeeSky.Plugins.CustomBill.ExpReimburseFormPlugin, KingdeeSky.Plugins
> KingdeeSky.Plugins.CustomBill.ExpReimburseSaveValidator, KingdeeSky.Plugins
> KingdeeSky.Plugins.CustomBill.ExpReimburseAuditOpPlugin, KingdeeSky.Plugins
> KingdeeSky.Plugins.Convert.PurchaseOrderToInStockConvertPlugin, KingdeeSky.Plugins
> KingdeeSky.Plugins.Convert.PurchaseInStockWriteBackPlugin, KingdeeSky.Plugins
> ```

---

## 构建 / 测试 / 打包（生产化）

- **一键构建**：根目录 `build.bat [版本号]`（等价于 `powershell -NoProfile -ExecutionPolicy Bypass -File eng\build.ps1`）：
  0. 先跑**明文密钥/敏感信息静态扫描**（`eng\scan-secrets.ps1`，可单独运行 `scan-secrets.bat`；也可 `-SkipSecretScan` 跳过），有命中立即中断（返回非 0）；
  1. 编译 WebApiClient（Release，零金蝶 DLL 依赖）；
  2. 跑**测试门禁**（62 个用例：鉴权/密钥来源/配置分层/客户端调用链/传输安全/AppSec 签名/多环境覆盖），任一失败立即中断（返回非 0）；
  3. 若 `lib\` 下已有从授权环境拷贝的 `Kingdee.BOS.Core.dll`，自动追加编译 Plugins（用 MSBuild，`KingdeeBinsDir` 指向 lib）；
  4. 产出版本化 ZIP 到 `artifacts\`（含 WebApiClient 可执行体、exe.config、模板、部署指南、SQL、README、VERSION）。
- **单独跑测试**：`tests\run-tests.bat`（或 `tests\run-tests.ps1`）。
- **单独扫密钥**：`scan-secrets.bat`（或 `eng\scan-secrets.ps1`；`-IncludeTests` 连测试夹具一起扫）。
- **CI**：`.github\workflows\ci.yml` 在 push / PR 时自动执行「密钥扫描 → 构建 → 测试 → 打包」并把 ZIP 上传为 Artifact；
  Plugins 因金蝶 DLL 不可分发，CI 检出后 `lib\` 下没有 `Kingdee.BOS.Core.dll`，`build.ps1` 探测不到即自动跳过其编译；如需在流水线中构建 Plugins，须在受控 / 自托管 runner 上预先放置授权 DLL。
- **版本锁定**：`eng/version.txt` 记录基线版本，`build.bat 1.1.0` 可临时覆盖；各工程注释与 `lib/README.md` 明确锁定**星空 V9.1 / .NET Framework 4.8**。
- **密钥与配置分层**：`App.config` 只放非敏感项与密钥键名；密钥值经 `ISecretProvider`（环境变量 / 本地文件 / Windows DPAPI / KMS）注入，配置以 `AuthMode` / `SecretStore` 切换，日志经 `SafeLogger` 自动脱敏。
- **传输与鉴权安全**：客户端默认强制 HTTPS（`AllowInsecureHttp` 显式设为 `true` 才放行明文 HTTP，并在日志/自检中告警；生产必须保持 `false`）；注意仓库自带的 `App.config` / `App.config.example` 为便于本地 mock 联调仍写 `true`，投产前必须改回 `false`；`AllowInvalidCertificate` 默认 `false`；AppSec 签名走 `IAppSecSigner` 骨架，配置 `AppSecSigningTemplate` 即启用 HMAC-SHA256 签名注入（详见 `deploy\鉴权降级与传输安全说明.md`）。
- **运行前自检**：`KingdeeSky.WebApiClient.exe --selfcheck` 输出结构化健康检查（版本 / 配置 / 密钥源 / 鉴权组件 / 传输层 / 传输安全策略），不发起网络请求；返回 0 = 全部就绪，返回 1 = 存在未就绪项（用于 CI 或部署脚本的预检门禁）。加 `--json` 输出**单行机器可读 JSON**（含 `passed`/`failed`/`elapsedMs`/`checks`），可直接接入监控与告警。
- **性能冒烟与基线**：`perf\run-perf.ps1` 启动本地 stub（`perf\stub_server.py`）→ 编译 `perf\PerfProbe.cs`（与客户端同源编译、零金蝶 DLL 依赖）→ 登录 1 次 + `ExecuteBillQuery` 循环 N 次 → 记录 min/avg/p95/max 延迟与失败数；基线写入 `perf\perf-baseline-<时间戳>.txt` 与 `perf\perf-baseline-latest.txt`，可纳入 CI 对比（与历史基线 P95 偏离 > 50% 需人工评估，> 100% 触发性能告警）。
- **回滚预案**：见 `deploy\回滚预案.md`（版本化产物保留、发布前检查清单、WebApiClient 与插件及数据库脚本的回滚步骤、事故复盘清单）。
- **SLA 与备份恢复预案**：见 `deploy\SLA与备份恢复预案.md`（SLA 指标与 RTO/RPO、备份对象与策略、备份验证与恢复演练模板、监控告警、事故分级响应、上线前检查表）。
- **多环境发布与变更管理**：见 `deploy\多环境发布与变更管理.md`（dev/test/prod 分层模型、`KDSKY_*` 环境变量覆盖层与优先级、`eng\run-env.ps1` 切换钩子、发布审批、灰度/金丝雀、回滚流程、变更记录模板）。每套环境用 `env\<环境>.env` 声明**非敏感项**（密钥值仍由密钥源注入，不入 env 文件），运行 `eng\run-env.ps1 -Environment test -Command selfcheck` 即按该环境加载配置并执行 `--selfcheck --json` 门禁；退出码 0 = 该环境就绪可发布，1 = 存在未就绪项禁止发布。

---

## 环境要求

| 项 | 要求 |
|---|---|
| IDE | Visual Studio 2019 / 2022（社区版即可），含“.NET 桌面开发”工作负载 |
| 目标框架 | .NET Framework 4.8（本仓库**锁定星空 V9.1**，勿混用其它版本程序集） |
| 星空环境 | 任意已授权的经典星空企业版/标准版（私有部署、本机演示环境或单租户环境） |
| 运行账号 | 有采购订单业务权限、并被授予 WebAPI 调用权限的用户 |
| 数据库 | 审计表脚本为 SQL Server 语法（经典星空主力库） |

## 关键提醒（新手最容易踩的坑）

1. **别和苍穹搞混**：本工程是星空 C#（`Kingdee.BOS.*`）；网上 `import kd.bos.*` 的 Java
   代码属于苍穹/星瀚，不能照搬到星空，反之亦然。
2. **DLL 版本必须与目标环境补丁一致**：升级补丁后重新从站点 bin 拷贝、重新编译。
3. **优先“扩展”标准单据，不要直接改标准元数据**，否则升级补丁时可能被覆盖。
4. **界面插件可被绕过**（WebAPI/导入不经过界面），关键规则必须放在校验/操作插件等服务端逻辑里。
5. **字段 Key 不要猜**：在 BOS 设计器选中字段看“标识(Key)”属性；
   WebAPI 参数可用主控台【公共设置→动态服务定义→WebAPI】的在线测试页先试。
6. 插件工程引用金蝶 DLL 时已设置 `Private=False`（不复制到输出目录），
   **切勿把 `Kingdee.BOS.*.dll` 一起拷进站点 bin 覆盖标准程序集**。

## 作者与署名

- **作者**：rachelli901（<https://github.com/rachelli901>）
- **邮箱**：rachelli901@users.noreply.github.com
- **仓库**：<https://github.com/rachelli901/kingdee-sky-devkit>

本仓库中的示例代码、测试、脚本、文档与 HTML 成品均由作者编写；第三方依赖与金蝶厂商资料的权属见上方「合规声明」。

## 官方资料入口（公开免费）

- 产品帮助手册：<https://help.open.kingdee.com>
- WebAPI 手册：<https://help.open.kingdee.com/dokuwiki/doku.php?id=webapi>
- 开发者社区（二开文章/学习地图）：<https://vip.kingdee.com>
- 开发者中心：<https://developer.kingdee.com>
- 补丁下载：<https://download.kdcloud.com>
- 认证中心：<https://kone.kingdee.com/certcenter>
