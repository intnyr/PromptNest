using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Automation;

using Microsoft.Data.Sqlite;

using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;
using PromptNest.Data.Db;
using PromptNest.Data.Migrations;
using PromptNest.Data.Repositories;

namespace PromptNest.SmokeTests;

public sealed class PromptNestDesktopSmokeTests
{
    [Fact]
    public async Task MainShellAndPaletteCriticalPathsWorkOnInteractiveDesktop()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("PROMPTNEST_RUN_UI_SMOKE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var repoRoot = FindRepositoryRoot();
        var appExe = Environment.GetEnvironmentVariable("PROMPTNEST_APP_EXE")
            ?? Path.Combine(repoRoot, "src", "PromptNest.App", "bin", "Debug", "net8.0-windows10.0.19041.0", "win-x64", "PromptNest.App.exe");

        Assert.True(File.Exists(appExe), $"PromptNest app executable was not found at {appExe}. Build the app before running smoke tests.");

        var smokeRoot = Path.Combine(Path.GetTempPath(), "PromptNest.Smoke", Guid.NewGuid().ToString("N"));
        await SeedSmokeDatabaseAsync(smokeRoot);

        using var process = StartApp(appExe, smokeRoot);

        try
        {
            AutomationElement mainWindow = WaitForWindow("PromptNest", TimeSpan.FromSeconds(30));
            ResizeWindow(mainWindow, width: 1400, height: 900);
            NativeMethods.ActivateWindow(mainWindow.Current.NativeWindowHandle);
            mainWindow.SetFocus();

            AutomationElement searchBox = FindByAutomationId(mainWindow, "PromptNest.SearchBox");
            AutomationElement titleBox = FindByAutomationId(mainWindow, "PromptNest.TitleBox");
            AutomationElement bodyEditor = FindByAutomationId(mainWindow, "PromptNest.PromptBodyEditor");
            AutomationElement tagInput = FindByAutomationId(mainWindow, "PromptNest.TagInput");
            TypeInto(searchBox, "market");
            WaitForText(mainWindow, "Market Analysis - Smoke", TimeSpan.FromSeconds(10));

            TypeInto(titleBox, "Market Analysis - Smoke Edited");
            TypeInto(bodyEditor, "Provide a smoke-tested overview for {{industry|SaaS}}.");
            TypeInto(tagInput, "strategy");
            NativeMethods.PressKey(NativeMethods.VirtualKeyEnter);
            AutomationElement saveButton = WaitForEnabled(mainWindow, "PromptNest.SaveButton", "Save", TimeSpan.FromSeconds(10));
            Invoke(saveButton);
            WaitForText(mainWindow, "Validation passed", TimeSpan.FromSeconds(10));

            TypeInto(titleBox, "Temporary unsaved title");
            AutomationElement cancelButton = WaitForEnabled(mainWindow, "PromptNest.CancelButton", "Cancel", TimeSpan.FromSeconds(10));
            Invoke(cancelButton);
            WaitForText(mainWindow, "Market Analysis - Smoke Edited", TimeSpan.FromSeconds(10));

            NativeMethods.PressHotkey(NativeMethods.VirtualKeyLeftWindows, NativeMethods.VirtualKeyShift, NativeMethods.VirtualKeySpace);
            AutomationElement paletteWindow = WaitForWindow("PromptNest Palette", TimeSpan.FromSeconds(10));
            AutomationElement paletteSearch = FindByAutomationId(paletteWindow, "PromptNest.PaletteSearchBox");

            TypeInto(paletteSearch, "copy");
            WaitForText(paletteWindow, "Copy Without Variables", TimeSpan.FromSeconds(10));
            NativeMethods.PressKey(NativeMethods.VirtualKeyEnter);
            WaitForText(paletteWindow, "Copied", TimeSpan.FromSeconds(10));
            NativeMethods.PressKey(NativeMethods.VirtualKeyEscape);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(3000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }

            SqliteConnection.ClearAllPools();
            DeleteDirectoryWithRetry(smokeRoot);
        }
    }

    private static Process StartApp(string appExe, string smokeRoot)
    {
        var startInfo = new ProcessStartInfo(appExe)
        {
            UseShellExecute = false
        };
        startInfo.Environment["PROMPTNEST_LOCALAPPDATA"] = smokeRoot;

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start PromptNest.");
    }

