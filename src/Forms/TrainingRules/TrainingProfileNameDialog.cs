using System.Windows.Forms;
using SleepRunner.Forms;

namespace SleepRunner.Forms.TrainingRules;

internal sealed class TrainingProfileNameDialog : Form
{
    private readonly TextBox _txtName;

    public TrainingProfileNameDialog(string title, string initialValue)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(400, 148);
        BackColor = RaceTheme.Bg;
        ForeColor = RaceTheme.TextPrimary;
        Font = RaceTheme.BodyFont();

        var lblName = new Label
        {
            Text = UiText.Training.ProfileNameLabel,
            AutoSize = false,
            Font = RaceTheme.SmallFont(),
            ForeColor = RaceTheme.TextSecondary,
            BackColor = Color.Transparent,
        };
        lblName.SetBounds(18, 16, 140, 22);

        _txtName = new TextBox
        {
            Text = initialValue,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = RaceTheme.SurfaceSunken,
            ForeColor = RaceTheme.TextPrimary,
            Font = RaceTheme.BodyFont(),
        };
        _txtName.SetBounds(18, 44, 364, 32);

        var btnSave = new Button
        {
            Text = UiText.Actions.OpenEditor,
            DialogResult = DialogResult.None,
        };
        btnSave.SetBounds(170, 98, 102, 34);
        btnSave.Click += (_, _) => ConfirmName();

        var btnCancel = new Button
        {
            Text = UiText.Actions.Cancel,
            DialogResult = DialogResult.Cancel,
        };
        btnCancel.SetBounds(280, 98, 102, 34);

        Controls.Add(lblName);
        Controls.Add(_txtName);
        Controls.Add(btnSave);
        Controls.Add(btnCancel);

        AcceptButton = btnSave;
        CancelButton = btnCancel;
    }

    public string ProfileName => SanitizeProfileName(_txtName.Text);

    public static bool TryPrompt(IWin32Window owner, string title, string initialValue, out string profileName)
    {
        using var dialog = new TrainingProfileNameDialog(title, initialValue);
        if (dialog.ShowDialog(owner) == DialogResult.OK)
        {
            profileName = dialog.ProfileName;
            return true;
        }

        profileName = string.Empty;
        return false;
    }

    private void ConfirmName()
    {
        string cleaned = ProfileName;
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            MessageBox.Show(this, UiText.Training.EmptyProfileNameMessage, UiText.Training.DialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtName.Focus();
            return;
        }

        _txtName.Text = cleaned;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static string SanitizeProfileName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        string value = raw.Trim();
        foreach (char ch in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(ch.ToString(), string.Empty);
        }

        if (value.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^5];
        }

        return value.Trim();
    }
}
