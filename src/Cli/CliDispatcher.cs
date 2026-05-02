using SleepRunner.Utils;

namespace SleepRunner.Cli;

/// <summary>
/// CLI 命令分发器：根据 args[0] 路由到对应 ICliCommand
/// </summary>
public sealed class CliDispatcher
{
    private readonly Dictionary<string, ICliCommand> _commands = new(StringComparer.Ordinal);

    /// <summary>
    /// 注册一条命令；同一实例可重复注册多个别名（如各 race-* 共用一个实现）
    /// </summary>
    public CliDispatcher Register(ICliCommand command, params string[] aliases)
    {
        _commands[command.Name] = command;
        foreach (var alias in aliases)
        {
            if (!string.IsNullOrEmpty(alias))
                _commands[alias] = command;
        }
        return this;
    }

    /// <summary>
    /// 当前 args[0] 是否命中某条已注册命令
    /// </summary>
    public bool TryResolve(string[] args, out ICliCommand command)
    {
        command = null!;
        if (args.Length == 0)
            return false;
        return _commands.TryGetValue(args[0], out command!);
    }

    /// <summary>
    /// 当前命令是否为临时命令（写日志策略不同）
    /// </summary>
    public bool IsEphemeral(string[] args)
    {
        return TryResolve(args, out var cmd) && cmd is IEphemeralCliCommand;
    }

    /// <summary>
    /// 执行命令；未命中时返回 -1
    /// </summary>
    public async Task<int> ExecuteAsync(string[] args)
    {
        if (!TryResolve(args, out var command))
        {
            Logger.Log($"[Cli] Unknown command: {(args.Length > 0 ? args[0] : "(none)")}");
            return -1;
        }

        try
        {
            return await command.ExecuteAsync(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: command '{command.Name}' failed: {ex.Message}");
            Logger.Log($"[Cli] Command '{command.Name}' threw: {ex}");
            return -2;
        }
    }

    /// <summary>
    /// 注册当前所有内置命令（与重构前 Program.cs 行为等价）
    /// </summary>
    public static CliDispatcher CreateDefault()
    {
        var d = new CliDispatcher();
        d.Register(new Commands.TestCommand());
        d.Register(new Commands.CountIconsCommand());
        d.Register(new Commands.OcrCommand());
        d.Register(new Commands.ProbeEventYCommand());
        d.Register(new Commands.TestEventCommand());
        d.Register(new Commands.OcrCardsCommand());
        d.Register(new Commands.ClickPosCommand());
        d.Register(new Commands.ScrollCommand());
        d.Register(new Commands.SnapshotCommand());
        d.Register(new Commands.EventHoverScanCommand());
        d.Register(new Commands.TestTrainingCommand());
        d.Register(new Commands.ProbeFailRateCommand());
        d.Register(new Commands.ProbePowerCommand());
        d.Register(new Commands.DebugTradeCommand());
        d.Register(new Commands.DebugTradeFlowCommand());
        d.Register(new Commands.DebugTradeHotkeysCommand());
        d.Register(new Commands.LocateCommissionSkipCommand());
        // 跑马 CLI 共用一个实现，按名称区分模式
        var raceRun = new Commands.RaceRunCommand("--race");
        var raceStep = new Commands.RaceRunCommand("--race-step");
        var raceStepOnce = new Commands.RaceRunCommand("--race-step-once");
        var raceDecideOnce = new Commands.RaceRunCommand("--race-decide-once");
        var raceAuto = new Commands.RaceRunCommand("--race-auto");
        var raceProbe = new Commands.RaceRunCommand("--race-probe-move-once");
        d.Register(raceRun)
         .Register(raceStep)
         .Register(raceStepOnce)
         .Register(raceDecideOnce)
         .Register(raceAuto)
         .Register(raceProbe);
        return d;
    }
}
