using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using PromptNest.Core.Abstractions;

namespace PromptNest.Platform.Hotkeys;

[SupportedOSPlatform("windows")]
public sealed class Win32GlobalHotkeyService : IGlobalHotkeyService
{
    private const int HotkeyId = 0x504E;
    private const int WmHotkey = 0x0312;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint VkSpace = 0x20;

    private Thread? messageThread;
    private uint threadId;

    public event EventHandler? HotkeyPressed;

    public bool IsRegistered { get; private set; }

    public string? RegistrationError { get; private set; }

    public Task RegisterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsRegistered || messageThread is not null)
        {
            return Task.CompletedTask;
        }

        var ready = new ManualResetEventSlim();
        messageThread = new Thread(() => RunMessageLoop(ready))
        {
            IsBackground = true,
            Name = "PromptNestGlobalHotkey"
        };
        messageThread.SetApartmentState(ApartmentState.STA);
        messageThread.Start();
        ready.Wait(cancellationToken);

        return Task.CompletedTask;
    }

    public Task UnregisterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (threadId != 0)
        {
            _ = PostThreadMessage(threadId, WmQuit, UIntPtr.Zero, IntPtr.Zero);
        }

        IsRegistered = false;
        return Task.CompletedTask;
    }

    private void RunMessageLoop(ManualResetEventSlim ready)
    {
        threadId = GetCurrentThreadId();
        IsRegistered = RegisterHotKey(IntPtr.Zero, HotkeyId, ModWin | ModShift, VkSpace);
        RegistrationError = IsRegistered ? null : new Win32Exception(Marshal.GetLastWin32Error()).Message;
        ready.Set();

        while (GetMessage(out MSG message, IntPtr.Zero, 0, 0) > 0)
        {
            if (message.message == WmHotkey && (int)message.wParam == HotkeyId)
            {
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
            }
        }

        if (IsRegistered)
        {
            _ = UnregisterHotKey(IntPtr.Zero, HotkeyId);
        }

        IsRegistered = false;
    }

    private const int WmQuit = 0x0012;

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint idThread, int msg, UIntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MSG
    {
        public readonly IntPtr hwnd;
        public readonly uint message;
        public readonly UIntPtr wParam;
        public readonly IntPtr lParam;
        public readonly uint time;
        public readonly int ptX;
        public readonly int ptY;
    }
}