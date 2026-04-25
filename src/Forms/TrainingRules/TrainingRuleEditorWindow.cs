using System.Windows.Forms;
using SleepRunner.Automation.Race.Policy.Training;
using SleepRunner.Forms;
using SleepRunner.Forms.Controls;
using SleepRunner.Utils;

namespace SleepRunner.Forms.TrainingRules;

internal sealed class TrainingRuleEditorWindow : Form
{
    private readonly string _profileName;
    private readonly string _savePath;
    private readonly string _originalSourcePath;
    private readonly TrainingLegacyStrategy _legacyStrategy;
    private readonly Label _lblProfile;
    private readonly Label _lblHint;
    private readonly BufferedCardsPanel _cardsPanel;
    private readonly RoundedButton _btnAddRule;
    private readonly RoundedButton _btnSave;
    private readonly RoundedButton _btnCancel;
    private readonly List<TrainingRuleCardControl> _ruleControls = new();

    private TrainingRuleEditorWindow(string title, string profileName, string savePath, string originalSourcePath, TrainingRuleProfile profile)
    {
        _profileName = profileName;
        _savePath = savePath;
        _originalSourcePath = originalSourcePath;
        _legacyStrategy = CloneLegacyStrategy(profile.LegacyStrategy);

        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        BackColor = RaceTheme.Bg;
        ForeColor = RaceTheme.TextPrimary;
        Font = RaceTheme.BodyFont();
        DoubleBuffered = true;

        _lblProfile = new Label
        {
            Text = UiText.Training.ProfileCaption(_profileName),
            Font = RaceTheme.BoldFont(11.25F),
            ForeColor = RaceTheme.TextPrimary,
            BackColor = Color.Transparent,
        };

        _lblHint = new Label
        {
            Text = UiText.Training.EditorHint,
            Font = RaceTheme.SmallFont(),
            ForeColor = RaceTheme.TextSecondary,
            BackColor = Color.Transparent,
        };

        _cardsPanel = new BufferedCardsPanel
        {
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = RaceTheme.Bg,
        };
        _cardsPanel.SizeChanged += (_, _) => RefreshCardWidths();

        _btnAddRule = new RoundedButton
        {
            Text = UiText.Actions.AddRule,
            Variant = RoundedButton.ButtonVariant.Secondary,
            AccentColor = RaceTheme.Accent,
            ForeColor = RaceTheme.Accent,
            BackdropColor = RaceTheme.Bg,
            Font = RaceTheme.BoldFont(),
        };
        _btnAddRule.Click += (_, _) => AddRuleCard(CreateDefaultRule());

        _btnSave = new RoundedButton
        {
            Text = UiText.Actions.Save,
            Variant = RoundedButton.ButtonVariant.Primary,
            AccentColor = RaceTheme.Success,
            ForeColor = Color.White,
            Font = RaceTheme.BoldFont(),
        };
        _btnSave.Click += (_, _) => SaveProfile();

        _btnCancel = new RoundedButton
        {
            Text = UiText.Actions.Cancel,
            Variant = RoundedButton.ButtonVariant.Ghost,
            ForeColor = RaceTheme.TextPrimary,
            BackdropColor = RaceTheme.Bg,
            Font = RaceTheme.BoldFont(),
        };
        _btnCancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        Controls.Add(_lblProfile);
        Controls.Add(_lblHint);
        Controls.Add(_cardsPanel);
        Controls.Add(_btnAddRule);
        Controls.Add(_btnSave);
        Controls.Add(_btnCancel);

        MinimumSize = new Size(760, 580);
        ClientSize = new Size(820, 620);

        LoadProfile(profile);
        LayoutControls();
    }

    public string SavedProfileName { get; private set; } = string.Empty;

    public static bool TryEditProfile(IWin32Window owner, string profileName, TrainingRuleProfile sourceProfile, out string savedProfileName)
    {
        string savePath = TrainingRuleProfileManager.ResolvePath(profileName);
        using var window = new TrainingRuleEditorWindow(
            UiText.Training.EditTitle(profileName),
            profileName,
            savePath,
            sourceProfile.SourcePath,
            CloneProfile(sourceProfile, savePath));

        bool saved = window.ShowDialog(owner) == DialogResult.OK;
        savedProfileName = saved ? window.SavedProfileName : string.Empty;
        return saved;
    }

