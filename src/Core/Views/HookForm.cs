using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using WindowsAssistant.Meta;

namespace WindowsAssistant.Core.Views;

// The client thread that calls SetWinEventHook must have a message loop in order to receive events.
// https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook

internal class HookForm : Form
{
    private static IntPtr hook;
    private static Win32.WinEventDelegate procDelegate = null!; // This won't be null after the form loads.
    private IReadOnlyList<ObjectCreateOptions> ObjectCreateOptions { get; } = [];
    internal RoundedTextBox LogTextBox => matchLogTextBox;

    public HookForm()
    {
        InitializeComponent();
    }

    public HookForm(IEnumerable<ObjectCreateOptions> objectCreateOptions) : this()
    {
        ObjectCreateOptions = objectCreateOptions.ToArray();
        Text = "Window Assistant v1.1.0";
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
            Console.WriteLine("Failed to set OBJECT_CREATE hook.");
        }
        else
        {
            Console.WriteLine("OBJECT_CREATE hook set.");
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // meta: Release the native hook before the message loop disappears.
        if (hook != IntPtr.Zero)
        {
            Win32.UnhookWinEvent(hook);
            Console.WriteLine("OBJECT_CREATE hook removed.");
            hook = IntPtr.Zero;
        }

        base.OnFormClosed(e);
    }

    private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hWnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // core: Only native window creation events can represent configured targets.
        if (hWnd == IntPtr.Zero || idObject != Win32.OBJID_WINDOW)
        {
            return;
        }

        // Get the window title
        var title = new StringBuilder(256);
        if (Win32.GetWindowText(hWnd, title, title.Capacity) == 0)
        {
            return;
        }

        //Console.WriteLine($"Detected window: {title}"); // for debugging

        // core: Apply the first configured rule whose title pattern matches.
        if (FindMatchingRule(title.ToString()) is { } options)
        {
            matchLogTextBox.AppendText($"{DateTime.Now:HH:mm:ss}  {title} -> {options.TitlePattern}{Environment.NewLine}");
            Console.WriteLine($"Window created: {options.TitlePattern}");

            ResizeWindow(hWnd, options.SizeFactor);
            _ = SendKeysAsync(hWnd, options.SendKeys);
        }
    }

    /// <summary>
    /// Resizes the target window and centers it within its current monitor's working area.
    /// </summary>
    private static void ResizeWindow(IntPtr hWnd, SizeFactorOptions sizeFactor)
    {
        if (!Win32.GetWindowRect(hWnd, out var windowRect))
        {
            Console.WriteLine($"Failed to read window bounds. Win32 error: {Marshal.GetLastWin32Error()}.");
            return;
        }

        // core: Resize using the independent width and height factors.
        var newWidth = (int)(windowRect.Width * sizeFactor.Width);
        var newHeight = (int)(windowRect.Height * sizeFactor.Height);

        // core: Center relative to the monitor containing the target window.
        var screenBounds = Screen.FromHandle(hWnd).WorkingArea;
        var newX = screenBounds.Left + (screenBounds.Width - newWidth) / 2;
        var newY = screenBounds.Top + (screenBounds.Height - newHeight) / 2;

        if (Win32.SetWindowPos(hWnd, IntPtr.Zero, newX, newY, newWidth, newHeight, 0))
        {
            Console.WriteLine($"Window resized by X={sizeFactor.Width:0.0} and Y={sizeFactor.Height:0.0}.");
        }
        else
        {
            Console.WriteLine($"Failed to resize window. Win32 error: {Marshal.GetLastWin32Error()}.");
        }
    }

    /// <summary>
    /// Sends the configured key sequences after their optional delays.
    /// </summary>
    /// <remarks>
    /// This method owns all delayed work so the native WinEvent callback remains synchronous.
    /// It catches its own exceptions because the callback intentionally does not await it.
    /// </remarks>
    private static async Task SendKeysAsync(IntPtr hWnd, IEnumerable<SendKeysOptions>? sendKeysOptions)
    {
        try
        {
            foreach (var keys in sendKeysOptions ?? [])
            {
                // core: Give applications time to finish initializing the target window.
                if (keys.DelayMs is > 0)
                {
                    await Task.Delay(keys.DelayMs.Value);
                }

                if (!Win32.IsWindow(hWnd))
                {
                    Console.WriteLine("Skipped configured keys because the window no longer exists.");
                    return;
                }

                // core: Never inject input unless the intended window owns the foreground.
                if (!Win32.SetForegroundWindow(hWnd) || Win32.GetForegroundWindow() != hWnd)
                {
                    Console.WriteLine("Skipped configured keys because the window could not be activated.");
                    return;
                }

                System.Windows.Forms.SendKeys.SendWait(keys.Sequence);
                Console.WriteLine(keys.Description);
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Failed to send configured keys: {exception.Message}");
        }
    }

    /// <summary>
    /// Finds the first configured window rule whose compiled regular expression matches the
    /// supplied window title.
    /// </summary>
    /// <remarks>
    /// Matching is kept outside the native WinEvent callback's main flow so malformed patterns
    /// and regex timeouts can be contained per rule. A bad pattern is logged and skipped instead
    /// of terminating the callback or preventing later rules from being considered.
    /// </remarks>
    private ObjectCreateOptions? FindMatchingRule(string title)
    {
        // util: Rules retain configuration order, so the first successful match wins.
        foreach (var options in ObjectCreateOptions)
        {
            try
            {
                if (options.TitleRegex.IsMatch(title))
                {
                    return options;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                Console.WriteLine($"Title pattern timed out: {options.TitlePattern}");
            }
            catch (ArgumentException exception)
            {
                Console.WriteLine($"Skipped invalid title pattern '{options.TitlePattern}': {exception.Message}");
            }
        }

        return null;
    }

    private void button1_Click(object? sender, EventArgs e)
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
        exitButton.Click += button1_Click;
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
