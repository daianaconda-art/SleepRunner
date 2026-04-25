using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SleepRunner.Automation.Race.Policy.Training;
using SleepRunner.Forms;
using SleepRunner.Forms.Controls;

namespace SleepRunner.Forms.TrainingRules;

internal sealed class TrainingRuleCardControl : UserControl
{
    private const int Pad = 10;
    private const int RowHeight = 30;
    private const int ButtonWidth = 72;
    private const int ButtonGap = 6;
    private const int LabelWidth = 84;

    private readonly Label _lblId;
    private readonly TextBox _txtId;
    private readonly CheckBox _chkEnabled;
    private readonly Label _lblCondition;
    private readonly ComboBox _cmbField;
    private readonly ComboBox _cmbOperator;
    private readonly NumericUpDown _numValue;
    private readonly Label _lblAction;
    private readonly ComboBox _cmbAction;
    private readonly RoundedButton _btnUp;
    private readonly RoundedButton _btnDown;
    private readonly RoundedButton _btnDelete;

    public event Action<TrainingRuleCardControl>? MoveUpRequested;
    public event Action<TrainingRuleCardControl>? MoveDownRequested;
    public event Action<TrainingRuleCardControl>? DeleteRequested;

    public TrainingRuleCardControl(TrainingRuleCard rule)
    {
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Margin = new Padding(0, 0, 0, 10);

        _lblId = CreateLabel(UiText.Training.RuleId);
        _txtId = new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            Font = RaceTheme.BodyFont(),
            BackColor = RaceTheme.SurfaceSunken,
            ForeColor = RaceTheme.TextPrimary,
        };

        _chkEnabled = new CheckBox
        {
            Text = UiText.Training.Enabled,
            AutoSize = false,
            Font = RaceTheme.SmallFont(),
            ForeColor = RaceTheme.TextSecondary,
            BackColor = Color.Transparent,
            CheckAlign = ContentAlignment.MiddleRight,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _lblCondition = CreateLabel(UiText.Training.Condition);
        _cmbField = CreateComboBox();
        _cmbOperator = CreateComboBox();
        _numValue = new NumericUpDown
        {
            BorderStyle = BorderStyle.FixedSingle,
            Font = RaceTheme.BodyFont(),
            BackColor = RaceTheme.SurfaceSunken,
            ForeColor = RaceTheme.TextPrimary,
            Minimum = -9999,
            Maximum = 9999,
        };

        _lblAction = CreateLabel(rule.IsFallback ? UiText.Training.Fallback : UiText.Training.Action);
        _cmbAction = CreateComboBox();

        _btnUp = CreateButton(UiText.Actions.MoveUp, () => MoveUpRequested?.Invoke(this));
        _btnDown = CreateButton(UiText.Actions.MoveDown, () => MoveDownRequested?.Invoke(this));
        _btnDelete = CreateButton(UiText.Actions.Delete, () => DeleteRequested?.Invoke(this));
        _btnDelete.ForeColor = RaceTheme.Danger;

        Controls.AddRange(
        [
            _lblId,
            _txtId,
            _chkEnabled,
            _lblCondition,
            _cmbField,
            _cmbOperator,
            _numValue,
            _lblAction,
            _cmbAction,
            _btnUp,
            _btnDown,
            _btnDelete,
        ]);

        PopulateFieldOptions();
        PopulateOperatorOptions();
        PopulateActionOptions();
        ApplyRule(rule);
    }

    public bool IsFallback { get; private set; }

    public void SetMoveState(bool canMoveUp, bool canMoveDown)
    {
        if (IsFallback)
        {
            _btnUp.Visible = false;
            _btnDown.Visible = false;
            _btnDelete.Visible = false;
            return;
        }

        _btnUp.Visible = true;
        _btnDown.Visible = true;
        _btnDelete.Visible = true;
        _btnUp.Enabled = canMoveUp;
        _btnDown.Enabled = canMoveDown;
    }

    public TrainingRuleCard ToRuleCard()
    {
        string id = _txtId.Text.Trim();
        if (IsFallback)
        {
            return new TrainingRuleCard
            {
                Id = string.IsNullOrWhiteSpace(id) ? "fallback" : id,
                Action = GetSelectedValue<TrainingDecisionAction>(_cmbAction),
                Enabled = _chkEnabled.Checked,
                IsFallback = true,
            };
        }

        return new TrainingRuleCard
        {
            Id = id,
            Field = GetSelectedValue<TrainingRuleField>(_cmbField),
            Operator = GetSelectedValue<TrainingRuleOperator>(_cmbOperator),
            Value = Decimal.ToInt32(_numValue.Value),
            Action = GetSelectedValue<TrainingDecisionAction>(_cmbAction),
            Enabled = _chkEnabled.Checked,
            IsFallback = false,
        };
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutControls();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        RaceTheme.FillRoundedRect(e.Graphics, rect, RaceTheme.Panel, 8);
        using var path = RaceTheme.BuildRoundedPath(rect, 8);
        using var pen = new Pen(RaceTheme.BorderStrong, 1);
        e.Graphics.DrawPath(pen, path);
    }

