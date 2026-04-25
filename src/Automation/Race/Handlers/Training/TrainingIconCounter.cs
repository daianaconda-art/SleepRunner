using OpenCvSharp;
using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Handlers.Training;

/// <summary>
/// 训练页右侧 5 行的圆形图标计数 + 基调优先级决策
///
/// 拆分意图：
/// - HSV 阈值是反复试出来的"魔法数"，集中在这里方便调
/// - ApplyPriorityRule 是纯决策函数，单元测试也容易
/// </summary>
internal static class TrainingIconCounter
{
    public const double IconCenterX = 0.73;
    public const double IconStartY = 0.13;
    public const double IconSpacing = 0.08;
    public const int MaxIconSlots = 8;
    public const double IconCheckRadius = 0.015;

    /// <summary>
    /// 调试开关：true 时把每个 slot 的取样区域落盘到 assets/screenshots/debug_icons/，
    /// 便于阈值标定（每次扫描会产生 ~5 张小图，正常使用建议关掉）
    /// </summary>
    public static bool DebugDumpEnabled = false;

    /// <summary>
    /// 通过分析固定位置的像素亮度、饱和度、颜色方差来计数圆形图标
    /// </summary>
    /// <param name="rowLabel">可选的训练行名，仅用于日志/落盘文件名标识，不影响判定</param>
    public static int CountCircularIcons(Mat screenshot, string? rowLabel = null)
    {
        int count = 0;
        int w = screenshot.Width;
        int h = screenshot.Height;

        using var hsv = new Mat();
        Cv2.CvtColor(screenshot, hsv, ColorConversionCodes.BGR2HSV);

        int checkSize = Math.Max(1, (int)(w * IconCheckRadius));
        string labelPrefix = string.IsNullOrEmpty(rowLabel) ? "" : $"[{rowLabel}] ";

        for (int slot = 0; slot < MaxIconSlots; slot++)
        {
            double slotY = IconStartY + slot * IconSpacing;
            int cx = (int)(w * IconCenterX);
            int cy = (int)(h * slotY);

            int x1 = Math.Max(0, cx - checkSize);
            int y1 = Math.Max(0, cy - checkSize);
            int x2 = Math.Min(w, cx + checkSize);
            int y2 = Math.Min(h, cy + checkSize);

            if (x2 <= x1 || y2 <= y1) break;

            using var region = new Mat(hsv, new Rect(x1, y1, x2 - x1, y2 - y1));
            Cv2.MeanStdDev(region, out var mean, out var stddev);

            double satMean = mean[1];
            double valMean = mean[2];
            double satStd = stddev[1];
            double valStd = stddev[2];

            // 彩色路径：力量行竖条误检约 satStd 33~34.5，用 >35 去掉；
            // 韧性第三格真头像约 38，必须保留（勿改回 39）
            bool colorIcon = satMean > 40 && valMean > 70 && satStd > 25;
            if (colorIcon)
            {
                if (satMean < 75)
                    colorIcon = satStd > 35;
                else
                    colorIcon = satStd > 40;
            }

            // 灰色/高亮立绘：valMean≈135 的真头像 valStd 通常 ≥46（实测 46.4 / 73.6），
            // 而行高亮/分隔线误判 valStd 仅 ~22-23；阈值收紧到 30 既切误判又不动真样本
            bool grayIcon = valMean > 135 && valStd > 30;
            bool hasIcon = colorIcon || grayIcon;
            string path = hasIcon ? (colorIcon ? "color" : "gray") : "none";

            Logger.Log($"[Race:TrainingSelect] {labelPrefix}Slot {slot}: y={slotY:F3} satMean={satMean:F1} valMean={valMean:F1} satStd={satStd:F1} valStd={valStd:F1} => {(hasIcon ? "ICON" : "empty")} ({path})");

            if (DebugDumpEnabled)
                DumpSlotRegion(screenshot, x1, y1, x2 - x1, y2 - y1, rowLabel, slot, hasIcon, satMean, valMean, satStd, valStd);

            if (hasIcon) count++;
            else break;
        }

        return count;
    }

    /// <summary>
    /// 把单个 slot 的 BGR 取样区域落盘，文件名带 ICON/empty + HSV 统计，便于事后核对
    /// 失败静默忽略，绝不影响主流程
    /// </summary>
    private static void DumpSlotRegion(Mat screenshotBgr, int x, int y, int w, int h,
        string? rowLabel, int slot, bool hasIcon, double satMean, double valMean, double satStd, double valStd)
    {
        try
        {
            string dir = Path.Combine(PathHelper.ScreenshotsDir, "debug_icons");
            Directory.CreateDirectory(dir);

            string ts = DateTime.Now.ToString("HHmmss_fff");
            string label = string.IsNullOrEmpty(rowLabel) ? "row" : SanitizeLabel(rowLabel);
            string verdict = hasIcon ? "ICON" : "empty";
            string fname = $"{ts}_{label}_slot{slot}_{verdict}_sm{satMean:F0}_vm{valMean:F0}_ss{satStd:F0}_vs{valStd:F0}.png";

            using var sub = new Mat(screenshotBgr, new Rect(x, y, w, h));
            Cv2.ImWrite(Path.Combine(dir, fname), sub);
        }
        catch
        {
            // 调试落盘失败不能影响识别本身
        }
    }

    private static string SanitizeLabel(string s)
    {
        var chars = s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        return new string(chars).Trim('_');
    }

    /// <summary>
    /// 集中/保护任一项 ≥4 时择优；否则前三项中取图标最多者
    /// 并列时按基调：攻击 力量→体力→韧性，生存 韧性→体力→力量
    /// </summary>
    public static int ApplyPriorityRule(int[] counts, BuildDirection buildDirection)
    {
        if (counts[3] >= 4 || counts[4] >= 4)
        {
            int special = counts[3] >= counts[4] ? 3 : 4;
            Logger.Log($"[Race:TrainingSelect] ApplyPriorityRule: special row {special + 1} has {counts[special]} icons (>=4), choosing between 集中/保护");
            return special;
        }

        int bestCount = Math.Max(counts[0], Math.Max(counts[1], counts[2]));
        var tied = new List<int>(3);
        for (int i = 0; i < 3; i++)
            if (counts[i] == bestCount) tied.Add(i);

        if (tied.Count == 1)
        {
            Logger.Log($"[Race:TrainingSelect] ApplyPriorityRule: front-3 unique max row {tied[0] + 1}, counts=[{counts[0]},{counts[1]},{counts[2]}]");
            return tied[0];
        }

        int[] preference = buildDirection == BuildDirection.Attack ? [0, 1, 2] : [2, 1, 0];
        foreach (int idx in preference)
        {
            if (tied.Contains(idx))
            {
                Logger.Log($"[Race:TrainingSelect] ApplyPriorityRule: tie among front-3 rows [{string.Join(",", tied)}], build={buildDirection} -> row {idx + 1}");
                return idx;
            }
        }

        return tied[0];
    }
}
