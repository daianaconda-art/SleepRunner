using System.Windows.Forms;
using SleepRunner.Cli;
using SleepRunner.Utils;

namespace SleepRunner;

/// <summary>
/// 应用入口：日志初始化 → CLI 分发；无参数 / --ui 时启动 GUI
/// </summary>
internal static class Program
{
    private static readonly CliDispatcher Dispatcher = CliDispatcher.CreateDefault();

    [STAThread]
    static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
        InitializeLogging(args);

        if (Dispatcher.TryResolve(args, out var command))
        {
            return command.ExecuteAsync(args).GetAwaiter().GetResult();
        }

        // 默认入口（无参数 或 --ui）：启动跑马 Race Console
        if (args.Length > 0 && args[0] != "--ui")
        {
            Console.WriteLine($"ERROR: Unknown command: {args[0]}");
            return -1;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Forms.RaceMainWindow());
        return 0;
    }

    /// <summary>
    /// 启动会话日志：GUI 引导 → gui_boot；CLI → cli_&lt;command&gt;；snapshot 等临时命令不写持久日志
    /// </summary>
    private static void InitializeLogging(string[] args)
    {
        string sessionName;
        if (args.Length == 0)
        {
            sessionName = "gui_boot";
        }
        else
        {
            string command = args[0].TrimStart('-').Replace('-', '_');
            sessionName = $"cli_{command}";
        }

        bool persistent = !Dispatcher.IsEphemeral(args);
        Logger.StartSession(
            sessionName,
            writeSessionFile: persistent,
            writeLatestFile: persistent);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Logger.EndSession();
    }
}
