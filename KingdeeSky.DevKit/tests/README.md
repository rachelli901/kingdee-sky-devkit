# L1 代码级用例执行脚本（tests/run-cases.*）

`run-cases.ps1` / `run-cases.bat` 用于**逐条 / 按分组**执行 L1 代码级回归的 79 条编号用例（`TC-L1-001` … `TC-L1-079`），与当前包含 81 个测试的 `run-tests.ps1` / `run-tests.bat`（全量门禁）互补：

- `run-tests.bat`：编译后全量运行 81 条，是 `build.bat` 一键构建内置的门禁；
- `run-cases.bat`：编译一次后按需驱动单条 / 分组用例，便于缺陷定位与回归记录（`deploy/回归测试用例执行记录模板.md`）逐条取证。

> 编号沿革：初次基线为 62 条（`TC-L1-001` … `TC-L1-062`）；2026-10-29 加固轮新增 5 条，**追加编号**为 `TC-L1-063` … `TC-L1-067`（原有编号一律不变，避免破坏既有引用）；其后 2026-11-02 轮继续追加至 `TC-L1-079`。另有 2 条测试**刻意不编号**（保持 001–079 连续不被破坏），仍由 `run-tests` 全量门禁执行。

## 用法

在 `tests/` 目录下执行（`.bat` 与 `.ps1` 等价，二选一）：

| 命令 | 作用 |
| --- | --- |
| `run-cases.bat` | 运行全部 79 条（等价 `-All`） |
| `run-cases.bat -All` | 运行全部 79 条 |
| `run-cases.bat -Case TC-L1-001` | 按编号运行单条用例 |
| `run-cases.bat -Case AuthProviderTests.Password_Name` | 按完整方法名运行单条用例 |
| `run-cases.bat -Case Password_Name` | 按短方法名运行单条用例 |
| `run-cases.bat -Group A` | 按分组运行（A / B / C / D / E） |
| `run-cases.bat -List` | 打印 79 条编号→分组→方法名映射，不编译不运行 |

PowerShell 直调：`powershell -NoProfile -ExecutionPolicy Bypass -File .\run-cases.ps1 -Case TC-L1-001`

## 退出码

- `0`：所选用例全部通过；
- `1`：存在失败（门禁拦截）；
- `2`：参数错误 / 未知用例 / 未知分组（`-Case`、`-Group`、`-All` 互斥混用）**或出现未识别的开关/参数**（例如误写 `-Cases`）；
- `3`：未找到 csc.exe（.NET Framework 4.8 编译器）。

## 输出示例

逐条输出 `  [PASS] TC-L1-001 AuthProviderTests.Password_Name`，末尾汇总：

```
===== RUN-CASES SUMMARY: total=79 passed=79 failed=0 =====
ALL RUN-CASES PASSED
```

## 编号与分组对照

| 分组 | 测试类 | 编号范围 | 数量 |
| --- | --- | --- | --- |
| A | `AuthProviderTests` | TC-L1-001 … TC-L1-016 | 16 |
| B | `SecretProviderTests` | TC-L1-017 … TC-L1-031、TC-L1-063、TC-L1-068 … TC-L1-069 | 18 |
| C | `ConfigTests` | TC-L1-032 … TC-L1-040、TC-L1-070 … TC-L1-072 | 12 |
| D | `ClientTests` | TC-L1-041 … TC-L1-046、TC-L1-064、TC-L1-073 … TC-L1-076、TC-L1-078 … TC-L1-079 | 13 |
| E | `TransportSecurityTests` | TC-L1-047 … TC-L1-062、TC-L1-065 … TC-L1-067、TC-L1-077 | 20 |

合计 **79** 条。完整清单见 `run-cases.ps1` 头部注释或 `-List` 输出；逐条执行记录模板见 `deploy/回归测试用例执行记录模板.md`。
