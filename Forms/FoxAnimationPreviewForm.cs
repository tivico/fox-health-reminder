using HealthReminder.Controls;
using HealthReminder.UI;

namespace HealthReminder.Forms;

/// <summary>
/// 讓使用者不必等待提醒觸發，即可直接切換並驗收狐狸動畫。
/// </summary>
public sealed class FoxAnimationPreviewForm : Form
{
    private readonly FoxAnimationControl _fox;
    private readonly Label _stateLabel;

    public FoxAnimationPreviewForm()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = AppTheme.WindowBackground;
        ClientSize = new Size(620, 390);
        Font = new Font(AppTheme.FontFamily, 9F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "小狐狸動畫預覽";

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.WindowBackground,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20),
            Margin = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

        var previewCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(12)
        };
        _fox = new FoxAnimationControl
        {
            Anchor = AnchorStyles.None,
            BackColor = AppTheme.CardBackground,
            Location = new Point(118, 8),
            Size = new Size(220, 220)
        };
        previewCard.Controls.Add(_fox);
        previewCard.Resize += (_, _) =>
        {
            _fox.Left = Math.Max(0, (previewCard.ClientSize.Width - _fox.Width) / 2);
            _fox.Top = Math.Max(0, (previewCard.ClientSize.Height - _fox.Height) / 2);
        };

        _stateLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font(AppTheme.FontFamily, 10F, FontStyle.Bold),
            ForeColor = AppTheme.PrimaryText,
            Text = "目前：待機",
            TextAlign = ContentAlignment.MiddleCenter
        };

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.WindowBackground,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(16, 8, 0, 0),
            WrapContents = false
        };
        actions.Controls.Add(CreateAnimationButton("待機", FoxAnimationState.Wait));
        actions.Controls.Add(CreateAnimationButton("揮手", FoxAnimationState.Wave));
        actions.Controls.Add(CreateAnimationButton("喝水", FoxAnimationState.Drink));
        actions.Controls.Add(CreateAnimationButton("護眼", FoxAnimationState.EyeRest));
        actions.Controls.Add(CreateAnimationButton("伸展", FoxAnimationState.Stretch));

        root.Controls.Add(previewCard, 0, 0);
        root.Controls.Add(_stateLabel, 0, 1);
        root.Controls.Add(actions, 0, 2);
        Controls.Add(root);
    }

    private Button CreateAnimationButton(string text, FoxAnimationState state)
    {
        var button = new Button
        {
            BackColor = AppTheme.Primary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font(AppTheme.FontFamily, 9F, FontStyle.Bold),
            Margin = new Padding(4, 0, 4, 0),
            Size = new Size(96, 38),
            Text = text,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) =>
        {
            _fox.Restart(state);
            _stateLabel.Text = $"目前：{text}";
        };
        return button;
    }
}