    private void ApplyRule(TrainingRuleCard rule)
    {
        IsFallback = rule.IsFallback;
        Height = IsFallback ? 104 : 142;

        _txtId.Text = rule.Id;
        _chkEnabled.Checked = rule.Enabled;

        if (rule.Field is not null)
        {
            SetSelectedValue(_cmbField, rule.Field.Value, "field");
        }

        if (rule.Operator is not null)
        {
            SetSelectedValue(_cmbOperator, rule.Operator.Value, "operator");
        }

        _numValue.Value = Math.Clamp(rule.Value ?? 0, Decimal.ToInt32(_numValue.Minimum), Decimal.ToInt32(_numValue.Maximum));
        SetSelectedValue(_cmbAction, rule.Action, "action");

        _lblCondition.Visible = !IsFallback;
        _cmbField.Visible = !IsFallback;
        _cmbOperator.Visible = !IsFallback;
        _numValue.Visible = !IsFallback;
        _lblAction.Text = IsFallback ? UiText.Training.Fallback : UiText.Training.Action;
        _btnDelete.Visible = !IsFallback;
        LayoutControls();
    }

    private void LayoutControls()
    {
        if (Width <= 0)
        {
            return;
        }

        int rightButtonsWidth = ButtonWidth * 3 + ButtonGap * 2;
        int contentWidth = Math.Max(200, Width - Pad * 2);
        int idLeft = Pad + LabelWidth + ButtonGap;
        int idWidth = Math.Max(140, contentWidth - LabelWidth - rightButtonsWidth - 86);

        _lblId.SetBounds(Pad, Pad, LabelWidth, RowHeight);
        _txtId.SetBounds(idLeft, Pad, idWidth, RowHeight);
        _chkEnabled.SetBounds(_txtId.Right + ButtonGap, Pad, 86, RowHeight);

        int buttonLeft = Width - Pad - rightButtonsWidth;
        _btnUp.SetBounds(buttonLeft, Pad, ButtonWidth, RowHeight);
        _btnDown.SetBounds(_btnUp.Right + ButtonGap, Pad, ButtonWidth, RowHeight);
        _btnDelete.SetBounds(_btnDown.Right + ButtonGap, Pad, ButtonWidth, RowHeight);

        int actionRowY = IsFallback ? Pad + RowHeight + 12 : Pad + (RowHeight + 8) * 2;
        int actionLeft = Pad + LabelWidth + ButtonGap;
        int actionWidth = Math.Max(140, Width - actionLeft - Pad);

        if (!IsFallback)
        {
            int conditionRowY = Pad + RowHeight + 8;
            int fieldWidth = Math.Max(150, Width - Pad * 2 - LabelWidth - 126 - 96 - ButtonGap * 3);
            int conditionLeft = Pad + LabelWidth + ButtonGap;

            _lblCondition.SetBounds(Pad, conditionRowY, LabelWidth, RowHeight);
            _cmbField.SetBounds(conditionLeft, conditionRowY, fieldWidth, RowHeight);
            _cmbOperator.SetBounds(_cmbField.Right + ButtonGap, conditionRowY, 80, RowHeight);
            _numValue.SetBounds(_cmbOperator.Right + ButtonGap, conditionRowY, 86, RowHeight);
        }

        _lblAction.SetBounds(Pad, actionRowY, LabelWidth, RowHeight);
        _cmbAction.SetBounds(actionLeft, actionRowY, actionWidth, RowHeight);
    }

