# SleepRunner

SleepRunner 是一个 Windows 桌面自动化工具。它基于截图、OCR、固定区域、颜色/几何判断与 Win32 输入模拟，围绕跑马流程提供可维护的自动化核心，同时配套 WinForms 控制台、CLI 调试命令与基础监督脚本。

## 项目定位

- 面向真实游戏窗口的桌面自动化，而不是注入式修改
- GUI 与 CLI 共用同一套自动化核心，便于日常使用与问题排查
- 以页面 Handler 为中心拆分复杂流程，避免单文件状态机失控
- 以 `assets/events`、`assets/cards`、`assets/trade`、`assets/training` 四套 JSON profile 驱动策略
- 训练决策可通过 GUI 的 rule cards 配置
- 以日志、快照和 watchdog 脚本支撑长时间运行与问题复盘

## 源码版说明

- 本分支是纯源码版本，不提供打包产物、安装器或 release 压缩包
- 仓库不包含游戏素材、模板图片或第三方专有资源
- 页面识别依赖 OCR、固定区域、颜色/几何判断，而不是本地模板图
- 授权许可见 [LICENSE](./LICENSE)；免责声明见 [DISCLAIMER.md](./DISCLAIMER.md)

## 免责声明

SleepRunner 是非官方桌面自动化工具，与任何目标应用的开发商、发行商、平台方或相关权利方无关联，也未获得其认可、赞助或授权。

使用本项目可能受到游戏服务条款、平台规则、活动规则或其他协议限制。使用者需要自行确认并承担使用、修改、运行本软件产生的全部风险，包括但不限于账号处罚、数据损失、误操作、游戏不稳定或自动化行为带来的其他后果。

本项目不修改游戏文件，不向游戏进程注入代码，也不绕过技术保护措施。代码按现状提供，不承诺安全性、可用性、正确性或适用于特定用途。

## 当前能力

- 跑马主流程自动化：事件、训练、交易、委托、战斗、主菜单分流
- WinForms 控制台：启动、停止、参数调节、profile 切换、状态展示
- CLI 调试工具：截图、OCR、事件/训练/交易专项探针
- 运行时配置持久化：阈值、速度、build 方向、窗口位置、profile 选择
- 基础监督能力：会话日志、手动快照、`watch-race.ps1` 看门狗

## 技术栈

- C# 12 / .NET 8
- WinForms
- OpenCvSharp
- Windows.Media.Ocr
- Win32 GDI / SendInput / PInvoke

## 仓库结构

```text
.
├── src/                    # 应用源码
│   ├── Automation/         # 自动化上下文、控制器、任务抽象
│   ├── Capture/            # 截图
│   ├── Cli/                # CLI 分发与命令
│   ├── Forms/              # WinForms UI
│   ├── Input/              # 鼠标与键盘模拟
│   ├── Recognition/        # OCR
│   ├── Utils/              # 日志、路径、窗口、用户设置
│   └── Vision/             # 单帧缓存与识别上下文
├── assets/                 # 源码资产：事件、卡片、交易、训练 profile
├── scripts/                # 一键启动与监督脚本
├── docs/                   # 辅助文档
├── ARCHITECTURE.md         # 架构说明
└── CONTRIBUTING.md         # 开发与提交流程
```

## 快速开始

### 环境要求

- Windows 10/11 x64
- .NET 8 SDK
- 可交互桌面上的目标窗口

### 构建

```powershell
dotnet build SleepRunner.sln -c Debug -p:Platform=x64
```

### 启动 GUI

```powershell
dotnet run --project .\src\SleepRunner.csproj
```

或使用脚本：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-ui.ps1
```

点击“开始”时，SleepRunner 会自动选择可见的 Unity 目标窗口。如果桌面上有多个候选窗口，可以在同一个终端配置目标进程名来覆盖自动选择：

```powershell
$env:SLEEPRUNNER_TARGET_PROCESS = "TargetProcessName"
```

### 常用 CLI

```powershell
dotnet run --project .\src\SleepRunner.csproj -- --test
dotnet run --project .\src\SleepRunner.csproj -- --test-training
dotnet run --project .\src\SleepRunner.csproj -- --race-decide-once
dotnet run --project .\src\SleepRunner.csproj -- --race-probe-move-once
dotnet run --project .\src\SleepRunner.csproj -- --race-auto
dotnet run --project .\src\SleepRunner.csproj -- --snapshot
```

## 配置与运行时文件

- 仓库根 `assets/` 保存源码资产，并在构建时复制到输出目录
- 运行时统一以可执行文件目录为根解析路径，而不是以仓库根为根
- `user_settings.json`、`latest.log`、监督快照等运行时文件写入 `src/bin/<platform>/<configuration>/<tfm>/assets/...`
- 当前启用的策略 profile 来自：
  - `assets/events/<profile>.json`
  - `assets/cards/<profile>.json`
  - `assets/trade/<profile>.json`
  - `assets/training/<profile>.json`

`--test-training` 是训练调试命令，会打印训练上下文与命中规则结果，不会点击底部 Train 按钮，但仍会在训练扫描流程中点击各行以采样页面状态。

## 文档索引

- [免责声明](./DISCLAIMER.md)
- [架构说明](./ARCHITECTURE.md)
- [贡献指南](./CONTRIBUTING.md)
- [监督使用说明](./docs/SUPERVISION_USAGE.md)

## 当前状态

项目仍在持续迭代，但主干已经完成分层重构，当前重点是提升页面识别稳定性、策略可配置性与运行期诊断能力。

目前仍需注意：

- Windows only
- 依赖真实窗口截图和 OCR，天然会受分辨率、DPI、界面样式影响
- 当前验证方式以 `dotnet build`、单元测试、CLI 探针和实机运行日志为主
