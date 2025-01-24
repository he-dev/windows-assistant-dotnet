using System.Text;
using System.Text.RegularExpressions;

namespace WindowsAssistant;

// The client thread that calls SetWinEventHook must have a message loop in order to receive events.
// https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook

internal class HookForm : Form
{
    private static IntPtr hook;
    private static Win32.WinEventDelegate procDelegate = null!; // This won't be null after the form loads.

    public HookForm()
    {
        InitializeComponent();
    }

    public HookForm(IEnumerable<ObjectCreateOptions> objectCreateOptions) : this()
    {
        ObjectCreateOptions = objectCreateOptions;
        Text = "Window Assistant v1.0.0";
        procDelegate = WinEventCallback;
        Load += HookForm_Load;
        //Shown += (_, _) => Hide(); // Hide the form after showing.
    }

    private IEnumerable<ObjectCreateOptions> ObjectCreateOptions { get; }

    private void HookForm_Load(object? sender, EventArgs e)
    {
        // Set up the hook when the form loads
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
        // Clean up the hook when the form closes.
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
        // Get the window title
        var title = new StringBuilder(256);
        Win32.GetWindowText(hWnd, title, 256);

        //Console.WriteLine($"Detected window: {title}"); // for debugging

        // Is this the right window?
        if (ObjectCreateOptions.FirstOrDefault(x => Regex.IsMatch(title.ToString(), x.TitlePattern)) is { } options)
        {
            Console.WriteLine($"Window created: {options.TitlePattern}");

            //Thread.Sleep(1000);

            if (Win32.GetWindowRect(hWnd, out var windowRect))
            {
                // Original window size
                var originalWidth = windowRect.Width;
                var originalHeight = windowRect.Height;

                // Calc the new window size.
                var newWidth = (int)(originalWidth * options.SizeFactor.Width);
                var newHeight = (int)(originalHeight * options.SizeFactor.Width);

                // Get screen size.
                var screenBounds = Screen.PrimaryScreen!.Bounds; // I don't think this will ever be null.

                // Calc the new position to center the window.
                var newX = (screenBounds.Width - newWidth) / 2;
                var newY = (screenBounds.Height - newHeight) / 2;

                // Resize the window.
                Win32.SetWindowPos(hWnd, IntPtr.Zero, newX, newY, newWidth, newHeight, 0);
                Console.WriteLine("Window resized by X={options.WindowSizeFactor.Width:0.0} and Y={options.WindowSizeFactor.Height:0.0}.");
            }

            // Make sure send-keys work.
            Win32.SetForegroundWindow(hWnd);

            // This didn't work. At least not for the power-query editor.
            // Win32.SendMessage(hwnd, Win32.WM_KEYDOWN, Win32.VK_SHIFT, IntPtr.Zero);
            // Win32.SendMessage(hwnd, Win32.WM_KEYDOWN, Win32.VK_CONTROL, IntPtr.Zero);
            // Win32.SendMessage(hwnd, Win32.WM_KEYDOWN, Win32.VK_PLUS, IntPtr.Zero);
            // Win32.SendMessage(hwnd, Win32.WM_KEYUP, Win32.VK_PLUS, IntPtr.Zero);
            // Win32.SendMessage(hwnd, Win32.WM_KEYUP, Win32.VK_CONTROL, IntPtr.Zero);
            // Win32.SendMessage(hwnd, Win32.WM_KEYUP, Win32.VK_SHIFT, IntPtr.Zero);

            foreach (var keys in options.SendKeys ?? [])
            {
                SendKeys.SendWait(keys.Sequence);
                Console.WriteLine(keys.Description);
            }
        }
    }

    private void button1_Click(object sender, EventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        exitButton = new System.Windows.Forms.Button();
        SuspendLayout();
        //
        // exitButton
        //
        exitButton.Font = new System.Drawing.Font("Segoe UI", 12F);
        exitButton.Location = new System.Drawing.Point(12, 12);
        exitButton.Name = "exitButton";
        exitButton.Size = new System.Drawing.Size(345, 183);
        exitButton.TabIndex = 0;
        exitButton.Text = "Exit";
        exitButton.UseVisualStyleBackColor = true;
        exitButton.Click += button1_Click;
        //
        // HookForm
        //
        ClientSize = new System.Drawing.Size(369, 207);
        Controls.Add(exitButton);
        ResumeLayout(false);
    }

    private System.Windows.Forms.Button exitButton;
}