    private void PopulateFieldOptions()
    {
        _cmbField.Items.AddRange(
        [
            new OptionItem<TrainingRuleField>(TrainingRuleField.StrengthIcons, UiText.Training.FieldLabel(TrainingRuleField.StrengthIcons)),
            new OptionItem<TrainingRuleField>(TrainingRuleField.StaminaIcons, UiText.Training.FieldLabel(TrainingRuleField.StaminaIcons)),
            new OptionItem<TrainingRuleField>(TrainingRuleField.AgilityIcons, UiText.Training.FieldLabel(TrainingRuleField.AgilityIcons)),
            new OptionItem<TrainingRuleField>(TrainingRuleField.FocusIcons, UiText.Training.FieldLabel(TrainingRuleField.FocusIcons)),
            new OptionItem<TrainingRuleField>(TrainingRuleField.GuardIcons, UiText.Training.FieldLabel(TrainingRuleField.GuardIcons)),
            new OptionItem<TrainingRuleField>(TrainingRuleField.StrengthFailRate, UiText.Training.FieldLabel(TrainingRuleField.StrengthFailRate)),
            new OptionItem<TrainingRuleField>(TrainingRuleField.StaminaFailRate, UiText.Training.FieldLabel(TrainingRuleField.StaminaFailRate)),
            new OptionItem<TrainingRuleField>(TrainingRuleField.AgilityFailRate, UiText.Training.FieldLabel(TrainingRuleField.AgilityFailRate)),
            new OptionItem<TrainingRuleField>(TrainingRuleField.FocusFailRate, UiText.Training.FieldLabel(TrainingRuleField.FocusFailRate)),
            new OptionItem<TrainingRuleField>(TrainingRuleField.GuardFailRate, UiText.Training.FieldLabel(TrainingRuleField.GuardFailRate)),
            new OptionItem<TrainingRuleField>(TrainingRuleField.AnyFailRate, UiText.Training.FieldLabel(TrainingRuleField.AnyFailRate)),
            new OptionItem<TrainingRuleField>(TrainingRuleField.StrengthStat, UiText.Training.FieldLabel(TrainingRuleField.StrengthStat)),
            new OptionItem<TrainingRuleField>(TrainingRuleField.StaminaStat, UiText.Training.FieldLabel(TrainingRuleField.StaminaStat)),
        ]);
    }

    private void PopulateOperatorOptions()
    {
        _cmbOperator.Items.AddRange(
        [
            new OptionItem<TrainingRuleOperator>(TrainingRuleOperator.GreaterThan, ">"),
            new OptionItem<TrainingRuleOperator>(TrainingRuleOperator.GreaterThanOrEqual, ">="),
            new OptionItem<TrainingRuleOperator>(TrainingRuleOperator.LessThan, "<"),
            new OptionItem<TrainingRuleOperator>(TrainingRuleOperator.LessThanOrEqual, "<="),
        ]);
    }

    private void PopulateActionOptions()
    {
        _cmbAction.Items.AddRange(
        [
            new OptionItem<TrainingDecisionAction>(TrainingDecisionAction.TrainStrength, UiText.Training.ActionLabel(TrainingDecisionAction.TrainStrength)),
            new OptionItem<TrainingDecisionAction>(TrainingDecisionAction.TrainStamina, UiText.Training.ActionLabel(TrainingDecisionAction.TrainStamina)),
            new OptionItem<TrainingDecisionAction>(TrainingDecisionAction.TrainAgility, UiText.Training.ActionLabel(TrainingDecisionAction.TrainAgility)),
            new OptionItem<TrainingDecisionAction>(TrainingDecisionAction.TrainFocus, UiText.Training.ActionLabel(TrainingDecisionAction.TrainFocus)),
            new OptionItem<TrainingDecisionAction>(TrainingDecisionAction.TrainGuard, UiText.Training.ActionLabel(TrainingDecisionAction.TrainGuard)),
            new OptionItem<TrainingDecisionAction>(TrainingDecisionAction.Rest, UiText.Training.ActionLabel(TrainingDecisionAction.Rest)),
            new OptionItem<TrainingDecisionAction>(TrainingDecisionAction.BuiltinDefault, UiText.Training.ActionLabel(TrainingDecisionAction.BuiltinDefault)),
        ]);
    }

    private static Label CreateLabel(string text) => new()
    {
        Text = text,
        AutoSize = false,
        Font = RaceTheme.SmallFont(),
        ForeColor = RaceTheme.TextSecondary,
        BackColor = Color.Transparent,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static ComboBox CreateComboBox() => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat,
        Font = RaceTheme.BodyFont(),
        BackColor = RaceTheme.SurfaceSunken,
        ForeColor = RaceTheme.TextPrimary,
    };

    private static RoundedButton CreateButton(string text, Action onClick)
    {
        var button = new RoundedButton
        {
            Text = text,
            Variant = RoundedButton.ButtonVariant.Ghost,
            Font = RaceTheme.SmallFont(),
            ForeColor = RaceTheme.TextPrimary,
            BackdropColor = RaceTheme.Panel,
            Height = RowHeight,
        };

        button.Click += (_, _) => onClick();
        return button;
    }

    private static T GetSelectedValue<T>(ComboBox combo)
    {
        if (combo.SelectedItem is OptionItem<T> item)
        {
            return item.Value;
        }

        throw new InvalidOperationException("A required training rule value is missing.");
    }

    private static void SetSelectedValue<T>(ComboBox combo, T value, string optionName)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is OptionItem<T> item &&
                EqualityComparer<T>.Default.Equals(item.Value, value))
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        throw new InvalidOperationException($"Missing editor option for training rule {optionName} '{value}'.");
    }

    private sealed class OptionItem<T>(T value, string text)
    {
        public T Value { get; } = value;

        public string Text { get; } = text;

        public override string ToString() => Text;
    }
}
