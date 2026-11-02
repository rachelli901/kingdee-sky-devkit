# KingdeeSky.DevKit 深度分析报告（2026-11-02）

> 分析对象：`KingdeeSky.DevKit` v1.0.4（相对 2026-10-29 基线的增量）
> 方法：逐文件读源码 + 实测构建/测试 + 与项目自带《回归测试方案》交叉核对
> 原则：所有结论附文件路径与行号证据；数字以实测为准，不采信文档自述

---

## 摘要

四天内项目完成了一轮**质量基线抬升**：CI 迁到仓库根并加入 SHA-256 校验、构建产物带自校验 sidecar、客户端入参防御成体系、前端传输层重构为三态可切换、测试从 67 增至 81 条。

实测结论：**81/81 用例通过，0 error / 0 warning 编译，密钥扫描 0 命中**。

工程成熟度已达「可交付样例工程」水平。但存在三类需要收口的问题：**产物堆积**（38 个包仅 2 个带校验文件）、**文档数字漂移**（方案文档仍提 67 条）、**冗余残留**（顶层空目录 + 旧版前端文件）。

---

## 一、项目定位与边界（不变）

**一句话**：面向经典金蝶云·星空（K/3 Cloud，V6–V9）的 WebAPI 对接与服务端二次开发参考工程，**非金蝶官方、不含任何金蝶二进制**。

| 它不是 | 它是 |
| --- | --- |
| ERP 产品 | 接入 ERP 的那**一层**的可复用底座 |
| 含业务逻辑的业务系统 | 通用集成底座 + 三条二开递进范例 |
| 能独立部署运行的系统 | 需配真实星空环境才产生业务价值 |

**技术代际说明**：客户端与插件统一 `.NET Framework 4.8`（`src/KingdeeSky.WebApiClient/KingdeeSky.WebApiClient.csproj:11`、`src/KingdeeSky.Plugins/KingdeeSky.Plugins.csproj:11`）。这是对齐星空 V9.1 BOS 插件宿主 CLR 的**工程决策**，非技术债——插件程序集必须与宿主同 CLR 版本加载。

---

## 二、本轮增量全景（vs 2026-10-29）

| 增量 | 证据 | 评价 |
| --- | --- | --- |
| **CI 迁至仓库根** | `.github/workflows/ci.yml`（新增）、`KingdeeSky.DevKit/.github/workflows/ci.yml`（删除） | ✅ 关键修复：GitHub Actions 只读根目录 workflows，嵌套副本永远不会触发。注释第 8-12 行明确写出了这个坑 |
| **构建产物带 SHA-256** | `eng/build.ps1:207-209` | ✅ 优秀：生成后**回读自校验**（`$writtenHash -ne $hash` 即 throw），格式为 sha256sum 标准双空格 + ASCII 编码，与 CI 解析（`ci.yml:43`）严格对应 |
| **前端传输层重构** | `webapi-console/prototype/index.html:573-598`（transport 单例）、`626-651`（liveCall）、`655-668`（judgeResponse）、`662-680`（kdsvc） | ✅ 三态可切换 `offline` / `live` / `offline(fallback)`，AbortController 6s 超时，协议调用日志带来源标记 |
| **客户端入参防御体系化** | `KingdeeWebApiClient.cs:88-91`（构造）、`170`/`213`（ValidateFormId）、`230-242`（ValidateIds）、`175-177`（分页非负）、`324`（拒绝设置传输层保留头） | ✅ 覆盖完整。`ValidateIds` 额外拦截**重复 id**（`seen.Add` 去重检测），这是易漏点 |
| **测试 67 → 81 条** | 实测 `passed=81 failed=0`；分组 A16/B18/C12/D15/E20 | ✅ 增量集中在密钥(→18)、配置(→12)、客户端(→15) |
| **新增《启动指南.md》** | 根目录 | ✅ 面向新开发者，七章覆盖环境/演示/操作台/构建/真实环境/FAQ/首操顺序 |
| **版本 1.0.3 → 1.0.4** | `eng/version.txt` | — |

---

## 三、架构深析

### 3.1 客户端分层：依赖倒置做得彻底

```
Program.cs（入口/交互）
  └─ Scenarios.cs（业务场景 + Settings 配置加载 + KDSKY_ 环境覆盖）
       └─ KingdeeWebApiClient.cs（端点封装 + IApiTransport 实现）
            ├─ Security/（3 鉴权 × 4 密钥源，正交）
            ├─ Logging/SafeLogger.cs（脱敏）
            └─ Diagnostics/HealthCheck.cs（自检，独立于主流程）
```

