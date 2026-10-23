# KingdeeSky DevKit —— 金蝶云·星空二次开发示例工作区

> *English TL;DR:* A secondary-development example workspace for Kingdee Cloud Sky
> (classic K/3 Cloud lineage, C# / .NET Framework 4.8, baseline V9.1): BOS server
> plugin skeletons, a DLL-free WebAPI client, a local protocol mock, a 62-case
> regression suite, and two ready-to-open HTML artifacts. No Kingdee binaries included.

本仓库是面向**经典金蝶云·星空（K/3 Cloud → 星空企业版/标准版，V6.x–V9.x，C# / .NET Framework 体系）**
的二次开发示例工作区：一个可直接改造的**服务端插件示例包**、一个**零金蝶 DLL 依赖的 WebAPI 对接客户端**、
一个**无需星空环境即可跑通的协议级模拟服务**，以及两个**双击即开的 HTML 成品**。

> 适用对象：第一次接触星空二开、需要一个能编译、能注册、能跑通全链路的参考工程的 .NET 开发者。
> 本仓库不含金蝶任何二进制（DLL/EXE）、源码或元数据，`Kingdee.BOS.*` 程序集需从你已合法授权的星空环境获取。

---

## 仓库里有什么

仓库以**工作区**为根，示例包位于 `KingdeeSky.DevKit/` 子目录，四个区域各司其职：

| 区域 | 内容 | 入口 |
|---|---|---|
| `KingdeeSky.DevKit/` | 二次开发示例包：服务端插件源码、零依赖 WebAPI 客户端工程、测试套件、构建/密钥扫描脚本、部署与运维文档、CI 定义 | `KingdeeSky.DevKit/README.md` |
| `KingdeeSky.WebApiClient/` | **无星空环境也能跑**的演示包：本地 Python 模拟服务 + 已编译客户端（免编译即可运行） | `KingdeeSky.WebApiClient/README.md` |
| `webapi-console/` | 单文件浏览器操作台的**源码工程**：原型页面、打包脚本、真机验证脚本与证据 | `webapi-console/prototype/index.html` |
| `deploy/` | 已交付的 HTML 成品（部署操作手册、浏览器操作台），双击即用 | 见下方「三个能立刻跑起来的入口」 |

根目录 `.gitignore` 声明了入库范围：仅上述四个区域 + 该文件本身；工作区中间态、个人笔记与可再生的构建产物不入库。

---

## 三个能立刻跑起来的入口

### 1. 没有星空环境：跑 WebAPI 全链路演示（约 5 分钟）

```bat
cd KingdeeSky.WebApiClient
run-demo.bat
```

自动启动本地模拟服务（端口 `18080`，账号 `Administrator / demo123`，预置 3 张采购订单），
再打开菜单程序，可完整走通 **登录 → 查询 → 建单 → 提交 → 审核 → 查看 → 反审核 / 删除 / 下推**。
这是**协议级模拟**，业务校验、审批流、反写与权限不在模拟范围内；接真实环境只改 `app/App.config` 的站点与账套，客户端代码零改动。

### 2. 浏览器里直接看：两个 HTML 成品

| 文件 | 说明 |
|---|---|
| `deploy/KingdeeSky.DevKit-V9.1 deployment.html` | 《KingdeeSky.DevKit 部署到金蝶云·星空 V9.1 服务器操作手册》，约 33 KB |
| `deploy/KingdeeSky.WebApiClient.html` | WebApiClient 浏览器操作台，约 6.3 MB；单文件全内联、**离线双击即用**，可切换在线模式对接本地 mock |

操作台为 DuMate（banlan）单一风格 provider，基于 React 18.3.1 + antd 5.29.3 + `@baidu/qianfan-antd-kit`，
默认走内置离线内存模拟（不发网络请求），需要真实联调时切到在线模式指向 `http://127.0.0.1:18080/K3Cloud`，失败自动回落并被记录在协议日志里。

### 3. 有授权的星空环境：编译并部署插件

1. 双击 `KingdeeSky.DevKit/lib/copy-kingdee-dlls.bat`，从星空站点 `WebSite\bin` 拷入 4 个 DLL；
2. 用 VS 打开 `KingdeeSky.DevKit/KingdeeSky.DevKit.sln` 重新生成 `KingdeeSky.Plugins`；
3. 在账套库执行 `KingdeeSky.DevKit/deploy/Scripts/T_SKY_AUDITLOG.sql`；
4. 按 `KingdeeSky.DevKit/deploy/部署与调试指南.md` 在 BOS 设计器中注册插件。

---

## 规模与能力

- **8 个服务端插件类**，覆盖三个递进场景：标准单据采购订单（表单 / 保存校验 / 审核操作三类插件构成完整防线）、
  从零新建自定义单据差旅费报销单 `SKY_ExpReimburse`、下推转换与反写。
- **62 条零依赖用例**，分五组（鉴权 / 密钥来源 / 配置分层 / 客户端调用链 / 传输安全），构成构建门禁。
- **零金蝶 DLL 依赖的客户端**：`HttpWebRequest` 直接 POST 内置 `.kdsvc` 端点，`CookieContainer` 维持会话，
  覆盖登录、查询、保存、提交、审核、反审核、删除、查看、下推与任意端点调用。
- **一条构建链**：明文密钥静态扫描 → 编译 → 测试门禁 → 版本化打包，产出 ZIP 至 `artifacts/`。
- **运维与安全文档**：部署调试、自定义单据开发、单据转换与反写、鉴权降级与传输安全、安全审计清单、
  SLA 与备份恢复、多环境发布与变更管理、回归测试方案、回滚预案。
- **一次真机验证**：操作台由 headless Chrome + CDP 脚本实测通过（`webapi-console/tools/cdp-verify.mjs`，
  62 项断言全绿，证据留档于 `webapi-console/tools/evidence/`）。

## 技术栈与版本基线

| 项 | 值 |
|---|---|
| 星空基线 | 经典星空 V9.1（K/3 Cloud 血统） |
| 目标框架 | .NET Framework 4.8，IDE 为 VS2019/2022 |
| 插件基类 | `AbstractBillPlugIn` / `AbstractValidator` / `AbstractOperationServicePlugIn` / `AbstractConvertPlugIn` / `AbstractBusinessFlowServicePlugIn` |
| 操作台运行时 | React 18.3.1 + antd 5.29.3 + `@baidu/qianfan-antd-kit` 0.1.0（importmap + text/babel，离线构建） |
| 模拟服务 | Python 3 标准库，无第三方依赖 |
| 数据库脚本 | SQL Server 语法（审计表） |

## 各区域的详细文档

- 示例包总览与全部使用说明：[`KingdeeSky.DevKit/README.md`](KingdeeSky.DevKit/README.md)
- 贡献与分支/提交/发布约定：[`KingdeeSky.DevKit/CONTRIBUTING.md`](KingdeeSky.DevKit/CONTRIBUTING.md)
- 演示包使用说明：[`KingdeeSky.WebApiClient/README.md`](KingdeeSky.WebApiClient/README.md)
- 测试用例执行说明：[`KingdeeSky.DevKit/tests/README.md`](KingdeeSky.DevKit/tests/README.md)
- 需从星空环境拷贝的 DLL 清单：[`KingdeeSky.DevKit/lib/README.md`](KingdeeSky.DevKit/lib/README.md)

---

## 合规声明

- 本仓库为**自主编写的学习示例**，不包含、不分发任何金蝶软件的二进制程序、源码、元数据、图标或文档原文。
- 「金蝶」「金蝶云·星空」「K/3 Cloud」等是金蝶国际软件集团的商标 / 产品名；`Kingdee.BOS.*` 程序集的著作权归金蝶所有，
  需从**已合法授权**的环境获取，仅限授权范围内开发使用，不得反编译、反向工程或用于破解许可。
- 示例代码可自由用于学习与自有星空环境的二次开发，按「现状」提供，不含任何担保。

## 已知边界与注意事项

1. **别和苍穹搞混**：本仓库是星空 C#（`Kingdee.BOS.*`）；网上 `import kd.bos.*` 的 Java 代码属于苍穹 / 星瀚，两者类名撞名但完全不通用。
2. **界面插件可被绕过**（WebAPI / 引入不经过界面），关键规则必须落在校验器或操作插件等服务端逻辑里。
3. **DLL 版本必须与目标环境补丁一致**，升级补丁后需重新拷贝并编译。
4. **生产必须收紧传输安全**：客户端代码中 `AllowInsecureHttp` 默认为 `false`（强制 HTTPS），
   但仓库自带的 `App.config` / `App.config.example` 为便于本地 mock 联调仍写为 `true`，投产前须改回 `false`。
5. **反写插件中的规则 GUID 是占位符**，必须替换为账套内真实反写规则 GUID 后才生效。
6. **L3 / L4 级验证需要外部条件**：授权星空 V9.1 测试账套、与补丁匹配的 BOS DLL，本仓库无法自带。
7. **CI 定义尚未迁到仓库根**：流水线文件位于 `KingdeeSky.DevKit/.github/workflows/ci.yml`，
   而 GitHub Actions 只读取仓库根目录下的 `.github/workflows/`，因此当前 push 不会自动触发构建；
   如需恢复流水线，需将其移至根目录并给脚本路径补上 `KingdeeSky.DevKit/` 前缀。

## 官方资料入口（公开免费）

- 产品帮助手册：<https://help.open.kingdee.com>
- WebAPI 手册：<https://help.open.kingdee.com/dokuwiki/doku.php?id=webapi>
- 开发者社区：<https://vip.kingdee.com>
- 开发者中心：<https://developer.kingdee.com>
- 补丁下载：<https://download.kdcloud.com>