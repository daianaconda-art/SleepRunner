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
    public void TrainingRuleCardControl_combo_boxes_ignore_mouse_wheel_selection_changes()
    {
        Exception? captured = null;

        var thread = new Thread(() =>
        {
            try
            {
                Type? controlType = typeof(TrainingRuleLoader).Assembly.GetType("SleepRunner.Forms.TrainingRules.TrainingRuleCardControl");
                Assert.NotNull(controlType);

                var rule = new TrainingRuleCard
                {
                    Id = "safe_strength",
                    Field = TrainingRuleField.StrengthIcons,
                    Operator = TrainingRuleOperator.GreaterThanOrEqual,
                    Value = 3,
                    Action = TrainingDecisionAction.TrainStrength,
                    Enabled = true,
                    IsFallback = false,
                };

                using var form = new Form();
                using var card = (Control)(Activator.CreateInstance(controlType!, new object[] { rule })
                    ?? throw new InvalidOperationException("Could not create TrainingRuleCardControl."));
                form.Controls.Add(card);
                form.Show();
                Application.DoEvents();

                ComboBox combo = card.Controls.OfType<ComboBox>().First(item => item.Items.Count > 1);
                Assert.NotEqual(typeof(ComboBox), combo.GetType());
                combo.Focus();
                combo.SelectedIndex = 0;
                Application.DoEvents();

                MethodInfo onMouseWheel = combo.GetType().GetMethod(
                    "OnMouseWheel",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("OnMouseWheel method not found.");

                onMouseWheel.Invoke(combo, [new MouseEventArgs(MouseButtons.None, 0, 0, 0, -120)]);

                Assert.Equal(0, combo.SelectedIndex);
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
    public void TrainingRuleCardControl_round_trips_two_conditions()
    {
        Exception? captured = null;

        var thread = new Thread(() =>
        {
            try
            {
                Type? controlType = typeof(TrainingRuleLoader).Assembly.GetType("SleepRunner.Forms.TrainingRules.TrainingRuleCardControl");
                Assert.NotNull(controlType);

                var rule = new TrainingRuleCard
                {
                    Id = "safe_strength",
                    Action = TrainingDecisionAction.TrainStrength,
                    Enabled = true,
                    IsFallback = false,
                };
                rule.Conditions.Add(new TrainingRuleCondition
                {
                    Field = TrainingRuleField.StrengthIcons,
                    Operator = TrainingRuleOperator.GreaterThanOrEqual,
                    Value = 3,
                });
                rule.Conditions.Add(new TrainingRuleCondition
                {
                    Field = TrainingRuleField.StrengthFailRate,
                    Operator = TrainingRuleOperator.LessThan,
                    Value = 40,
                });

                using var card = (Control)(Activator.CreateInstance(controlType!, new object[] { rule })
                    ?? throw new InvalidOperationException("Could not create TrainingRuleCardControl."));

                MethodInfo toRuleCard = controlType.GetMethod("ToRuleCard", BindingFlags.Instance | BindingFlags.Public)
                    ?? throw new InvalidOperationException("ToRuleCard method not found.");

                var saved = Assert.IsType<TrainingRuleCard>(toRuleCard.Invoke(card, null));

                Assert.Equal("safe_strength", saved.Id);
                Assert.Equal(2, saved.Conditions.Count);
                Assert.Equal(TrainingRuleField.StrengthIcons, saved.Conditions[0].Field);
                Assert.Equal(TrainingRuleOperator.GreaterThanOrEqual, saved.Conditions[0].Operator);
                Assert.Equal(3, saved.Conditions[0].Value);
                Assert.Equal(TrainingRuleField.StrengthFailRate, saved.Conditions[1].Field);
                Assert.Equal(TrainingRuleOperator.LessThan, saved.Conditions[1].Operator);
                Assert.Equal(40, saved.Conditions[1].Value);
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