**关键设计**：`IApiTransport` 抽象（`KingdeeWebApiClient.cs` 实现的 `PostJson` / `PostForm` / `UseBearerToken` / `SetRequestHeader`）让 OAuth2 与 AppSec 两种鉴权**复用同一套传输层**，而不是各自实现 HTTP。这让"换鉴权方式"真正做到零业务代码改动。

**亮点 1 — 配置零硬编码**：`Scenarios.cs:86-91` 显示非敏感项全部可被 `KDSKY_*` 环境变量覆盖（`KDSKY_SERVER_URL` / `KDSKY_ACCT_ID` / `KDSKY_USER_NAME` / `KDSKY_AUTH_MODE` / `KDSKY_SECRET_STORE` / `KDSKY_TOKEN_ENDPOINT`），传输开关亦可（`:109-111`）。敏感项（密码 / AppSecret / KMS Token）**只走密钥源**，永不进配置文件。

**亮点 2 — 保留头保护**（`KingdeeWebApiClient.cs:324`）：`SetRequestHeader` 显式拒绝设置传输层自管的头（如 `Authorization`），防止外部覆盖鉴权头——这是容易被忽略的安全细节。

**亮点 3 — Dispose 清凭据**（`:441-450`）：释放时清空 Bearer 令牌与签名请求头（AppSec 签名/时间戳/Nonce），避免已弃用实例继续持有可冒充的凭据材料。

### 3.2 安全体系：四层防护 + 唯一真实短板

| 层 | 机制 | 证据 |
| --- | --- | --- |
| 不落盘 | 4 种密钥源：env / file / DPAPI / KMS | `Security/*SecretProvider.cs` |
| 不进库 | 明文密钥扫描作为构建门禁（扫全仓库 `git ls-files`） | `eng/scan-secrets.ps1` |
| 不外泄 | 日志脱敏（已登记密钥整词替换） | `Security/SecretRedactor.cs`、`Logging/SafeLogger.cs` |
| 传输安全 | 默认强制 HTTPS；证书放行**仅限回环地址** | `Security/TransportSecurityPolicy.cs`、`Program.cs:63-83` |

**值得点赞的设计**：mock 服务端**不再硬编码任何口令**（`perf/mock_server.py:44-74`），按「环境变量 → 本机 `.demo-credential`（已 gitignore）→ 现场随机生成」三级解析，源码无明文口令。

**唯一真实短板**：AppSec 的**签名字段与拼接顺序必须以星空【第三方系统登录授权】的在线样本对齐**，不能凭猜。签名器本身已是完整实现（模板化 9 占位符 + HMAC-SHA256 + unix/unix-ms 时间戳 + InvariantCulture），但**协议正确性无法离线自证**。

### 3.3 服务端插件：三条递进路线

| 场景 | 文件 | 演示的注册位置 |
| --- | --- | --- |
| 标准单据 | `PurchaseOrder/{FormPlugin,SaveValidator,AuditOpPlugin}` | 表单 / 保存校验器 / 操作服务 |
| 自定义单据 | `CustomBill/ExpReimburse*` | 同上，自定义业务对象 |
| 下推与反写 | `Convert/{PurchaseOrderToInStockConvertPlugin,PurchaseInStockWriteBackPlugin}` | 单据转换 / 业务流程 |

**贯穿全部样例的铁律**（`PurchaseOrderSaveValidator.cs` 注释原话）：界面插件只在当前用户界面生效、**可被绕过**（如 WebAPI 保存）；保存校验插件在服务端执行，**界面端与 WebAPI 端保存都会被拦截**。

**审计一致性**：`AuditLogHelper` 的审计写入放在 `EndOperationTransaction`（事务内）——审核与审计同生共死；若写在事务后，会出现「审核失败却有日志」。这是 ERP 二开中极易犯错的地方。

### 3.4 工程化体系：门禁链条完整

```
密钥扫描 → 编译(Release, 0 warning 门禁) → 81 条测试 → 打包 + SHA-256 sidecar
```

**CI 五步**（`.github/workflows/ci.yml`）：密钥扫描 → 构建测试打包 → 编号回归（`run-cases.ps1 -All`）→ **校验 SHA-256 sidecar** → 上传 artifact（`if-no-files-found: error`）。

