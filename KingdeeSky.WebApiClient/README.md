# KingdeeSky.WebApiClient Demo —— 无金蝶环境也能跑起来的 WebAPI 对接演示

本项目在**没有金蝶云·星空程序**的前提下，用本地 Python 模拟服务复刻星空 WebAPI 的
9 个 `.kdsvc` 端点行为，让 `KingdeeSky.DevKit` 中的 WebApiClient 客户端**真实编译、真实运行、
完整跑通**：登录 → 查询 → 建单 → 提交 → 审核 → 查看（以及反审核 / 删除 / 下推）。

> 说明：这是**协议级模拟**，不是金蝶官方实现，也不包含金蝶任何二进制。
> 业务校验、审批流、反写、权限等企业逻辑不在模拟范围内。
> 客户端源码**只有一份**，位于 `KingdeeSky.DevKit/src/KingdeeSky.WebApiClient/`；本目录的 `app/`
> 是**构建输出**（已加入 `.gitignore`，不入库），由 `build-demo.ps1` 现场编译生成。
> 接真实环境时，只需改 `KingdeeSky.DevKit/src/KingdeeSky.WebApiClient/App.config` 的站点与账套，
> 客户端代码零改动。

---

## 一、快速开始（Windows）

**前置条件**：Python 3（`python` 可用）、PowerShell、.NET Framework 4.8（Win10/11 系统自带）。

方式一（推荐）：双击 `run-demo.bat`，脚本依次做三件事：
1. 调用 `build-demo.ps1`，用 .NET Framework 的 `csc` 从 DevKit 源码编译客户端到 `app/`；
2. 检测端口 18080，未占用则启动本地模拟服务（新窗口）；
3. 打开客户端菜单程序。

方式二（Git Bash / 终端）：
```bash
./run-demo.sh
```

**登录口令不写在仓库任何文件里**，模拟服务与客户端共用同一个值，按以下优先级解析：

1. 环境变量 `KINGDEE_PASSWORD`（你想固定口令时先自己设置）；
2. 本目录下 `.demo-credential`（已有 `.gitignore` 规则忽略，供本机多次运行复用）；
3. 前两者都没有时，由 `mock_server.py` 现场随机生成并写入 `.demo-credential`。

客户端菜单（与原 `KingdeeSky.DevKit` 示例完全一致）：
```
1 = 查询最近 10 张采购订单
2 = 新建一张采购订单（保存为草稿）
3 = 对刚创建的单据执行 提交 + 审核
4 = 一键全流程（查询 → 建单 → 提交 → 审核 → 查看）
5 = 新建自定义单据：差旅费报销单 SKY_ExpReimburse
0 = 退出
```

建议先选 `4` 体验完整链路；`1` 再查一次能看到刚才建的单已变为「已审核（C）」状态。

---

## 二、目录结构

```
KingdeeSky.WebApiClient/
├── mock_server.py          # 金蝶 WebAPI 本地模拟服务（仅 Python 3 标准库，无第三方依赖）
├── build-demo.ps1          # 从 DevKit 源码编译客户端到 app/（构建入口）
├── run-demo.bat            # Windows 一键：构建 → 起模拟服务 → 跑客户端（双击即可）
├── run-demo.sh             # Git Bash / POSIX 一键：同上
├── README.md               # 本说明
├── .demo-credential        # 本机口令（首次运行时生成，已被 .gitignore 忽略，不入库）
└── app/                    # 构建输出目录（不入库），由 build-demo.ps1 生成
    ├── KingdeeSky.WebApiClient.exe        # 编译产物（.NET Framework 4.8）
    ├── KingdeeSky.WebApiClient.exe.config # 由 DevKit 的 App.config 复制而来
    ├── App.config.example                 # 配置模板
    └── secrets.env.example                # 密钥环境变量模板
```

`app/` 目录由构建脚本生成，仓库中**不保存**它的任何内容；客户端源码以
`KingdeeSky.DevKit/src/KingdeeSky.WebApiClient/` 为唯一真源。

---

## 三、模拟服务说明

启动：`python mock_server.py [port]`（默认端口 **18080**）。

模拟账号：用户名 `Administrator`；口令按上文优先级解析（不写死在源码里），启动日志会打印本次生效的口令。

覆盖端点（路径与 `KingdeeWebApiClient.cs` 常量一一对应）：

