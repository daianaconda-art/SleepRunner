using System.Diagnostics;
using OpenCvSharp;
using SleepRunner.Automation.Race;
using SleepRunner.Automation.Race.Handlers;
using SleepRunner.Capture;
using SleepRunner.Input;
using SleepRunner.Utils;

namespace SleepRunner.Automation;

/// <summary>
/// 自动化上下文，封装截屏→识别→操作的常用组合，供任务脚本调用
/// </summary>
public class GameContext : IDisposable
{
    private const int SlowCaptureThresholdMs = 120;
    private readonly IntPtr _hWnd;
    public IntPtr WindowHandle => _hWnd;
    private readonly BitBltCapture _capture;
    private readonly CancellationToken _token;

    /// <summary>与任务生命周期绑定的取消令牌（单步暂停时可检查）</summary>
    public CancellationToken CancellationToken => _token;

    public GameContext(IntPtr hWnd, BitBltCapture capture, CancellationToken token)
    {
        _hWnd = hWnd;
        _capture = capture;
        _token = token;
    }

    #region 操作类

    /// <summary>
    /// 点击窗口客户区的相对位置（百分比坐标 0.0~1.0）
    /// ClickAtClient 期望物理坐标，所以需要将逻辑尺寸乘以 DPI 缩放
    /// </summary>
    public async Task ClickAtPercent(double xPct, double yPct)
    {
        var sw = Stopwatch.StartNew();
        var (w, h) = WindowHelper.GetClientSize(_hWnd);
        float dpi = MouseSimulator.GetDpiScale();
        int logicalX = (int)(w * xPct);
        int logicalY = (int)(h * yPct);
        int x = (int)(w * dpi * xPct);
        int y = (int)(h * dpi * yPct);
        Log.Log(
            $"ClickAtPercent: pct=({xPct:F3},{yPct:F3}) => logical=({logicalX},{logicalY}), " +
            $"physical=({x},{y}) in client={w}x{h}, dpi={dpi:F2}");
        await MouseSimulator.ClickAtClient(_hWnd, x, y);
        sw.Stop();
        Log.Log($"ClickAtPercent: completed in {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// 点击屏幕中央（关闭弹窗/结算等通用操作）
    /// </summary>
    public async Task ClickCenter()
    {
        await ClickAtPercent(0.5, 0.5);
    }

    /// <summary>
    /// 在窗口客户区的相对位置滚动鼠标滚轮，负值向下滚
    /// ScrollAtClient 期望物理坐标，所以需要将逻辑尺寸乘以 DPI 缩放
    /// </summary>
    public async Task ScrollAtPercent(double xPct, double yPct, int clicks)
    {
        var sw = Stopwatch.StartNew();
        var (w, h) = WindowHelper.GetClientSize(_hWnd);
        float dpi = MouseSimulator.GetDpiScale();
        int logicalX = (int)(w * xPct);
        int logicalY = (int)(h * yPct);
        int x = (int)(w * dpi * xPct);
        int y = (int)(h * dpi * yPct);
        Log.Log(
            $"ScrollAtPercent: pct=({xPct:F3},{yPct:F3}) => logical=({logicalX},{logicalY}), " +
            $"physical=({x},{y}), clicks={clicks}, client={w}x{h}, dpi={dpi:F2}");
        await MouseSimulator.ScrollAtClient(_hWnd, x, y, clicks);
        sw.Stop();
        Log.Log($"ScrollAtPercent: completed in {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// 等待指定毫秒（内置取消检查）
    /// 全局倍率 RaceConfig.WaitMultiplier 会缩放传入的 ms：
    ///   - 倍率 0.5 → 等一半时间（更快但风险更高）
    ///   - 倍率 1.0 → 默认行为
    ///   - 倍率 2.0 → 等两倍时间（更慢更稳）
    /// 50ms 保底，避免极端配置导致直接跳过动画。
    /// </summary>
    public async Task Wait(int ms)
    {
        double mul = RaceConfig.WaitMultiplier;
        int actual = Math.Max(50, (int)Math.Round(ms * mul));
        if (Math.Abs(mul - 1.0) < 0.001)
            Log.Log($"Wait: {ms}ms");
        else
            Log.Log($"Wait: {actual}ms (base={ms}ms, x{mul:F2})");
        await Task.Delay(actual, _token);
    }

    /// <summary>
    /// 反复点击左上角返回，直到 OCR 检测到跑马主菜单
    /// </summary>
    public async Task<bool> NavigateToMain(int maxRetries = 8)
    {
        Log.Log("NavigateToMain: returning to main interface...");
        var totalSw = Stopwatch.StartNew();
        for (int i = 0; i < maxRetries; i++)
        {
            _token.ThrowIfCancellationRequested();

            using var shot = CaptureScreen();
            if (shot != null && !shot.Empty() &&
                MainMenuScreenChecks.IsMainMenuScreen(shot, out string summary))
            {
                Log.Log($"NavigateToMain: main menu detected after {i} back clicks ({summary}), elapsed={totalSw.ElapsedMilliseconds}ms");
                return true;
            }

            Log.Log($"NavigateToMain: clicking back button (attempt {i + 1}/{maxRetries})...");
            await ClickAtPercent(0.025, 0.035);
            await Wait(1500);
        }

        Log.Log($"NavigateToMain: FAILED - main interface not found after max retries, elapsed={totalSw.ElapsedMilliseconds}ms");
        return false;
    }

    #endregion

    #region 截屏

    /// <summary>
    /// 截取当前画面
    /// </summary>
    public Mat? CaptureScreen()
    {
        var sw = Stopwatch.StartNew();
        Mat? shot = _capture.Capture();
        sw.Stop();
        if (sw.ElapsedMilliseconds >= SlowCaptureThresholdMs)
        {
            string state = shot == null || shot.Empty() ? "empty" : $"{shot.Width}x{shot.Height}";
            Log.Log($"CaptureScreen: slow capture {sw.ElapsedMilliseconds}ms ({state})");
        }
        return shot;
    }

    /// <summary>
    /// 发送快捷键组合（如 Alt+1）
    /// </summary>
    public async Task SendHotkey(ushort modifier, ushort key)
    {
        var sw = Stopwatch.StartNew();
        Log.Log($"SendHotkey: modifier=0x{modifier:X2} + key=0x{key:X2}");
        await KeyboardSimulator.SendHotkey(_hWnd, modifier, key);
        sw.Stop();
        Log.Log($"SendHotkey: completed in {sw.ElapsedMilliseconds}ms");
    }

    public async Task<bool> SendGameAction(GameActionKey action)
    {
        var sw = Stopwatch.StartNew();
        Log.Log($"SendGameAction: action={action}");
        bool sent = await GameKeyboard.Default.SendActionAsync(_hWnd, action, _token);
        sw.Stop();
        Log.Log($"SendGameAction: action={action}, sent={sent}, completed in {sw.ElapsedMilliseconds}ms");
        return sent;
    }

    /// <summary>
    /// 检查是否已请求取消，供外部 Handler 调用
    /// </summary>
    public void CheckCancellation()
    {
        _token.ThrowIfCancellationRequested();
    }

    #endregion

    public void Dispose()
    {
    }
    private static readonly LogScope Log = new("Context");
}
