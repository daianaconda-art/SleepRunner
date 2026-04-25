using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using SleepRunner.Utils;
using Xunit;

namespace SleepRunner.Tests.Forms;

public class WarmUiSectionControlTests
{
    [Fact]
    public void RaceStatusIndicator_uses_warm_card_height()
    {
        WinFormsTestHost.Run(() =>
        {
            using var indicator = (Control)WinFormsTestHost.CreateInternal("SleepRunner.Forms.Controls.RaceStatusIndicator");

            Assert.Equal(78, indicator.Height);
        });
    }

    [Fact]
    public void RaceActionButtons_uses_warm_row_layout()
    {
        WinFormsTestHost.Run(() =>
        {
            using var buttons = (Control)WinFormsTestHost.CreateInternal("SleepRunner.Forms.Controls.RaceActionButtons");

            buttons.Width = 180;

            Assert.Equal(2, buttons.Controls.Count);
            Assert.Equal(46, buttons.Height);
            Assert.True(buttons.Controls[0].Width > buttons.Controls[1].Width);
        });
    }

    [Fact]
    public void RaceActionButtons_uses_buffered_painted_host_surface()
    {
        WinFormsTestHost.Run(() =>
        {
            using var buttons = (Control)WinFormsTestHost.CreateInternal("SleepRunner.Forms.Controls.RaceActionButtons");

            MethodInfo getStyle = typeof(Control).GetMethod("GetStyle", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("GetStyle not found.");

            Assert.Equal(Color.FromArgb(251, 249, 246).ToArgb(), buttons.BackColor.ToArgb());
            Assert.True((bool)getStyle.Invoke(buttons, [ControlStyles.UserPaint])!);
            Assert.True((bool)getStyle.Invoke(buttons, [ControlStyles.AllPaintingInWmPaint])!);
            Assert.True((bool)getStyle.Invoke(buttons, [ControlStyles.OptimizedDoubleBuffer])!);
            Assert.True((bool)getStyle.Invoke(buttons, [ControlStyles.ResizeRedraw])!);
        });
    }

    [Fact]
    public void RaceConfigStrip_uses_warm_card_height_and_stepper_size()
    {
        WinFormsTestHost.Run(() =>
        {
            using var strip = (Control)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.Controls.RaceConfigStrip",
                new UserSettings());

            Assert.Equal(136, strip.Height);

            Control firstRow = strip.Controls[0];
            Control stepper = firstRow.Controls.Cast<Control>()
                .Single(control => control.GetType().Name == "NumericStepper");

            Assert.Equal(new Size(132, 36), stepper.Size);
            Assert.Equal("x", (string)stepper.GetType().GetProperty("Suffix")!.GetValue(stepper)!);
        });
    }

    [Fact]
    public void ProfilesStrip_uses_warm_card_and_combo_sizes()
    {
        WinFormsTestHost.Run(() =>
        {
            using var strip = (Control)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.Controls.ProfilesStrip",
                new UserSettings());
            using var host = new Form();

            host.Controls.Add(strip);
            host.CreateControl();
            strip.CreateControl();

            var combos = strip.Controls.OfType<ComboBox>().ToArray();

            Assert.Equal(132, strip.Height);
            Assert.Equal(3, combos.Length);
            Assert.All(combos, combo =>
            {
                combo.CreateControl();
                Assert.True(combo.IsHandleCreated);
                Assert.Equal(ComboBoxStyle.DropDownList, combo.DropDownStyle);
                Assert.Equal(DrawMode.OwnerDrawFixed, combo.DrawMode);
                Assert.False(combo.IntegralHeight);
                Assert.Equal(32, combo.Height);
            });
        });
    }

    [Fact]
    public void TrainingRulesStrip_uses_warm_card_and_button_sizes()
    {
        WinFormsTestHost.Run(() =>
        {
            using var strip = (Control)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.Controls.TrainingRulesStrip",
                "default");
            using var host = new Form();

            host.Controls.Add(strip);
            host.CreateControl();
            strip.CreateControl();

            var combo = strip.Controls.OfType<ComboBox>().Single();
            var buttons = strip.Controls
                .Cast<Control>()
                .Where(control => control.GetType().Name == "RoundedButton")
                .ToArray();

            Assert.Equal(118, strip.Height);
            combo.CreateControl();
            Assert.True(combo.IsHandleCreated);
            Assert.Equal(ComboBoxStyle.DropDownList, combo.DropDownStyle);
            Assert.Equal(DrawMode.OwnerDrawFixed, combo.DrawMode);
            Assert.False(combo.IntegralHeight);
            Assert.Equal(32, combo.Height);
            Assert.Equal(3, buttons.Length);
            Assert.All(buttons, button => Assert.Equal(36, button.Height));
        });
    }

    [Fact]
    public void FilesStrip_uses_warm_card_and_button_sizes()
    {
        WinFormsTestHost.Run(() =>
        {
            using var strip = (Control)WinFormsTestHost.CreateInternal("SleepRunner.Forms.Controls.FilesStrip");

            var buttons = strip.Controls
                .Cast<Control>()
                .Where(control => control.GetType().Name == "RoundedButton")
                .ToArray();

            Assert.Equal(78, strip.Height);
            Assert.Equal(3, buttons.Length);
            Assert.All(buttons, button => Assert.Equal(40, button.Height));
        });
    }
}
