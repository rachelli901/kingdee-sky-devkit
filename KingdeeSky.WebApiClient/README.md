# KingdeeSky.WebApiClient Demo —— 无金蝶环境也能跑起来的 WebAPI 对接演示

本项目在**没有金蝶云·星空程序**的前提下，用本地 Python 模拟服务复刻星空 WebAPI 的
9 个 `.kdsvc` 端点行为，让 `KingdeeSky.DevKit` 中的 WebApiClient 客户端**真实编译、真实运行、
完整跑通**：登录 → 查询 → 建单 → 提交 → 审核 → 查看（以及反审核 / 删除 / 下推）。

> 说明：这是**协议级模拟**，不是金蝶官方实现，也不包含金蝶任何二进制。
> 业务校验、审批流、反写、权限等企业逻辑不在模拟范围内。
> 接真实环境时，只需把 `app/App.config` 改回正式站点地址与账套即可，客户端代码零改动。

---

## 一、快速开始（Windows）

**前置条件**：Python 3（`python` 可用）、.NET Framework 4.8（Win10/11 系统自带）。

方式一（推荐）：双击 `run-demo.bat`
- 自动检测端口 18080，未占用则启动模拟服务（新窗口），随后打开客户端菜单程序。

方式二（Git Bash / 终端）：
```bash
./run-demo.sh
```

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
KingdeeSky.WebApiClient-Demo/
├── mock_server.py          # 金蝶 WebAPI 本地模拟服务（仅 Python 3 标准库，无第三方依赖）
├── run-demo.bat            # Windows 一键运行（双击即可）
├── run-demo.sh             # Git Bash / POSIX 一键运行
├── README.md               # 本说明
└── app/                    # 演示客户端（由原示例源码编译，仅改了 App.config）
    ├── KingdeeSky.WebApiClient.exe        # 编译产物（.NET Framework 4.8）
    ├── KingdeeSky.WebApiClient.exe.config # 演示连接配置
    ├── Program.cs / KingdeeWebApiClient.cs / Scenarios.cs / Properties/AssemblyInfo.cs
    └── App.config          # 演示配置源文件（编译时复制为 *.exe.config）
```

`app/` 下四个 `.cs` 与 `KingdeeSky.DevKit/src/KingdeeSky.WebApiClient/` 完全一致，**未改动业务逻辑**，
仅 `App.config` 指向本地模拟服务。

---

## 三、模拟服务说明

启动：`python mock_server.py [port]`（默认端口 **18080**）。

模拟账号：`Administrator / demo123`

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

1. 打开 `app/App.config`（或 `app/KingdeeSky.WebApiClient.exe.config`）；
2. 修改：
   - `ServerUrl` → 你的星空站点，如 `https://xxx.kdcloud.com/` 或 `http://应用服务器IP/K3Cloud/`；
   - `AcctId` → 数据中心（账套）ID；
   - `Password` → 你的密码；
   - `OrgNumber / SupplierNumber / MaterialNumber / BillTypeNumber` → 账套内真实基础资料编号；
3. 重新运行 `KingdeeSky.WebApiClient.exe`。

> 写操作的返回结构（`Result.ResponseStatus.IsSuccess / SuccessEntitys`）与真实星空一致，
> 客户端解析逻辑无需调整。

---

## 五、重新编译客户端（可选）

`app/` 已内置编译好的 exe，一般无需重编。如需验证从源码构建：

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe ^
  /out:KingdeeSky.WebApiClient.exe ^
  /r:System.Web.Extensions.dll /r:System.Configuration.dll ^
  Program.cs KingdeeWebApiClient.cs Scenarios.cs Properties\AssemblyInfo.cs
copy App.config KingdeeSky.WebApiClient.exe.config
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

---

## 七、免责声明

- 本项目为学习/演示用途，非金蝶官方输出；`mock_server.py` 只是按公开的 WebAPI 调用形态复刻协议行为。
- 不包含金蝶任何 DLL/二进制；从未授权环境获取的金蝶组件请遵守相应授权与合规要求。