**用例编号体系**：`TC-L1-001`…`TC-L1-079` 连续无缺号，分 5 组；支持按编号 / 分组 / 方法名 / 短名四种方式取证，非法参数返回 exit=2。

---

## 四、实测数据（本次亲自跑出）

| 项目 | 结果 |
| --- | --- |
| 全量测试 | **passed=81 failed=0**，exit 0 |
| 分组分布 | A16 / B18 / C12 / D15 / E20 |
| Release 编译 | 21 个源文件，**0 error / 0 warning** |
| 密钥扫描 | 全仓库跟踪文件，**0 命中** |
| 客户端入参防御 | FormId 全路径校验、ids 拦截 null/空/≤0/重复、分页参数非负、保留头拒绝 |
| SHA-256 sidecar | 格式 `<hash>  <filename>`，与 CI 解析一致 |
| CI 流水线 | 5 步，含产物校验 |

---

## 五、问题清单（按优先级）

### P1｜产物堆积且校验文件不完整（✅ 已解决 2026-11-02）

- **原现象**：`artifacts/` 累积 **38 个 zip，仅 2 个带 `.sha256` sidecar**（sha256 机制是 11-02 才引入的）。
- **已实施**：`eng/build.ps1` 新增 `-KeepArtifacts`（默认 5，0=不清理），在 **sidecar 自校验通过之后**执行：按文件名内嵌时间戳排序保留最近 N 个、zip 与 sidecar 成对处理、**为保留的包补建缺失 sidecar**、顺带清孤儿 sidecar。
- **验证结果**：38 个包 → **5 个包 / 5 个 sidecar，哈希全部一致**；`pruned` / `kept newest 5` / `restored missing sidecar` 三条分支均实跑触发。

### P1｜文档数字漂移

- `deploy/回归测试方案.md:35`：「执行记录模板列出**原有 67 条**」——但全量已是 81 条；
- 同文件 `:111` 同样表述；
- `deploy/回归测试用例执行记录模板.md` 若同样停留在 67 条，逐条取证时会漏 14 条。
- 分组表（`:29-33`）已更新到 16/18/12/13/20，与 CaseMap 一致 ✅，但总条数表述未同步。
- **建议**：统一改为「编号 79 条 + 2 条不编号 = 全量 81 条」的双口径表述，避免读者按 67 条核对时困惑。

### P2｜编号体系与全量用例的 2 条差口（已知取舍，非缺陷）

`ClientTests.BillOperations_RejectEmptyOrInvalidIds` 与 `BillOperations_RejectMissingFormId` 未进编号体系，方案文档 `:35` 已明示「两个尚未编号的测试仍由全量门禁执行」。

- **取舍合理性**：保持 `TC-L1-001…079` 连续不被破坏，避免破坏既有引用——**是对的**。
- **代价**：按编号或按分组取证时覆盖不到这 2 条（`-Group D` 只跑 13/15）。这两条恰好是**入参防御**用例，属于高价值回归点。
- **建议**：不追加编号（会破坏稳定性），改为在 `run-cases.ps1` 增加一个 `-Group` 之外的 `-All` 语义澄清——全量门禁本已覆盖；并在文档里把「按编号取证」的覆盖边界写明。

### P2｜前端回退提示（复核后撤销原建议）

> **2026-11-02 复核更正**：本文初稿把该项列为"提示不够醒目、建议加顶栏横幅"，**该判断有误，已撤销**。
> 复核发现前端其实已有三层提示：
> - `SourceTag` 按来源上色（`index.html:837-840`）：live=蓝 / offline=灰 / **offline(fallback)=橙**，且文案为"离线（自动回退）"；
> - "测试连接"按钮失败时明确提示"在线服务不可达，已回退离线内存模拟"（`index.html:935`）；
> - 协议调用日志行内标注"（已回退离线）"（`index.html:690`）。
>
> 在此基础上再加顶栏横幅属于过度设计，会破坏现有克制的视觉风格。**该项无需处理。**

### P1｜交付包缺少浏览器操作台（由原 P3 升级）