    public static bool TryDuplicateProfile(IWin32Window owner, string sourceProfileName, TrainingRuleProfile sourceProfile, out string savedProfileName)
    {
        string initialName = BuildDuplicateProfileName(sourceProfileName);
        if (!TrainingProfileNameDialog.TryPrompt(owner, UiText.Training.DuplicateProfilePromptTitle, initialName, out string profileName))
        {
            savedProfileName = string.Empty;
            return false;
        }

        string savePath = TrainingRuleProfileManager.ResolvePath(profileName);
        using var window = new TrainingRuleEditorWindow(
            UiText.Training.DuplicateTitle(profileName),
            profileName,
            savePath,
            sourceProfile.SourcePath,
            CloneProfile(sourceProfile, savePath));

        bool saved = window.ShowDialog(owner) == DialogResult.OK;
        savedProfileName = saved ? window.SavedProfileName : string.Empty;
        return saved;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutControls();
    }

    private void LoadProfile(TrainingRuleProfile profile)
    {
        _ruleControls.Clear();
        _cardsPanel.Controls.Clear();

        List<TrainingRuleCard> orderedRules = profile.Rules
            .Where(rule => !rule.IsFallback)
            .Select(CloneRule)
            .ToList();

        TrainingRuleCard fallbackRule = profile.Rules.FirstOrDefault(rule => rule.IsFallback) is { } existingFallback
            ? CloneRule(existingFallback)
            : new TrainingRuleCard
            {
                Id = "fallback",
                Action = TrainingDecisionAction.BuiltinDefault,
                Enabled = true,
                IsFallback = true,
            };

        foreach (TrainingRuleCard rule in orderedRules)
        {
            AddRuleCard(rule);
        }

        AddRuleCard(fallbackRule);
        RefreshRuleOrdering();
    }

    private void AddRuleCard(TrainingRuleCard rule)
    {
        var cardControl = new TrainingRuleCardControl(rule);
        cardControl.MoveUpRequested += HandleMoveUpRequested;
        cardControl.MoveDownRequested += HandleMoveDownRequested;
        cardControl.DeleteRequested += HandleDeleteRequested;

        int insertIndex = rule.IsFallback ? _ruleControls.Count : FindFallbackIndex();
        if (insertIndex < 0)
        {
            insertIndex = _ruleControls.Count;
        }

        _ruleControls.Insert(insertIndex, cardControl);
        RefreshRuleOrdering();
    }

    private void HandleMoveUpRequested(TrainingRuleCardControl cardControl)
    {
        MoveCard(cardControl, -1);
    }

    private void HandleMoveDownRequested(TrainingRuleCardControl cardControl)
    {
        MoveCard(cardControl, 1);
    }

    private void HandleDeleteRequested(TrainingRuleCardControl cardControl)
    {
        if (cardControl.IsFallback)
        {
            return;
        }

        _ruleControls.Remove(cardControl);
        _cardsPanel.Controls.Remove(cardControl);
        cardControl.Dispose();
        RefreshRuleOrdering();
    }

    private void MoveCard(TrainingRuleCardControl cardControl, int delta)
    {
        if (cardControl.IsFallback)
        {
            return;
        }

        int index = _ruleControls.IndexOf(cardControl);
        if (index < 0)
        {
            return;
        }

        int fallbackIndex = FindFallbackIndex();
        int lastNormalIndex = fallbackIndex >= 0 ? fallbackIndex - 1 : _ruleControls.Count - 1;
        int targetIndex = Math.Clamp(index + delta, 0, lastNormalIndex);
        if (targetIndex == index)
        {
            return;
        }

        _ruleControls.RemoveAt(index);
        _ruleControls.Insert(targetIndex, cardControl);
        RefreshRuleOrdering();
    }

    private void RefreshRuleOrdering()
    {
        _cardsPanel.SuspendLayout();
        _cardsPanel.Controls.Clear();

        for (int i = 0; i < _ruleControls.Count; i++)
        {
            TrainingRuleCardControl control = _ruleControls[i];
            bool canMoveUp = !control.IsFallback && i > 0;
            bool canMoveDown = !control.IsFallback && i < _ruleControls.Count - 2;
            control.SetMoveState(canMoveUp, canMoveDown);
            _cardsPanel.Controls.Add(control);
        }

        _cardsPanel.ResumeLayout();
        RefreshCardWidths();
    }

    private void RefreshCardWidths()
    {
        int width = Math.Max(600, _cardsPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8);
        foreach (TrainingRuleCardControl control in _ruleControls)
        {
            control.Width = width;
        }
    }

    private void LayoutControls()
    {
        if (_lblProfile is null ||
            _lblHint is null ||
            _cardsPanel is null ||
            _btnAddRule is null ||
            _btnSave is null ||
            _btnCancel is null)
        {
            return;
        }

        int pad = 16;
        int buttonWidth = 116;
        int buttonHeight = 36;
        int buttonsY = ClientSize.Height - pad - buttonHeight;
        int panelTop = 74;
        int panelHeight = buttonsY - panelTop - 14;

        _lblProfile.SetBounds(pad, pad, ClientSize.Width - pad * 2, 26);
        _lblHint.SetBounds(pad, pad + 30, ClientSize.Width - pad * 2, 22);
        _cardsPanel.SetBounds(pad, panelTop, ClientSize.Width - pad * 2, Math.Max(220, panelHeight));

        _btnAddRule.SetBounds(pad, buttonsY, buttonWidth, buttonHeight);
        _btnCancel.SetBounds(ClientSize.Width - pad - buttonWidth, buttonsY, buttonWidth, buttonHeight);
        _btnSave.SetBounds(_btnCancel.Left - 10 - buttonWidth, buttonsY, buttonWidth, buttonHeight);

        RefreshCardWidths();
    }

