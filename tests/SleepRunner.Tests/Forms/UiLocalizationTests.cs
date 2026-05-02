using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using SleepRunner.Automation;
using SleepRunner.Automation.Race;
using SleepRunner.Automation.Race.Policy.Training;
using SleepRunner.Forms;
using Xunit;

namespace SleepRunner.Tests.Forms;

public class UiLocalizationTests
{
    [Fact]
    public void RaceMainWindow_uses_chinese_visible_copy()
    {
        WinFormsTestHost.Run(() =>
        {
            using var window = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.RaceMainWindow",
                new StubRaceController());

            IReadOnlyList<string> texts = WinFormsTestHost.CollectTexts(window);

            Assert.Contains("跑马助手", texts);
            Assert.Contains("开始", texts);
            Assert.Contains("停止", texts);
            Assert.Contains("调校", texts);
            Assert.Contains("配置方案", texts);
            Assert.Contains("训练规则", texts);
            Assert.Contains("配置目录", texts);
            Assert.Contains("Alt+Q 开始/停止  •  Esc 关闭  •  拖动边缘可调整大小", texts);
            Assert.Contains("版本 1.0", texts);
        });
    }

    [Fact]
    public void RaceStatusIndicator_uses_chinese_state_copy()
    {
        WinFormsTestHost.Run(() =>
        {
            object indicator = WinFormsTestHost.CreateInternal("SleepRunner.Forms.Controls.RaceStatusIndicator");

            WinFormsTestHost.Invoke(indicator, "ApplyState", RaceState.Idle);
            Assert.Equal("空闲", WinFormsTestHost.ReadPrivateField<string>(indicator, "_title"));
            Assert.Equal("进入跑马界面后点击开始", WinFormsTestHost.ReadPrivateField<string>(indicator, "_subtitle"));

            WinFormsTestHost.Invoke(indicator, "ApplyState", RaceState.Running);
            Assert.Equal("运行中", WinFormsTestHost.ReadPrivateField<string>(indicator, "_title"));
            Assert.Equal("正在自动跑马", WinFormsTestHost.ReadPrivateField<string>(indicator, "_subtitle"));
        });
    }

    [Fact]
    public void RaceConfigStrip_uses_chinese_labels_and_copy_without_legacy_controls()
    {
        WinFormsTestHost.Run(() =>
        {
            using var strip = (Control)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.Controls.RaceConfigStrip",
                new SleepRunner.Utils.UserSettings());

            IReadOnlyList<string> texts = WinFormsTestHost.CollectTexts(strip);

            Assert.Contains("等待倍率", texts);
            Assert.Contains("点击速度", texts);
            Assert.Contains("红色委托", texts);
            Assert.DoesNotContain("失败率上限", texts);
            Assert.DoesNotContain("猛攻阈值", texts);
            Assert.DoesNotContain("养成方向", texts);
            Assert.DoesNotContain("攻击", texts);
            Assert.DoesNotContain("生存", texts);
        });
    }

    [Fact]
    public void RaceMainWindow_resize_hit_test_supports_diagonal_corners()
    {
        MethodInfo method = typeof(RaceMainWindow).GetMethod("ResolveResizeHit", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ResolveResizeHit not found.");

        Assert.Equal(13, (int)method.Invoke(null, [new Point(2, 2), new Size(360, 640)])!);
        Assert.Equal(14, (int)method.Invoke(null, [new Point(358, 2), new Size(360, 640)])!);
        Assert.Equal(16, (int)method.Invoke(null, [new Point(2, 638), new Size(360, 640)])!);
        Assert.Equal(17, (int)method.Invoke(null, [new Point(358, 638), new Size(360, 640)])!);
    }

    [Fact]
    public void TrainingRules_surfaces_use_chinese_visible_copy()
    {
        WinFormsTestHost.Run(() =>
        {
            using var strip = (Control)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.Controls.TrainingRulesStrip",
                "default");
            IReadOnlyList<string> stripTexts = WinFormsTestHost.CollectTexts(strip);
            Assert.Contains("配置", stripTexts);
            Assert.Contains("编辑", stripTexts);
            Assert.Contains("复制", stripTexts);
            Assert.Contains("打开目录", stripTexts);

            using var nameDialog = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.TrainingRules.TrainingProfileNameDialog",
                "复制训练配置",
                "default-copy");
            IReadOnlyList<string> nameDialogTexts = WinFormsTestHost.CollectTexts(nameDialog);
            Assert.Contains("配置名", nameDialogTexts);
            Assert.Contains("打开编辑器", nameDialogTexts);
            Assert.Contains("取消", nameDialogTexts);

            var rule = new TrainingRuleCard
            {
                Id = "rest_high_fail",
                Field = TrainingRuleField.AnyFailRate,
                Operator = TrainingRuleOperator.GreaterThan,
                Value = 30,
                Action = TrainingDecisionAction.Rest,
                Enabled = true,
                IsFallback = false,
            };

            using var card = (Control)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.TrainingRules.TrainingRuleCardControl",
                rule);
            IReadOnlyList<string> cardTexts = WinFormsTestHost.CollectTexts(card);
            Assert.DoesNotContain("规则编号", cardTexts);
            Assert.Contains("启用", cardTexts);
            Assert.Contains("条件", cardTexts);
            Assert.Contains("附加条件", cardTexts);
            Assert.Contains("动作", cardTexts);
            Assert.Contains("上移", cardTexts);
            Assert.Contains("下移", cardTexts);
            Assert.Contains("删除", cardTexts);
            Assert.Contains("任意失败率", cardTexts);
            Assert.Contains("休息", cardTexts);
            Assert.Contains("内置默认", cardTexts);

            var profile = new TrainingRuleProfile
            {
                SourcePath = "profile.json",
            };
            profile.Rules.Add(new TrainingRuleCard
            {
                Id = "fallback",
                Action = TrainingDecisionAction.BuiltinDefault,
                Enabled = true,
                IsFallback = true,
            });

            using var editor = (Form)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.TrainingRules.TrainingRuleEditorWindow",
                "编辑训练规则 - default",
                "default",
                "profile.json",
                "profile.json",
                profile);
            IReadOnlyList<string> editorTexts = WinFormsTestHost.CollectTexts(editor);
            Assert.Contains("配置：default", editorTexts);
            Assert.Contains("每张规则卡最多编辑两个条件和一个动作。兜底卡会固定在最后，并且只编辑动作。", editorTexts);
            Assert.Contains("新增规则", editorTexts);
            Assert.Contains("保存", editorTexts);
            Assert.Contains("取消", editorTexts);
            Assert.Contains("兜底", editorTexts);
        });
    }

    private sealed class StubRaceController : IRaceController
    {
        public RaceState State => RaceState.Idle;

        public event Action<RaceState>? StateChanged
        {
            add { }
            remove { }
        }

        public event Action<string>? ActivityChanged
        {
            add { }
            remove { }
        }

        public void Dispose()
        {
        }

        public void Pause()
        {
        }

        public void Resume()
        {
        }

        public void Start()
        {
        }

        public Task StopAsync() => Task.CompletedTask;
    }
}
