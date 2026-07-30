using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using Serilog.Events;
using WindowsAssistant.Util.Serilog;

namespace WindowsAssistant.Core.Views;

/// <summary>
/// Read-only multiline text box with a rounded, anti-aliased border.
/// </summary>
/// <remarks>
/// A native WinForms <see cref="TextBox"/> only provides rectangular borders. This control hosts
/// a borderless text box and draws the missing rounded border around it.
/// </remarks>
internal sealed class RoundedTextBox : UserControl, ILogMessageObserver
{
    // core: The native rich-text box retains text selection while allowing per-message colors.
    private readonly RichTextBox textBox = new()
    {
        BackColor = Color.White,
        BorderStyle = BorderStyle.None,
        DetectUrls = false,
        Dock = DockStyle.Fill,
        ReadOnly = true,
        ScrollBars = RichTextBoxScrollBars.None,
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

    public void OnLogMessage(string message, LogEventLevel level)
    {
        // meta: The observer owns its UI-thread boundary so the observable remains UI-agnostic.
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(() => OnLogMessage(message, level)));
            }
            catch (InvalidOperationException)
            {
                // util: The control lost its handle while the application was shutting down.
            }
        }
        else
        {
            textBox.SelectionStart = textBox.TextLength;
            textBox.SelectionLength = 0;
            textBox.SelectionColor = level switch
            {
                LogEventLevel.Verbose => Color.Gray,
                LogEventLevel.Debug => Color.DimGray,
                LogEventLevel.Information => Color.Black,
                LogEventLevel.Warning => Color.DarkOrange,
                LogEventLevel.Error => Color.Firebrick,
                LogEventLevel.Fatal => Color.DarkRed,
                _ => Color.Black,
            };
            textBox.AppendText(message);
            textBox.SelectionColor = textBox.ForeColor;
        }
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