    private void SaveProfile()
    {
        try
        {
            TrainingRuleProfile profile = BuildProfile();
            string json = TrainingRuleLoader.SaveToJson(profile);
            _ = TrainingRuleLoader.LoadFromJson(json, _savePath);

            if (File.Exists(_savePath) &&
                !string.Equals(_originalSourcePath, _savePath, StringComparison.OrdinalIgnoreCase))
            {
                DialogResult overwrite = MessageBox.Show(
                    this,
                    UiText.Training.OverwriteMessage(_profileName),
                    UiText.Training.DialogTitle,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (overwrite != DialogResult.Yes)
                {
                    return;
                }
            }

            TrainingRuleLoader.SaveToPath(profile, _savePath);
            Logger.Log($"[UI] Saved training profile '{_profileName}' to {_savePath}");

            SavedProfileName = _profileName;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, UiText.Training.SaveErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private TrainingRuleProfile BuildProfile()
    {
        var profile = new TrainingRuleProfile
        {
            SourcePath = _savePath,
        };
        profile.LegacyStrategy.BuildDirection = _legacyStrategy.BuildDirection;
        profile.LegacyStrategy.FailRateThreshold = _legacyStrategy.FailRateThreshold;
        profile.LegacyStrategy.RushThreshold = _legacyStrategy.RushThreshold;

        foreach (TrainingRuleCardControl control in _ruleControls)
        {
            profile.Rules.Add(control.ToRuleCard());
        }

        return profile;
    }

    private int FindFallbackIndex()
    {
        return _ruleControls.FindIndex(control => control.IsFallback);
    }

    private TrainingRuleCard CreateDefaultRule()
    {
        return new TrainingRuleCard
        {
            Id = BuildRuleId(),
            Field = TrainingRuleField.StrengthIcons,
            Operator = TrainingRuleOperator.GreaterThanOrEqual,
            Value = 1,
            Action = TrainingDecisionAction.TrainStrength,
            Enabled = true,
            IsFallback = false,
        };
    }

    private string BuildRuleId()
    {
        var usedIds = new HashSet<string>(
            _ruleControls.Select(control => control.ToRuleCard().Id).Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.OrdinalIgnoreCase);

        int suffix = usedIds.Count + 1;
        string candidate = $"rule_{suffix}";
        while (!usedIds.Add(candidate))
        {
            suffix++;
            candidate = $"rule_{suffix}";
        }

        return candidate;
    }

    private static string BuildDuplicateProfileName(string sourceProfileName)
    {
        return sourceProfileName.EndsWith("-copy", StringComparison.OrdinalIgnoreCase)
            ? sourceProfileName + "2"
            : sourceProfileName + "-copy";
    }

    private static TrainingRuleProfile CloneProfile(TrainingRuleProfile sourceProfile, string savePath)
    {
        var clone = new TrainingRuleProfile
        {
            SourcePath = savePath,
        };
        clone.LegacyStrategy.BuildDirection = sourceProfile.LegacyStrategy.BuildDirection;
        clone.LegacyStrategy.FailRateThreshold = sourceProfile.LegacyStrategy.FailRateThreshold;
        clone.LegacyStrategy.RushThreshold = sourceProfile.LegacyStrategy.RushThreshold;

        foreach (TrainingRuleCard rule in sourceProfile.Rules)
        {
            clone.Rules.Add(CloneRule(rule));
        }

        return clone;
    }

    private static TrainingRuleCard CloneRule(TrainingRuleCard rule)
    {
        return new TrainingRuleCard
        {
            Id = rule.Id,
            Field = rule.Field,
            Operator = rule.Operator,
            Value = rule.Value,
            Action = rule.Action,
            Enabled = rule.Enabled,
            IsFallback = rule.IsFallback,
        };
    }

    private static TrainingLegacyStrategy CloneLegacyStrategy(TrainingLegacyStrategy source)
    {
        return new TrainingLegacyStrategy
        {
            BuildDirection = source.BuildDirection,
            FailRateThreshold = source.FailRateThreshold,
            RushThreshold = source.RushThreshold,
        };
    }

    private sealed class BufferedCardsPanel : FlowLayoutPanel
    {
        public BufferedCardsPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.ResizeRedraw, true);
        }
    }
}
