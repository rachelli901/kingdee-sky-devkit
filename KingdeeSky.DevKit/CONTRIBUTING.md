# 贡献与版本管理约定

本文件定义 KingdeeSky.DevKit 的版本控制、分支、提交与发布规范，是工程基线（阶段一）的一部分。
所有改动必须通过本契约，方可进入版本化交付包。

---

## 1 版本控制基线

- 仓库根目录为 `KingdeeSky.DevKit/`，与解决方案 `KingdeeSky.DevKit.sln` 同级。
- 忽略规则见 `.gitignore`：**密钥类文件与本地配置严禁入库**；行尾与二进制约定见 `.gitattributes`。
- 首次接入或行尾规则变更后执行一次：`git add --renormalize .`

## 2 分支策略

| 分支 | 用途 | 规则 |
| --- | --- | --- |
| `main` | 唯一长期分支，始终可构建、可测试、可打包 | 受保护；禁止直接推送，须经合并请求评审 |
| `feature/<短描述>` | 新功能、能力扩展 | 从 `main` 切出，完成后合并回 `main` |
| `fix/<短描述>` | 缺陷修复 | 同上 |
| `hotfix/<短描述>` | 生产事故紧急修复 | 从对应版本标签切出，修复后同时回合 `main` 并补发补丁版本 |

约定：

- 一个分支只做一件事，分支名用小写英文与连字符，不使用中文分支名。
- 合并前必须本地跑通 `tests\run-tests.bat`（当前基线 62 项全部通过），并保证 `build.bat` 可产出包；
  定位单条失败时用 `tests\run-cases.bat -Case TC-L1-0xx`（用例编号范围见 `tests/README.md`）。
- 禁止向已发布标签（`vX.Y.Z`）提交任何改动；变更须走新版本。

## 3 提交信息规范

采用 Conventional Commits，格式：`<type>(<scope>): <subject>`

| type | 含义 |
| --- | --- |
| `feat` | 新增能力 |
| `fix` | 缺陷修复 |
| `docs` | 文档变更 |
| `refactor` | 重构（不改变外部行为） |
| `test` | 测试相关 |
| `build` | 构建 / 打包 / 依赖 |
| `ci` | 持续集成配置 |
| `chore` | 杂项维护 |

- `scope` 取模块名，如 `client` / `plugins` / `security` / `deploy` / `eng`。
- `subject` 用祈使句、不超过 50 字符，说明“为什么”优先于“做了什么”。
- 一次提交只承载一个逻辑变更；禁止把格式化与功能改动混在一次提交。

## 4 版本策略

- 语义化版本 `MAJOR.MINOR.PATCH`，单一真源为 `eng/version.txt`；构建脚本从该文件读取版本号。
- 版本号递进规则：

| 变更性质 | 递进 | 示例 |
| --- | --- | --- |
| 破坏性变更（配置键、接口、部署方式不兼容） | `MAJOR` | 1.0.0 → 2.0.0 |
| 向后兼容的新能力 | `MINOR` | 1.0.0 → 1.1.0 |
| 向后兼容的缺陷修复 | `PATCH` | 1.0.0 → 1.0.1 |

- 每次发布在 `main` 上打标签 `vX.Y.Z`，标签指向该版本的发布提交。
- 星空版本与运行时基线锁定在 `lib/README.md` 与工程注释中（当前为星空 V9.1 / .NET Framework 4.8）。

## 5 发布流程

1. 确认 `main` 上待发布提交均已通过 `build.bat`（编译 → 测试 → 打包）。
2. 更新 `eng/version.txt` 至目标版本并提交（`build: bump version to X.Y.Z`）。
3. 运行 `build.bat`，在 `artifacts/` 得到 `KingdeeSky.DevKit-<版本>-<时间戳>.zip`。
4. 在发布提交上打标签：`git tag -a vX.Y.Z -m "release vX.Y.Z"`。
5. 按 `deploy/部署与调试指南.md` 与 `deploy/回滚预案.md` 执行部署与回滚准备。
6. 如启用 CI，推送标签触发 `.github/workflows/ci.yml` 产出构件。

## 6 本机受限说明

- `src/KingdeeSky.Plugins` 依赖金蝶 BOS 程序集（商业软件，不可分发），在**无授权星空环境**的机器上无法编译；
  在授权环境执行 `lib/copy-kingdee-dlls.bat` 取得 DLL 后，`build.bat` 会自动纳入 Plugins 编译。
- 上述边界不改变分支与发布契约，但影响“哪些产物可在何环境构建”，须在交付说明中如实标注。