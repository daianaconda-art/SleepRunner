# Contributing

感谢你关注 SleepRunner。

这不是一个通用桌面自动化框架，而是一个围绕特定流程逐步演化出的自动化项目。提交前请先理解当前分层边界，再决定改动落点。

## 开发环境

- Windows 10/11 x64
- .NET 8 SDK
- PowerShell 5.1 或 PowerShell 7
- 可交互桌面上的游戏窗口

## 推荐流程

### 构建

```powershell
dotnet build SleepRunner.sln -c Debug -p:Platform=x64
```

### 启动 GUI

```powershell
dotnet run --project .\src\SleepRunner.csproj
```

### 常用调试命令

```powershell
dotnet run --project .\src\SleepRunner.csproj -- --test
dotnet run --project .\src\SleepRunner.csproj -- --race-decide-once
dotnet run --project .\src\SleepRunner.csproj -- --race-probe-move-once
dotnet run --project .\src\SleepRunner.csproj -- --debug-trade
dotnet run --project .\src\SleepRunner.csproj -- --snapshot
```

## 代码边界

### UI

- UI 改动优先落在 `src/Forms/` 与 `src/Forms/Controls/`
- UI 与自动化核心通过 `IRaceController` 交互
- 除 `Program.cs` 外，不要在 `Forms/` 之外引入 `System.Windows.Forms`

### 自动化业务

- 跑马主循环在 `src/Automation/Race/RaceRunner.cs`
- 页面逻辑在 `src/Automation/Race/Handlers/`
- 如果一个 Handler 变复杂，优先拆成同目录子模块，例如：
  - `XxxScreenChecks`
  - `XxxOcrRegions`
  - `XxxGeometry`
  - `XxxPolicy`
  - `XxxActions`

### 识别与输入

- 截图：`src/Capture/`
- OCR / 颜色几何识别：`src/Recognition/` 与对应页面 Handler 子模块
- 单帧缓存：`src/Vision/FrameContext.cs`
- 鼠标键盘模拟：`src/Input/`

### 路径与配置

- 所有运行时路径统一经由 `PathHelper`
- 不要假设当前工作目录就是仓库根
- 策略 profile 数据位于：
  - `assets/events/`
  - `assets/cards/`
  - `assets/trade/`

## 提交前检查

- `dotnet build SleepRunner.sln -c Debug -p:Platform=x64`
- 至少运行一个与改动相关的 CLI 探针或 UI 场景
- 如果改动影响文档、命令或目录结构，同步更新 `README.md` / `ARCHITECTURE.md` / `CONTRIBUTING.md`

## 日志与调试约定

- 新日志统一使用 `LogScope`
- 页面识别逻辑优先复用 `FrameContext`，避免同帧 OCR 重复计算
- 排查界面误判时，优先用 `--race-decide-once` 定位“是谁接管了当前页面”

## PowerShell 编码注意事项

PowerShell 5.1 默认读取 UTF-8 文件时存在编码坑。批量改写源码或文档时，不要依赖会重新解释编码的脚本式读写流程；优先使用可靠的编辑器或显式 UTF-8 方案。

## 文档分工

- `README.md`：公开首页
- `ARCHITECTURE.md`：公开架构说明
- `CONTRIBUTING.md`：公开开发流程
- `AGENT_HANDOFF.md`：内部维护手册
- `DEVNOTES.md`：内部历史记录与踩坑备忘

