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

    private readonly CheckBox _chkEnabled;
    private readonly Label _lblCondition;
    private readonly ComboBox _cmbField;
    private readonly ComboBox _cmbOperator;
    private readonly NumericUpDown _numValue;
    private readonly CheckBox _chkSecondCondition;
    private readonly ComboBox _cmbSecondField;
    private readonly ComboBox _cmbSecondOperator;
    private readonly NumericUpDown _numSecondValue;
    private readonly Label _lblAction;
    private readonly ComboBox _cmbAction;
    private readonly RoundedButton _btnUp;
    private readonly RoundedButton _btnDown;
    private readonly RoundedButton _btnDelete;
    private string _ruleId = string.Empty;

    public event Action<TrainingRuleCardControl>? MoveUpRequested;
    public event Action<TrainingRuleCardControl>? MoveDownRequested;
    public event Action<TrainingRuleCardControl>? DeleteRequested;

    public TrainingRuleCardControl(TrainingRuleCard rule)
    {
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Margin = new Padding(0, 0, 0, 10);

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

        _chkSecondCondition = new CheckBox
        {
            Text = UiText.Training.ExtraCondition,
            AutoSize = false,
            Font = RaceTheme.SmallFont(),
            ForeColor = RaceTheme.TextSecondary,
            BackColor = Color.Transparent,
            CheckAlign = ContentAlignment.MiddleRight,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _chkSecondCondition.CheckedChanged += (_, _) => UpdateSecondConditionState();

        _cmbSecondField = CreateComboBox();
        _cmbSecondOperator = CreateComboBox();
        _numSecondValue = new NumericUpDown
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
            _chkEnabled,
            _lblCondition,
            _cmbField,
            _cmbOperator,
            _numValue,
            _chkSecondCondition,
            _cmbSecondField,
            _cmbSecondOperator,
            _numSecondValue,
            _lblAction,
            _cmbAction,
            _btnUp,
            _btnDown,
            _btnDelete,
        ]);

        PopulateFieldOptions(_cmbField);
        PopulateFieldOptions(_cmbSecondField);
        PopulateOperatorOptions(_cmbOperator);
        PopulateOperatorOptions(_cmbSecondOperator);
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
        string id = _ruleId.Trim();
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

        var card = new TrainingRuleCard
        {
            Id = id,
            Action = GetSelectedValue<TrainingDecisionAction>(_cmbAction),
            Enabled = _chkEnabled.Checked,
            IsFallback = false,
        };

        AddCondition(card, _cmbField, _cmbOperator, _numValue);
        if (_chkSecondCondition.Checked)
        {
            AddCondition(card, _cmbSecondField, _cmbSecondOperator, _numSecondValue);
        }

        TrainingRuleCondition first = card.Conditions[0];
        card.Field = first.Field;
        card.Operator = first.Operator;
        card.Value = first.Value;
        return card;
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
        Height = IsFallback ? 104 : 180;

        _ruleId = rule.Id;
        _chkEnabled.Checked = rule.Enabled;

        IReadOnlyList<TrainingRuleCondition> conditions = GetConditions(rule);
        if (conditions.Count > 0)
        {
            ApplyCondition(conditions[0], _cmbField, _cmbOperator, _numValue, "field");
        }

        if (conditions.Count > 1)
        {
            _chkSecondCondition.Checked = true;
            ApplyCondition(conditions[1], _cmbSecondField, _cmbSecondOperator, _numSecondValue, "second field");
        }
        else
        {
            _chkSecondCondition.Checked = false;
            ApplyCondition(
                new TrainingRuleCondition
                {
                    Field = TrainingRuleField.StrengthFailRate,
                    Operator = TrainingRuleOperator.LessThan,
                    Value = 40,
                },
                _cmbSecondField,
                _cmbSecondOperator,
                _numSecondValue,
                "second field");
        }

        SetSelectedValue(_cmbAction, rule.Action, "action");

        _lblCondition.Visible = !IsFallback;
        _cmbField.Visible = !IsFallback;
        _cmbOperator.Visible = !IsFallback;
        _numValue.Visible = !IsFallback;
        _chkSecondCondition.Visible = !IsFallback;
        _lblAction.Text = IsFallback ? UiText.Training.Fallback : UiText.Training.Action;
        _btnDelete.Visible = !IsFallback;
        UpdateSecondConditionState();
        LayoutControls();
    }

    private void LayoutControls()
    {
        if (Width <= 0)
        {
            return;
        }

        int rightButtonsWidth = ButtonWidth * 3 + ButtonGap * 2;
        _chkEnabled.SetBounds(Pad, Pad, 86, RowHeight);

        int buttonLeft = Width - Pad - rightButtonsWidth;
        _btnUp.SetBounds(buttonLeft, Pad, ButtonWidth, RowHeight);
        _btnDown.SetBounds(_btnUp.Right + ButtonGap, Pad, ButtonWidth, RowHeight);
        _btnDelete.SetBounds(_btnDown.Right + ButtonGap, Pad, ButtonWidth, RowHeight);

        int actionRowY = IsFallback ? Pad + RowHeight + 12 : Pad + (RowHeight + 8) * 3;
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

            int secondConditionRowY = conditionRowY + RowHeight + 8;
            _chkSecondCondition.SetBounds(Pad, secondConditionRowY, LabelWidth, RowHeight);
            _cmbSecondField.SetBounds(conditionLeft, secondConditionRowY, fieldWidth, RowHeight);
            _cmbSecondOperator.SetBounds(_cmbSecondField.Right + ButtonGap, secondConditionRowY, 80, RowHeight);
            _numSecondValue.SetBounds(_cmbSecondOperator.Right + ButtonGap, secondConditionRowY, 86, RowHeight);
        }

        _lblAction.SetBounds(Pad, actionRowY, LabelWidth, RowHeight);
        _cmbAction.SetBounds(actionLeft, actionRowY, actionWidth, RowHeight);
    }

    private static void AddCondition(
        TrainingRuleCard card,
        ComboBox fieldCombo,
        ComboBox operatorCombo,
        NumericUpDown valueInput)
    {
        card.Conditions.Add(new TrainingRuleCondition
        {
            Field = GetSelectedValue<TrainingRuleField>(fieldCombo),
            Operator = GetSelectedValue<TrainingRuleOperator>(operatorCombo),
            Value = Decimal.ToInt32(valueInput.Value),
        });
    }

    private static void ApplyCondition(
        TrainingRuleCondition condition,
        ComboBox fieldCombo,
        ComboBox operatorCombo,
        NumericUpDown valueInput,
        string optionName)
    {
        SetSelectedValue(fieldCombo, condition.Field, optionName);
        SetSelectedValue(operatorCombo, condition.Operator, "operator");
        valueInput.Value = Math.Clamp(condition.Value, Decimal.ToInt32(valueInput.Minimum), Decimal.ToInt32(valueInput.Maximum));
    }

    private static IReadOnlyList<TrainingRuleCondition> GetConditions(TrainingRuleCard rule)
    {
        if (rule.Conditions.Count > 0)
        {
            return rule.Conditions;
        }

        if (rule.Field is null || rule.Operator is null || !rule.Value.HasValue)
        {
            return [];
        }

        return
        [
            new TrainingRuleCondition
            {
                Field = rule.Field.Value,
                Operator = rule.Operator.Value,
                Value = rule.Value.Value,
            },
        ];
    }

    private void UpdateSecondConditionState()
    {
        bool visible = !IsFallback && _chkSecondCondition.Checked;
        _cmbSecondField.Visible = visible;
        _cmbSecondOperator.Visible = visible;
        _numSecondValue.Visible = visible;
    }

    private static void PopulateFieldOptions(ComboBox combo)
    {
        combo.Items.AddRange(
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

    private static void PopulateOperatorOptions(ComboBox combo)
    {
        combo.Items.AddRange(
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

    private static ComboBox CreateComboBox() => new WheelSafeComboBox()
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

    private sealed class WheelSafeComboBox : ComboBox
    {
        private const int WmMouseWheel = 0x020A;
        private const int WmMouseHWheel = 0x020E;

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (e is HandledMouseEventArgs handled)
            {
                handled.Handled = true;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg is WmMouseWheel or WmMouseHWheel)
            {
                return;
            }

            base.WndProc(ref m);
        }
    }
}