| 端点 | 行为 |
| --- | --- |
| AuthService.**ValidateUser** | 账号密码校验；成功下发会话 Cookie（客户端 CookieContainer 自动维持） |
| DynamicFormService.**Save** | 建单：自动分配内码与单据编号，模拟星空带出基础资料名称，状态=草稿(A) |
| DynamicFormService.**Submit** | 提交：状态 A → B |
| DynamicFormService.**Audit** | 审核：状态 B → C |
| DynamicFormService.**UnAudit** | 反审核：状态 C → B |
| DynamicFormService.**Delete** | 删除：草稿/B 状态可删；已审核(C)拒绝，需先反审核 |
| DynamicFormService.**View** | 按内码返回单据完整数据包 |
| DynamicFormService.**ExecuteBillQuery** | 按 FieldKeys 返回二维数组，支持 `基础资料.属性` 嵌套字段 |
| DynamicFormService.**Push** | 下推：按 TargetFormId 生成一张目标单（默认 STK_InStock） |

预置 3 张采购订单（PO20261018001/19001/20001），启动即有查询结果。

---

## 四、换成真实金蝶环境

1. 编辑 `KingdeeSky.DevKit/src/KingdeeSky.WebApiClient/App.config`（这是唯一配置源，构建时复制为
   `app\KingdeeSky.WebApiClient.exe.config`）：
   - `ServerUrl` → 你的星空站点，如 `https://xxx.kdcloud.com/` 或 `http://应用服务器IP/K3Cloud/`；
   - `AcctId` → 数据中心（账套）ID；
   - `OrgNumber / SupplierNumber / MaterialNumber / BillTypeNumber` → 账套内真实基础资料编号；
   - 需要时改 `AuthMode`（`password` / `oauth2` / `appsec`）与 `SecretStore`（`env` / `file` / `dpapi` / `kms`）；
   - 生产环境把 `AllowInsecureHttp` 改回 `false`（强制 HTTPS）。
2. 口令等密钥**不写进配置文件**，而是交给密钥源，例如：
   - 环境变量：`set KINGDEE_PASSWORD=<你的口令>`（PowerShell：`$env:KINGDEE_PASSWORD="<你的口令>"`），
     键名与配置里的 `PasswordKey` 对应；其他密钥同理（`KINGDEE_CLIENT_ID` / `KINGDEE_CLIENT_SECRET` 等）；
   - 或改用 `SecretStore=file` / `dpapi` / `kms`，把密钥放到仓库之外的文件或密钥服务中，
     模板见 `App.config.example` 与 `secrets.env.example`。
3. 重新运行 `run-demo.bat`（会重新编译并把 App.config 复制到 `app/`）。

> 写操作的返回结构（`Result.ResponseStatus.IsSuccess / SuccessEntitys`）与真实星空一致，
> 客户端解析逻辑无需调整。

---

## 五、重新编译客户端（可选）

`app/` 是构建输出，每次运行 `run-demo` 都会重新编译；也可单独构建：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-demo.ps1
```

（也可用 Visual Studio / MSBuild 打开原工程 `KingdeeSky.DevKit.sln` 编译 WebApiClient 工程。）

---

## 六、验证记录（本演示包自测通过）

- 登录成功/失败（错误密码被拒）
- 查询：3 张预置采购订单，嵌套字段（FSupplierId.FName）正确
- 建单 → 提交 → 审核 → 查看：内码/编号/状态正确，基础资料名称自动带出
- 反审核 → 删除成功；删除已审核单据被拒（提示先反审核）
- 下推：采购订单 → 采购入库单（STK_InStock），目标单可查询到
- 未知端点返回 404；响应头顺序合规（.NET HttpWebRequest 严格解析通过）
- 从源码构建 → 起模拟服务 → 跑完整链路（`login/query/save/submit/audit/view`）在本机复跑通过

---

## 七、免责声明

- 本项目为学习/演示用途，非金蝶官方输出；`mock_server.py` 只是按公开的 WebAPI 调用形态复刻协议行为。
- 不包含金蝶任何 DLL/二进制；从未授权环境获取的金蝶组件请遵守相应授权与合规要求。

## 八、作者与署名

- **作者**：rachelli901（<https://github.com/rachelli901>）
- **邮箱**：lir30481@gmail.com
- **仓库**：<https://github.com/rachelli901/kingdee-sky-devkit>

本仓库中的示例代码、测试、脚本、文档与 HTML 成品均由作者编写；第三方依赖与金蝶厂商资料的权属见上方「免责声明」。