    private static async Task SeedSmokeDatabaseAsync(string localAppDataRoot)
    {
        var pathProvider = new SmokePathProvider(Path.Combine(localAppDataRoot, "PromptNest"));
        var connectionFactory = new SqliteConnectionFactory(pathProvider);
        var migrationRunner = new SqliteMigrationRunner(connectionFactory, pathProvider);
        await migrationRunner.MigrateAsync(CancellationToken.None);

        var promptRepository = new PromptRepository(connectionFactory);
        var folderRepository = new FolderRepository(connectionFactory);
        var now = DateTimeOffset.UtcNow;

        string folderId = await folderRepository.CreateAsync(
            new Folder { Id = "smoke", Name = "Smoke", CreatedAt = now },
            CancellationToken.None);

        await promptRepository.CreateAsync(
            new Prompt
            {
                Id = "market-smoke",
                Title = "Market Analysis - Smoke",
                Body = "Provide a comprehensive overview of the {{industry|SaaS}} industry.",
                FolderId = folderId,
                Tags = ["research", "market"],
                Variables = [new PromptVariable { Name = "industry", DefaultValue = "SaaS" }],
                CreatedAt = now,
                UpdatedAt = now
            },
            CancellationToken.None);

        await promptRepository.CreateAsync(
            new Prompt
            {
                Id = "copy-smoke",
                Title = "Copy Without Variables",
                Body = "Copy this stable prompt without variables.",
                FolderId = folderId,
                Tags = ["copy"],
                CreatedAt = now.AddMinutes(1),
                UpdatedAt = now.AddMinutes(1)
            },
            CancellationToken.None);
    }

    private static AutomationElement WaitForWindow(string name, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        var condition = new PropertyCondition(AutomationElement.NameProperty, name);

        while (DateTimeOffset.UtcNow < deadline)
        {
            AutomationElement? window = AutomationElement.RootElement.FindFirst(TreeScope.Children, condition);
            if (window is not null)
            {
                return window;
            }

            Thread.Sleep(250);
        }

        throw new TimeoutException($"Timed out waiting for window '{name}'.");
    }

    private static AutomationElement FindByAutomationId(AutomationElement root, string automationId)
    {
        var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
        return root.FindFirst(TreeScope.Descendants, condition)
            ?? throw new InvalidOperationException($"Unable to find automation element '{automationId}'.");
    }

    private static AutomationElement WaitForEnabled(AutomationElement root, string automationId, string name, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            AutomationElement? element = TryFindByAutomationId(root, automationId) ?? TryFindByName(root, name);
            if (element?.Current.IsEnabled == true)
            {
                return element;
            }

            Thread.Sleep(250);
        }

