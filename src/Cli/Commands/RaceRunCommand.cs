using SleepRunner.Automation;
using SleepRunner.Automation.Race;
using SleepRunner.Capture;
using SleepRunner.Utils;

namespace SleepRunner.Cli.Commands;

/// <summary>
/// 跑马 CLI 入口，按命令名映射成不同 RaceRunner 模式：
/// --race                    : 普通自动跑（无单步、无退出条件）
/// --race-step               : 单步模式（每个 handler 后等待 Enter）
/// --race-step-once          : 单步 + 处理完一个画面后退出
/// --race-decide-once        : 只决策不点击，处理一帧即退出
/// --race-auto               : AUTO 模式（自动执行已知决策、跳过未知）
/// --race-probe-move-once    : 只移动鼠标不点击，处理一帧即退出
/// </summary>
internal sealed class RaceRunCommand : ICliCommand
{
    public string Name { get; }

    public RaceRunCommand(string name)
    {
        Name = name;
    }

    public async Task<int> ExecuteAsync(string[] args)
    {
        CliBootstrap.EnsureUtf8Console();
        var settings = UserSettings.Load();
        settings.ApplyToRaceConfig();
        var presets = ResolvePresets();

        Console.WriteLine("=== SleepRunner CLI Race Mode ===");
        Console.WriteLine($"Base dir: {PathHelper.BaseDir}");
        Console.WriteLine($"Profiles: events={settings.EventsProfile}, cards={settings.CardsProfile}, trade={settings.TradeProfile}, training={settings.TrainingProfile}");
        if (presets.AutoMode)
            Console.WriteLine("AUTO mode: ON (auto-execute known decisions, skip unknowns, keep probing)");
        if (presets.StepMode)
            Console.WriteLine("Step mode: ON (press Enter after each handler to continue)");
        if (presets.ExitAfterFirstHandledStep)
            Console.WriteLine("Once mode: will stop after first handled screen");
        if (presets.DecisionOnly)
            Console.WriteLine("Decision-only mode: detect and print decision, no click");
        if (presets.ProbeMoveOnly)
            Console.WriteLine("Probe-move mode: detect and move cursor only, no click");

        var hWnd = CliBootstrap.FindGameOrLogError();
        if (hWnd == IntPtr.Zero)
            return 1;

        using var capture = new BitBltCapture();
        capture.Start(hWnd);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("Cancelling race task...");
        };

        IRaceStepGate? gate = presets.StepMode ? new ConsoleRaceStepGate() : null;
        using var ctx = new GameContext(hWnd, capture, cts.Token);
        var runner = new RaceRunner(
            stepGate: gate,
            exitAfterFirstHandledStep: presets.ExitAfterFirstHandledStep,
            decisionOnly: presets.DecisionOnly,
            probeMoveOnly: presets.ProbeMoveOnly,
            autoMode: presets.AutoMode);
        try
        {
            await runner.RunAsync(ctx);
        }
        catch (RaceTaskCompletedException ex)
        {
            Console.WriteLine($"Race task completed: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Race task cancelled.");
        }

        Console.WriteLine("CLI race mode exit.");
        return 0;
    }

    private RacePresets ResolvePresets() => Name switch
    {
        "--race" => new RacePresets(),
        "--race-step" => new RacePresets { StepMode = true },
        "--race-step-once" => new RacePresets { StepMode = true, ExitAfterFirstHandledStep = true },
        "--race-decide-once" => new RacePresets { ExitAfterFirstHandledStep = true, DecisionOnly = true },
        "--race-auto" => new RacePresets { AutoMode = true },
        "--race-probe-move-once" => new RacePresets { ExitAfterFirstHandledStep = true, ProbeMoveOnly = true },
        _ => new RacePresets()
    };

    private sealed class RacePresets
    {
        public bool StepMode { get; init; }
        public bool ExitAfterFirstHandledStep { get; init; }
        public bool DecisionOnly { get; init; }
        public bool ProbeMoveOnly { get; init; }
        public bool AutoMode { get; init; }
    }
}