- **现象**：交付包 19 个条目中**不含任何 HTML 操作台**；而根 `README.md:57` 与《启动指南.md:第七节》都把 `deploy/KingdeeSky.WebApiClient.html` 列为客户的**首步操作入口**。
- **影响**：客户拿到 zip 后按文档执行第 2 步"打开浏览器操作台"会**直接找不到文件**——这是文档与产物之间的硬矛盾，属交付阻塞级。
- **根因**：`eng/build.ps1` 的 staging 段只拷贝 `KingdeeSky.DevKit\deploy`（11 篇 .md + Scripts/*.sql），而两个 HTML 交付物在**仓库根的 `deploy\`**，二者路径不同名同，易漏。

### P2｜前端交付物依赖手工同步

- `webapi-console/prototype/index.html`（唯一真源）→ `tools/build-single-file.mjs` → `webapi-console/dist/index.html`；
- 仓库根 `deploy/KingdeeSky.WebApiClient.html` 与 dist 版**内容同源但靠手工复制**，无任何脚本保证同步（已 grep 全部 `.ps1/.mjs/.bat`，无生成逻辑）。
- **风险**：改了 prototype 忘记同步，交付物就是旧版；且两者都在 `scan-secrets.ps1:31` 的扫描范围内，内容漂移会带来安全扫描面不一致。
- **建议**：让 `build-single-file.mjs` 成为唯一产出闸口，构建后自动同步到 `deploy/`；打包时纳入交付包；CI 断言包内 HTML 与 `deploy/` 下的一致。

### P3｜冗余与残留

- `webapi-console/dist/webapi-console.html`（6.26 MB，10-23 旧版）：**未被 git 跟踪**（`git ls-files webapi-console/dist/` 为空），属本地构建残留，删除零风险；
- `deploy/KingdeeSky.WebApiClient.html` 与 `webapi-console/dist/index.html` 双份维护，见上条。

### P3｜依赖解析的隐性约束（值得写进文档）

`tests/run-tests.ps1` 与 `run-cases.ps1` 用**显式源文件清单**，刻意排除 `Program.cs`（避免与测试入口 `Main` 冲突）。

实测提醒：若有人把编译方式改成"递归抓全部 `.cs`"，会立刻得到两个 `Main` 冲突（`CS0017`）。这个约束目前只存在于脚本注释里，**建议在 `CONTRIBUTING.md` 明确写出**。

---

## 六、结论

| 维度 | 评价 |
| --- | --- |
| 架构分层 | ★★★★★ 依赖倒置彻底，鉴权/传输/密钥/日志四层正交解耦 |
| 安全工程 | ★★★★☆ 四层防护到位，唯一短板是 AppSec 协议正确性需真实样本对齐 |
| 二开示范 | ★★★★★ 三条递进路线完整，"服务端强校验"铁律贯穿始终 |
| 工程化 | ★★★★★ 五段门禁 + SHA-256 自校验 + 可追溯用例编号 |
| 文档 | ★★★☆☆ 内容专业详实，但**数字同步是短板**，多处与实现漂移 |
| 产物卫生 | ★★☆☆☆ artifacts 堆积 38 个包、历史包缺校验文件、存在空目录与冗余文件 |

**总体判断**：已达到**可对客户交付**的水准。

**本报告建议的三项,已于 2026-11-02 当日全部落地并实测通过：**

1. ~~清理产物 + 补历史包校验~~ → **已完成**：`build.ps1` 新增 `-KeepArtifacts`（默认 5），按文件名内嵌时间戳保留最近 5 个、zip 与 sidecar 成对处理、为保留的包补建缺失 sidecar。38 个包收敛为 5 个，sidecar 全部一致。
2. ~~同步文档数字~~ → **已完成**：`回归测试方案.md` 统一为「编号 79 + 2 条不编号 = 全量 81」双口径；`tests/README.md` 同步。
3. ~~把「递归抓 .cs 会撞 Main」写进 CONTRIBUTING~~ → **已完成**：新增 §6.1「编译源文件清单的约定」。

**同日追加解决（原 P3 升级为 P1）**：交付包缺客户入口文件 → `build.ps1` staging 纳入仓库根 `deploy\*.html`，`build-single-file.mjs` 构建后自动同步操作台，CI 新增「Verify package carries the delivered HTML」字节级比对。交付包 19 → 21 条目。

**P2（前端回退提示）经复核后撤销**：前端已具备三层提示（`SourceTag` 橙色标签、`测试连接` 按钮失败文案、协议日志行内标注），无需再加横幅。

---

*报告基于 2026-11-02 实测与源码逐行核对生成。所有引用的文件路径与行号均可在仓库中直接复核。*
