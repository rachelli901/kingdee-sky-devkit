# lib 目录说明（Kingdee.BOS.* DLL）

本目录用于存放编译插件工程所需的金蝶 BOS 程序集。**这些 DLL 是金蝶商业软件的组成部分，
受金蝶软件许可协议保护，不能随本示例代码分发**，请从你自己拥有合法授权的星空应用服务器上拷贝。

> **版本锁定**：本仓库锁定 **金蝶云·星空 V9.1（.NET Framework 4.8）**。插件与星空版本强绑定，
> 请拷贝与目标环境补丁版本一致的 DLL；升级环境补丁后重新拷贝、重新编译，勿混用其它版本程序集。

## 需要的 DLL（共 4 个）

| DLL | 作用 |
|---|---|
| `Kingdee.BOS.dll` | BOS 内核：Context、工具类、HotFix 特性等 |
| `Kingdee.BOS.Core.dll` | 插件基类、事件参数、元数据（表单/单据/操作插件都在这里） |
| `Kingdee.BOS.App.dll` | 服务端数据访问：DBServiceHelper、SqlParam、KDDbType |
| `Kingdee.BOS.Orm.dll` | ORM 实体：DynamicObject、DynamicObjectCollection |

## 从哪里拷贝

星空应用服务器上的站点 bin 目录，常见路径：

- `C:\Program Files (x86)\Kingdee\K3Cloud\WebSite\bin\`
- `C:\Program Files\Kingdee\K3Cloud\WebSite\bin\`
- IIS 自定义部署路径下的 `K3Cloud\bin\`（在 IIS 管理器中查看站点物理路径）

> 要求：DLL 版本必须与你将来部署/调试的目标环境补丁版本一致，否则可能出现元数据不一致、
> 类型加载失败等问题。升级环境补丁后，请重新拷贝并重新编译插件。

## 拷贝方式（任选其一）

1. **脚本方式（推荐）**：双击本目录下的 `copy-kingdee-dlls.bat`，按提示粘贴站点 bin 目录路径，
   脚本会自动校验并复制 4 个 DLL 到本目录。
2. **手动方式**：直接把上述 4 个 DLL 复制到本目录（与本文同级）。

> 工程引用配置：`KingdeeSky.Plugins.csproj` 通过 `$(KingdeeBinsDir)` 属性指向 BOS 程序集目录，
> 默认取本 `lib\` 目录；需要从其它位置引用时用
> `msbuild KingdeeSky.Plugins.csproj /p:KingdeeBinsDir="D:\K3Cloud\WebSite\bin"`
> 覆盖即可，无需改动工程文件（适合 CI 从授权环境注入路径）。

完成后用 Visual Studio 打开 `KingdeeSky.DevKit.sln`，`KingdeeSky.Plugins` 工程即可编译；
`KingdeeSky.WebApiClient` 工程不依赖任何金蝶 DLL，无需本步骤即可编译运行。
一键构建用根目录 `build.bat`（编译 WebApiClient → 跑测试门禁 → 检测到本目录 DLL 时追加编译 Plugins → 产出版本化安装包）。