        throw new TimeoutException($"Timed out waiting for enabled automation element '{automationId}'.");
    }

    private static AutomationElement? TryFindByAutomationId(AutomationElement root, string automationId)
    {
        var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
        return root.FindFirst(TreeScope.Descendants, condition);
    }

    private static AutomationElement? TryFindByName(AutomationElement root, string name)
    {
        var condition = new PropertyCondition(AutomationElement.NameProperty, name);
        return root.FindFirst(TreeScope.Descendants, condition);
    }

    private static void WaitForText(AutomationElement root, string text, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        var condition = new PropertyCondition(AutomationElement.NameProperty, text);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (root.FindFirst(TreeScope.Descendants, condition) is not null)
            {
                return;
            }

            Thread.Sleep(250);
        }

        throw new TimeoutException($"Timed out waiting for text '{text}'.");
    }

    private static void SetValue(AutomationElement element, string value)
    {
        if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out object pattern))
        {
            throw new InvalidOperationException($"Element '{element.Current.AutomationId}' does not support ValuePattern.");
        }

        ((ValuePattern)pattern).SetValue(value);
    }

    private static void TypeInto(AutomationElement element, string value)
    {
        element.SetFocus();
        NativeMethods.ClickCenter(element.Current.BoundingRectangle);
        Thread.Sleep(100);
        NativeMethods.PressHotkey(NativeMethods.VirtualKeyControl, NativeMethods.VirtualKeyA);
        NativeMethods.TypeText(value);
    }

    private static void Invoke(AutomationElement element)
    {
        if (!element.TryGetCurrentPattern(InvokePattern.Pattern, out object pattern))
        {
            throw new InvalidOperationException($"Element '{element.Current.AutomationId}' does not support InvokePattern.");
        }

        ((InvokePattern)pattern).Invoke();
    }

    private static void ResizeWindow(AutomationElement window, double width, double height)
    {
        if (window.TryGetCurrentPattern(TransformPattern.Pattern, out object pattern))
        {
            var transform = (TransformPattern)pattern;
            if (transform.Current.CanResize)
            {
                transform.Resize(width, height);
                Thread.Sleep(500);
            }
        }
    }

    private static void DeleteDirectoryWithRetry(string path)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < 9)
            {
                Thread.Sleep(250);
            }
            catch (UnauthorizedAccessException) when (attempt < 9)
            {
                Thread.Sleep(250);
            }
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PromptNest.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Unable to locate PromptNest.sln.");
    }

    private sealed class SmokePathProvider : IPathProvider
    {
        public SmokePathProvider(string dataDirectory)
        {
            DataDirectory = dataDirectory;
            DatabasePath = Path.Combine(DataDirectory, "library.db");
            LogsDirectory = Path.Combine(DataDirectory, "logs");
            BackupsDirectory = Path.Combine(DataDirectory, "Backups");
            SettingsPath = Path.Combine(DataDirectory, "settings.json");
            UpdateCacheDirectory = Path.Combine(DataDirectory, "Updates");
        }

        public string DataDirectory { get; }

        public string DatabasePath { get; }

        public string LogsDirectory { get; }

        public string BackupsDirectory { get; }

        public string SettingsPath { get; }

        public string UpdateCacheDirectory { get; }

        public bool IsPackaged => false;
    }

    private static class NativeMethods
    {
        public const byte VirtualKeyEnter = 0x0D;
        public const byte VirtualKeyEscape = 0x1B;
        public const byte VirtualKeyControl = 0x11;
        public const byte VirtualKeyA = 0x41;
        public const byte VirtualKeyLeftWindows = 0x5B;
        public const byte VirtualKeyShift = 0x10;
        public const byte VirtualKeySpace = 0x20;

        private const uint KeyEventKeyUp = 0x0002;
        private const uint InputKeyboard = 1;
        private const uint KeyEventUnicode = 0x0004;
        private const uint MouseEventLeftDown = 0x0002;
        private const uint MouseEventLeftUp = 0x0004;

        public static void PressKey(byte key)
        {
            KeybdEvent(key, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            KeybdEvent(key, 0, KeyEventKeyUp, UIntPtr.Zero);
        }

        public static void PressHotkey(params byte[] keys)
        {
            foreach (byte key in keys)
            {
                KeybdEvent(key, 0, 0, UIntPtr.Zero);
                Thread.Sleep(50);
            }

            foreach (byte key in keys.Reverse())
            {
                KeybdEvent(key, 0, KeyEventKeyUp, UIntPtr.Zero);
                Thread.Sleep(50);
            }
        }

        public static void TypeText(string text)
        {
            foreach (char character in text)
            {
                var down = Input.ForUnicode(character, keyUp: false);
                var up = Input.ForUnicode(character, keyUp: true);
                Input[] inputs = [down, up];
                _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
            }
        }

        public static void ClickCenter(System.Windows.Rect bounds)
        {
            int x = (int)(bounds.Left + (bounds.Width / 2));
            int y = (int)(bounds.Top + (bounds.Height / 2));
            _ = SetCursorPos(x, y);
            Thread.Sleep(50);
            MouseEvent(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
            MouseEvent(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
        }

        public static void ActivateWindow(int nativeWindowHandle)
        {
            if (nativeWindowHandle != 0)
            {
                _ = SetForegroundWindow((IntPtr)nativeWindowHandle);
                Thread.Sleep(250);
            }
        }

        [DllImport("user32.dll", EntryPoint = "keybd_event")]
        private static extern void KeybdEvent(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint inputCount, Input[] inputs, int size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", EntryPoint = "mouse_event")]
        private static extern void MouseEvent(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr windowHandle);

        [StructLayout(LayoutKind.Sequential)]
        private struct Input
        {
            public uint Type;
            public KeyboardInput Keyboard;

            public static Input ForUnicode(char character, bool keyUp) => new()
            {
                Type = InputKeyboard,
                Keyboard = new KeyboardInput
                {
                    VirtualKey = 0,
                    ScanCode = character,
                    Flags = KeyEventUnicode | (keyUp ? KeyEventKeyUp : 0),
                    Time = 0,
                    ExtraInfo = UIntPtr.Zero
                }
            };
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }
    }
}