using Microsoft.Extensions.Logging;
using WindowsAssistant.Meta;
using WindowsAssistant.Util.Buzz;
using WindowsAssistant.Util.Serilog;
using Buzz = WindowsAssistant.Util.Buzz;

namespace WindowsAssistant.Core.Views;

// The client thread that calls SetWinEventHook must have a message loop in order to receive events.
// https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook

internal class HookForm : Form
{
    private static IntPtr hook;
    private static Win32.WinEventDelegate procDelegate = null!; // This won't be null after the form loads.
    private ILogger<HookForm> Logger { get; } = null!;
    private Buzz.MatchWindow MatchWindow { get; } = null!;
    private Buzz.ResizeWindow ResizeWindow { get; } = null!;
    private Buzz.SendKeys SendKeys { get; } = null!;
    private IDisposable? LogSubscription { get; set; }

    // meta: Visual Studio's forms designer requires a parameterless constructor.
    public HookForm()
    {
        InitializeComponent();
    }

    public HookForm(ObservableLogSink observableLogSink, ILogger<HookForm> logger, Buzz.MatchWindow matchWindow, Buzz.ResizeWindow resizeWindow, Buzz.SendKeys sendKeys) : this()
    {
        Logger = logger;
        MatchWindow = matchWindow;
        ResizeWindow = resizeWindow;
        SendKeys = sendKeys;
        LogSubscription = observableLogSink.Subscribe(matchLogTextBox);
        Text = "Window Assistant v1.2.0";
        procDelegate = WinEventCallback;
        Load += HookForm_Load;
        //Shown += (_, _) => Hide(); // Hide the form after showing.
    }

    private void HookForm_Load(object? sender, EventArgs e)
    {
        // meta: Keep the controller form usable on whichever monitor Windows selects.
        var screen = Screen.FromControl(this).WorkingArea;
        StartPosition = FormStartPosition.CenterScreen;
        Location = new Point
        (
            screen.Left + (screen.Width - Width) / 2,
            screen.Top + (screen.Height - Height) / 2
        );

        // meta: Install the hook only after the form's message loop is ready.
        hook = Win32.SetWinEventHook(Win32.EVENT_OBJECT_CREATE, Win32.EVENT_OBJECT_CREATE, IntPtr.Zero, procDelegate, 0, 0, Win32.WINEVENT_OUTOFCONTEXT);

        if (hook == IntPtr.Zero)
        {
            Logger.LogError("Window hook not installed.");
        }
        else
        {
            Logger.LogInformation("Window hook active.");
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // meta: Release the native hook before the message loop disappears.
        if (hook != IntPtr.Zero)
        {
            Win32.UnhookWinEvent(hook);
            Logger.LogInformation("Window hook removed.");
            hook = IntPtr.Zero;
        }

        LogSubscription?.Dispose();
        base.OnFormClosed(e);
    }

    private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hWnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // core: Apply the first configured rule whose title pattern matches.
        if (MatchWindow.Execute(hWnd, idObject) is { } options)
        {
            ResizeWindow.Execute(hWnd, options.SizeFactor.Width, options.SizeFactor.Height);
            _ = SendKeys.ExecuteAsync(hWnd, options.SendKeys);
        }
    }

    private void ExitButton_Click(object? sender, EventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        exitButton = new Button();
        matchLogTextBox = new RoundedTextBox();
        SuspendLayout();
        // 
        // exitButton
        // 
        exitButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        exitButton.Font = new Font("Segoe UI", 12F);
        exitButton.Location = new Point(12, 271);
        exitButton.Name = "exitButton";
        exitButton.Size = new Size(674, 78);
        exitButton.TabIndex = 0;
        exitButton.Text = "Exit";
        exitButton.UseVisualStyleBackColor = true;
        exitButton.Click += ExitButton_Click;
        // 
        // matchLogTextBox
        // 
        matchLogTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        matchLogTextBox.BackColor = Color.Transparent;
        matchLogTextBox.Location = new Point(12, 12);
        matchLogTextBox.Name = "matchLogTextBox";
        matchLogTextBox.Padding = new Padding(7, 6, 7, 6);
        matchLogTextBox.Size = new Size(674, 253);
        matchLogTextBox.TabIndex = 1;
        matchLogTextBox.TabStop = false;
        // 
        // HookForm
        // 
        ClientSize = new Size(698, 354);
        Controls.Add(matchLogTextBox);
        Controls.Add(exitButton);
        Name = "HookForm";
        ResumeLayout(false);
    }

    private System.Windows.Forms.Button exitButton = null!;
    private RoundedTextBox matchLogTextBox = null!;
}
