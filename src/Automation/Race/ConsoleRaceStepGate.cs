namespace SleepRunner.Automation.Race;

/// <summary>
/// 控制台单步：打印提示后在后台线程等待 Enter，便于无 GUI 下调试
/// </summary>
public sealed class ConsoleRaceStepGate : IRaceStepGate
{
    public async Task WaitForContinueAsync(string finishedStepSummary, CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine($"---------- RACE STEP PREVIEW ----------");
        Console.WriteLine(finishedStepSummary);
        Console.WriteLine("Press Enter to execute this step (Ctrl+C to cancel)...");

        // 输入被重定向时（例如 echo.|dotnet run）不能访问 KeyAvailable，回退到 ReadLine
        if (Console.IsInputRedirected)
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = Console.ReadLine();
            }, cancellationToken);
            return;
        }

        // 交互控制台模式：使用可取消轮询等待 Enter
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                    return;
            }

            await Task.Delay(50, cancellationToken);
        }
    }
}
