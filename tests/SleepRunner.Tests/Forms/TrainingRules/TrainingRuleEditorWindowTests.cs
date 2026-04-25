using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using SleepRunner.Automation.Race.Policy.Training;
using Xunit;

namespace SleepRunner.Tests.Forms.TrainingRules;

public class TrainingRuleEditorWindowTests
{
    [Fact]
    public void TrainingRuleEditorWindow_constructor_does_not_throw_during_initial_layout()
    {
        Exception? captured = null;

        var thread = new Thread(() =>
        {
            try
            {
                Type? windowType = typeof(TrainingRuleLoader).Assembly.GetType("SleepRunner.Forms.TrainingRules.TrainingRuleEditorWindow");
                Assert.NotNull(windowType);

                ConstructorInfo? ctor = windowType!.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                    .SingleOrDefault(c => c.GetParameters().Length == 5);
                Assert.NotNull(ctor);

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

                using Form? window = ctor!.Invoke(
                [
                    "Edit training rules - default",
                    "default",
                    "profile.json",
                    "profile.json",
                    profile,
                ]) as Form;

                Assert.NotNull(window);
            }
            catch (Exception ex)
            {
                captured = ex is TargetInvocationException tie
                    ? tie.InnerException ?? tie
                    : ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(captured);
    }

    [Fact]
    public void TrainingRuleEditorWindow_uses_buffered_non_transparent_cards_panel()
    {
        Exception? captured = null;

        var thread = new Thread(() =>
        {
            try
            {
                Type? windowType = typeof(TrainingRuleLoader).Assembly.GetType("SleepRunner.Forms.TrainingRules.TrainingRuleEditorWindow");
                Assert.NotNull(windowType);

                ConstructorInfo? ctor = windowType!.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                    .SingleOrDefault(c => c.GetParameters().Length == 5);
                Assert.NotNull(ctor);

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

                using Form? window = ctor!.Invoke(
                [
                    "Edit training rules - default",
                    "default",
                    "profile.json",
                    "profile.json",
                    profile,
                ]) as Form;

                Assert.NotNull(window);

                FieldInfo cardsField = windowType.GetField("_cardsPanel", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("_cardsPanel field not found.");

                Control cardsPanel = (Control)(cardsField.GetValue(window!) ?? throw new InvalidOperationException("_cardsPanel value missing."));
                Assert.NotEqual(Color.Transparent.ToArgb(), cardsPanel.BackColor.ToArgb());

                PropertyInfo doubleBuffered = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("DoubleBuffered property not found.");

                Assert.True((bool)(doubleBuffered.GetValue(cardsPanel) ?? false));
            }
            catch (Exception ex)
            {
                captured = ex is TargetInvocationException tie
                    ? tie.InnerException ?? tie
                    : ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(captured);
    }
}
