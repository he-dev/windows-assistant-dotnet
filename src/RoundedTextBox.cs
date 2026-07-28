using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace WindowsAssistant;

/// <summary>
/// Read-only multiline text box with a rounded, anti-aliased border.
/// </summary>
/// <remarks>
/// A native WinForms <see cref="TextBox"/> only provides rectangular borders. This control hosts
/// a borderless text box and draws the missing rounded border around it.
/// </remarks>
internal sealed class RoundedTextBox : UserControl
{
    // core: The native text box retains standard text selection, rendering, and append behavior.
    private readonly TextBox textBox = new()
    {
        BackColor = Color.White,
        BorderStyle = BorderStyle.None,
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        TabStop = false,
    };

    public RoundedTextBox()
    {
        // meta: Custom painting and double buffering keep the rounded border smooth while resizing.
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor
            | ControlStyles.UserPaint,
            true);

        BackColor = Color.Transparent;
        Padding = new Padding(7, 6, 7, 6);
        TabStop = false;
        Controls.Add(textBox);
    }

    [AllowNull]
    public override string Text
    {
        get => textBox.Text;
        set => textBox.Text = value ?? string.Empty;
    }

    public void AppendText(string text)
    {
        // util: Expose only the operation needed by the match log.
        textBox.AppendText(text);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);

        // meta: Keep the hosted native text box synchronized with the public control.
        textBox.Font = Font;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        // core: Paint the white surface and rounded border behind the borderless native text box.
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var border = new GraphicsPath();
        border.AddRoundedRectangle(
            new RectangleF(0.5f, 0.5f, Width - 1, Height - 1),
            new SizeF(6, 6));

        e.Graphics.FillPath(Brushes.White, border);
        e.Graphics.DrawPath(SystemPens.ControlDark, border);
    }
}
