using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Xunit;

namespace SleepRunner.Tests.Forms;

public class WarmUiPrimitiveControlTests
{
    [Fact]
    public void IconButton_uses_warm_default_size()
    {
        WinFormsTestHost.Run(() =>
        {
            using var button = (Control)WinFormsTestHost.CreateInternal("SleepRunner.Forms.Controls.IconButton");

            Assert.Equal(new Size(30, 28), button.Size);
        });
    }

    [Fact]
    public void NumericStepper_uses_warm_default_size_and_button_width()
    {
        WinFormsTestHost.Run(() =>
        {
            using var stepper = (Control)WinFormsTestHost.CreateInternal("SleepRunner.Forms.Controls.NumericStepper");

            Assert.Equal(new Size(132, 36), stepper.Size);

            PropertyInfo buttonWidth = stepper.GetType().GetProperty("ButtonWidth", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("ButtonWidth not found.");

            Assert.Equal(32, (int)buttonWidth.GetValue(stepper)!);
        });
    }

    [Fact]
    public void RoundedButton_uses_warm_corner_radius()
    {
        WinFormsTestHost.Run(() =>
        {
            using var button = (Control)WinFormsTestHost.CreateInternal("SleepRunner.Forms.Controls.RoundedButton");

            PropertyInfo cornerRadius = button.GetType().GetProperty("CornerRadius", BindingFlags.Instance | BindingFlags.Public)
                ?? throw new InvalidOperationException("CornerRadius not found.");

            Assert.Equal(12, (int)cornerRadius.GetValue(button)!);
        });
    }

    [Fact]
    public void RoundedButton_uses_rounded_clip_region()
    {
        WinFormsTestHost.Run(() =>
        {
            using var host = new Form();
            using var button = (Control)WinFormsTestHost.CreateInternal("SleepRunner.Forms.Controls.RoundedButton");
            button.Size = new Size(132, 46);

            host.Controls.Add(button);
            host.CreateControl();
            button.CreateControl();

            Assert.NotNull(button.Region);
            Assert.False(button.Region!.IsVisible(0, 0), "Top-left corner should be clipped out by the rounded region.");
            Assert.True(button.Region.IsVisible(button.Width / 2, button.Height / 2), "Button center should remain inside the rounded region.");
        });
    }

    [Fact]
    public void RoundedButton_honors_caller_supplied_theming()
    {
        WinFormsTestHost.Run(() =>
        {
            using var defaultButton = (Control)WinFormsTestHost.CreateInternal("SleepRunner.Forms.Controls.RoundedButton");
            var defaultColors = ResolveColors(defaultButton);
            Color defaultAccent = (Color)defaultButton.GetType().GetProperty("AccentColor", BindingFlags.Instance | BindingFlags.Public)!.GetValue(defaultButton)!;
            Assert.Equal(defaultAccent, defaultColors.fill);
            Assert.Equal(Color.FromArgb(225, 117, 67), defaultColors.border);

            using var themedButton = (Control)WinFormsTestHost.CreateInternal("SleepRunner.Forms.Controls.RoundedButton");
            themedButton.GetType().GetProperty("AccentColor", BindingFlags.Instance | BindingFlags.Public)!.SetValue(themedButton, Color.FromArgb(32, 96, 168));
            themedButton.GetType().GetProperty("ForeColor", BindingFlags.Instance | BindingFlags.Public)!.SetValue(themedButton, Color.FromArgb(88, 40, 120));
            Type variantType = themedButton.GetType().GetNestedType("ButtonVariant", BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("ButtonVariant not found.");

            themedButton.GetType().GetProperty("Variant", BindingFlags.Instance | BindingFlags.Public)!.SetValue(themedButton, Enum.Parse(variantType, "Secondary"));

            var themedSecondary = ResolveColors(themedButton);
            Assert.Equal(Color.FromArgb(32, 96, 168), themedSecondary.fg);
            Assert.NotEqual(Color.FromArgb(225, 117, 67), themedSecondary.border);

            themedButton.GetType().GetProperty("Variant", BindingFlags.Instance | BindingFlags.Public)!.SetValue(themedButton, Enum.Parse(variantType, "Primary"));
            SetPrivateField(themedButton, "_hover", true);
            var themedPrimary = ResolveColors(themedButton);
            Assert.Equal(Lighten(Color.FromArgb(32, 96, 168), 0.08f), themedPrimary.fill);
            Assert.Equal(Darken(Color.FromArgb(32, 96, 168), 0.2f), themedPrimary.border);

            themedButton.GetType().GetProperty("Variant", BindingFlags.Instance | BindingFlags.Public)!.SetValue(themedButton, Enum.Parse(variantType, "Ghost"));
            var themedGhost = ResolveColors(themedButton);
            Assert.Equal(Color.FromArgb(88, 40, 120), themedGhost.fg);
        });
    }

    [Fact]
    public void RoundedButton_secondary_preserves_caller_supplied_forecolor_with_default_accent()
    {
        WinFormsTestHost.Run(() =>
        {
            using var button = (Control)WinFormsTestHost.CreateInternal("SleepRunner.Forms.Controls.RoundedButton");
            Type variantType = button.GetType().GetNestedType("ButtonVariant", BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("ButtonVariant not found.");

            Color callerForeColor = Color.FromArgb(88, 40, 120);
            button.GetType().GetProperty("ForeColor", BindingFlags.Instance | BindingFlags.Public)!.SetValue(button, callerForeColor);
            button.GetType().GetProperty("Variant", BindingFlags.Instance | BindingFlags.Public)!.SetValue(button, Enum.Parse(variantType, "Secondary"));

            var colors = ResolveColors(button);

            Assert.Equal(callerForeColor, colors.fg);
        });
    }

    [Fact]
    public void RoundedButton_ghost_preserves_backdrop_and_hover_colors()
    {
        WinFormsTestHost.Run(() =>
        {
            using var button = (Control)WinFormsTestHost.CreateInternal("SleepRunner.Forms.Controls.RoundedButton");
            Type variantType = button.GetType().GetNestedType("ButtonVariant", BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("ButtonVariant not found.");

            button.GetType().GetProperty("Variant", BindingFlags.Instance | BindingFlags.Public)!.SetValue(button, Enum.Parse(variantType, "Ghost"));
            button.GetType().GetProperty("BackdropColor", BindingFlags.Instance | BindingFlags.Public)!.SetValue(button, Color.FromArgb(20, 30, 40));
            button.GetType().GetProperty("ForeColor", BindingFlags.Instance | BindingFlags.Public)!.SetValue(button, Color.FromArgb(88, 40, 120));

            using var bitmap = new Bitmap(button.Width, button.Height);
            button.DrawToBitmap(bitmap, new Rectangle(Point.Empty, button.Size));

            var interior = bitmap.GetPixel(button.Width / 2, button.Height / 2);
            Assert.Equal(Color.FromArgb(20, 30, 40).ToArgb(), interior.ToArgb());

            SetPrivateField(button, "_hover", true);
            var hover = ResolveColors(button);
            Assert.Equal(Color.FromArgb(249, 245, 240), hover.fill);
            Assert.Equal(Color.FromArgb(88, 40, 120), hover.fg);
        });
    }

    [Fact]
    public void RoundedButton_hover_render_does_not_introduce_dark_corner_artifacts()
    {
        WinFormsTestHost.Run(() =>
        {
            using var host = new Form { BackColor = Color.FromArgb(251, 249, 246) };
            using var button = (Control)WinFormsTestHost.CreateInternal("SleepRunner.Forms.Controls.RoundedButton");
            Type variantType = button.GetType().GetNestedType("ButtonVariant", BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("ButtonVariant not found.");

            button.GetType().GetProperty("Variant", BindingFlags.Instance | BindingFlags.Public)!.SetValue(button, Enum.Parse(variantType, "Secondary"));
            button.GetType().GetProperty("ForeColor", BindingFlags.Instance | BindingFlags.Public)!.SetValue(button, Color.FromArgb(122, 116, 108));
            button.Size = new Size(132, 46);

            host.Controls.Add(button);
            host.CreateControl();
            button.CreateControl();

            SetPrivateField(button, "_hover", true);

            using var bitmap = new Bitmap(button.Width, button.Height);
            button.DrawToBitmap(bitmap, new Rectangle(Point.Empty, button.Size));

            Color corner = bitmap.GetPixel(1, 1);
            Assert.NotEqual(Color.Black.ToArgb(), corner.ToArgb());
        });
    }

    [Fact]
    public void SectionHeader_uses_warm_row_height()
    {
        WinFormsTestHost.Run(() =>
        {
            using var header = (Control)WinFormsTestHost.CreateInternal(
                "SleepRunner.Forms.Controls.SectionHeader",
                "Warm section");

            Assert.Equal(24, header.Height);
        });
    }

    private static (Color fill, Color border, Color fg) ResolveColors(object button)
    {
        MethodInfo method = button.GetType().GetMethod("ResolveColors", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ResolveColors not found.");

        object?[] args = [null, null, null];
        method.Invoke(button, args);
        return ((Color)args[0]!, (Color)args[1]!, (Color)args[2]!);
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found.");

        field.SetValue(instance, value);
    }

    private static Color Darken(Color c, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(c.A,
            (int)(c.R * (1 - amount)),
            (int)(c.G * (1 - amount)),
            (int)(c.B * (1 - amount)));
    }

    private static Color Lighten(Color c, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(c.A,
            (int)(c.R + (255 - c.R) * amount),
            (int)(c.G + (255 - c.G) * amount),
            (int)(c.B + (255 - c.B) * amount));
    }
}
