using System.Runtime.InteropServices;
using System.Text;

using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.Platform.Clipboard;

public sealed class Win32ClipboardService : IClipboardService
{
    public Task<OperationResult> CopyTextAsync(string text, string? html, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();

        if (!OpenClipboard(IntPtr.Zero))
        {
            return Task.FromResult(OperationResult.Failure("ClipboardUnavailable", "Clipboard is currently unavailable."));
        }

        try
        {
            _ = EmptyClipboard();
            byte[] bytes = Encoding.Unicode.GetBytes(text + '\0');
            IntPtr handle = GlobalAlloc(0x0042, (UIntPtr)bytes.Length);
            if (handle == IntPtr.Zero)
            {
                return Task.FromResult(OperationResult.Failure("ClipboardAllocationFailed", "Could not allocate clipboard memory."));
            }

            IntPtr target = GlobalLock(handle);
            Marshal.Copy(bytes, 0, target, bytes.Length);
            _ = GlobalUnlock(handle);

            if (SetClipboardData(13, handle) == IntPtr.Zero)
            {
                _ = GlobalFree(handle);
                return Task.FromResult(OperationResult.Failure("ClipboardSetFailed", "Could not write text to the clipboard."));
            }

            return Task.FromResult(OperationResult.Success());
        }
        finally
        {
            _ = CloseClipboard();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}