# Prompt Library — Implementation Plan

**Stack:** WinUI 3 · .NET 8 · C# · SQLite · MVVM
**Target:** Windows 10 1809+ (Windows 11 recommended)
**Distribution:** GitHub Releases (free) — self-signed MSIX + portable ZIP
**Constraints:** 100% free tooling. Fully AI-buildable end-to-end. Zero human approval steps.

---

## 1. Assumptions

- Single-user, local-first desktop app. No cloud sync in v1.
- Windows-only. No cross-platform requirement.
- All work performed by AI agent. No human coding, review, signing, submission, or approval.
- Toolchain entirely free / OSS. No paid accounts, certs, stores, or services.
- Build environment: Visual Studio 2022 **Community Edition** (free) OR `dotnet` SDK CLI only (preferred for AI automation — no IDE needed).
- English-only UI in v1; localization scaffolded but not populated.
- Distribution: GitHub Releases via GitHub Actions on a public repo (free unlimited minutes for public repos).
- Code signing: **self-signed cert** generated in CI (free). Users see SmartScreen warning on first run; documented in README. No paid EV cert.
- App is MSIX-packaged for clean install/uninstall **and** shipped as portable ZIP for users who skip MSIX trust step.
- Max prompt body size: 64KB.
- Soft cap: 100k prompts before optimization needed.
- No Microsoft Store submission (requires $19 dev account + human review). Direct download only.

---

## 2. High-Level Architecture

### 2.1 Layered Design

```
┌────────────────────────────────────────────────┐
│  Presentation (WinUI 3 Views + ViewModels)     │
│   - XAML pages, controls, palette window       │
│   - MVVM via CommunityToolkit.Mvvm             │
├────────────────────────────────────────────────┤
│  Application Services                          │
│   - PromptService, FolderService, TagService   │
│   - SearchService (FTS5 wrapper)               │
│   - ClipboardService, HotkeyService            │
│   - VariableResolver                           │
├────────────────────────────────────────────────┤
│  Domain Models                                 │
│   - Prompt, Folder, Tag, Variable              │
├────────────────────────────────────────────────┤
│  Data Access                                   │
│   - SQLite + Dapper repositories               │
│   - Migrations (DbUp or hand-rolled)           │
├────────────────────────────────────────────────┤
│  Platform / Infrastructure                     │
│   - Win32 P/Invoke (RegisterHotKey)            │
│   - WinAppSDK APIs (Clipboard, Notifications)  │
│   - Logging (Serilog), DI (MS.Ext.DI)          │
└────────────────────────────────────────────────┘
```

### 2.2 Patterns

- **MVVM** for all views. ViewModels expose `ObservableObject` and `RelayCommand`.
- **Repository** pattern for data access. One repo per aggregate.
- **Dependency Injection** via `Microsoft.Extensions.DependencyInjection`. Composition root in `App.xaml.cs`.
- **Mediator** (optional, MediatR) for cross-cutting commands (e.g., `CopyPromptCommand` triggered from palette and main window). Skip in v1 if overhead.
- **Async-first**: all I/O `async/await`. UI thread reserved for rendering.

---

## 3. Project Structure

Solution layout (single repo):

```
PromptLibrary.sln
├─ src/
│  ├─ PromptLibrary.App/              # WinUI 3 project (entry point)
│  │   ├─ App.xaml(.cs)
│  │   ├─ Views/
│  │   │   ├─ MainWindow.xaml
│  │   │   ├─ PalettePage.xaml
│  │   │   ├─ Pages/
│  │   │   │   ├─ LibraryPage.xaml
│  │   │   │   ├─ EditorPage.xaml
│  │   │   │   └─ SettingsPage.xaml
│  │   │   └─ Controls/
│  │   │       ├─ PromptListItem.xaml
│  │   │       ├─ TagChip.xaml
│  │   │       └─ FolderTree.xaml
│  │   ├─ ViewModels/
│  │   ├─ Converters/
│  │   ├─ Styles/
│  │   │   ├─ Colors.xaml
│  │   │   ├─ Typography.xaml
│  │   │   └─ Controls.xaml
│  │   ├─ Assets/
│  │   └─ Package.appxmanifest
│  │
│  ├─ PromptLibrary.Core/             # Domain + services (.NET 8 class lib)
│  │   ├─ Models/
│  │   ├─ Services/
│  │   ├─ Abstractions/               # interfaces
│  │   └─ Variables/                  # variable parser/resolver
│  │
│  ├─ PromptLibrary.Data/             # SQLite repos + migrations
│  │   ├─ Repositories/
│  │   ├─ Migrations/
│  │   ├─ DbContext.cs                # SqliteConnection factory
│  │   └─ Schema.sql
│  │
│  └─ PromptLibrary.Platform/         # Win32 interop, hotkey, tray
│      ├─ GlobalHotkey.cs
│      ├─ TrayIcon.cs
│      └─ NativeMethods.cs
│
├─ tests/
│  ├─ PromptLibrary.Core.Tests/       # xUnit
│  ├─ PromptLibrary.Data.Tests/       # repo integration tests (in-memory SQLite)
│  └─ PromptLibrary.UiTests/          # WinAppDriver (optional)
│
├─ build/
│  ├─ msix/                           # packaging assets
│  └─ scripts/                        # build/sign/release scripts
│
└─ docs/
```

### 3.1 Key NuGet Packages

| Package | Purpose |
|---------|---------|
| `Microsoft.WindowsAppSDK` | WinUI 3 + WinAppSDK |
| `Microsoft.Windows.SDK.BuildTools` | SDK build support |
| `CommunityToolkit.Mvvm` | `ObservableObject`, `RelayCommand`, source generators |
| `CommunityToolkit.WinUI.Controls.*` | Extra controls (SettingsCard, etc.) |
| `Microsoft.Extensions.DependencyInjection` | DI |
| `Microsoft.Extensions.Hosting` | Generic host for services |
| `Microsoft.Data.Sqlite` | SQLite driver |
| `Dapper` | Micro-ORM |
| `H.NotifyIcon.WinUI` | Tray icon |
| `Serilog` + `Serilog.Sinks.File` | Logging |
| `FluentValidation` | Input validation |
| `Velopack` | Auto-update via GitHub Releases (free, OSS) |
| `xunit`, `FluentAssertions`, `Moq` | Testing |

---

## 4. Data Management

### 4.1 Storage Location

- **Packaged (MSIX):** `Windows.Storage.ApplicationData.Current.LocalFolder` → `%LOCALAPPDATA%\Packages\<PackageFamily>\LocalState\library.db`
- **Unpackaged:** `%LOCALAPPDATA%\PromptLibrary\library.db`
- Detect mode at startup; resolve via `IPathProvider` abstraction.

### 4.2 Schema (SQLite + FTS5)

```sql
PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;

CREATE TABLE folders (
  id          TEXT PRIMARY KEY,
  name        TEXT NOT NULL,
  parent_id   TEXT REFERENCES folders(id) ON DELETE CASCADE,
  sort_order  INTEGER NOT NULL DEFAULT 0,
  created_at  INTEGER NOT NULL
);

CREATE TABLE prompts (
  id            TEXT PRIMARY KEY,
  title         TEXT NOT NULL,
  body          TEXT NOT NULL,
  folder_id     TEXT REFERENCES folders(id) ON DELETE SET NULL,
  is_favorite   INTEGER NOT NULL DEFAULT 0,
  use_count     INTEGER NOT NULL DEFAULT 0,
  last_used_at  INTEGER,
  created_at    INTEGER NOT NULL,
  updated_at    INTEGER NOT NULL,
  deleted_at    INTEGER  -- soft delete
);

CREATE INDEX idx_prompts_folder      ON prompts(folder_id);
CREATE INDEX idx_prompts_favorite    ON prompts(is_favorite) WHERE is_favorite = 1;
CREATE INDEX idx_prompts_updated     ON prompts(updated_at DESC);
CREATE INDEX idx_prompts_last_used   ON prompts(last_used_at DESC);

CREATE TABLE tags (
  name   TEXT PRIMARY KEY COLLATE NOCASE,
  color  TEXT,
  count  INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE prompt_tags (
  prompt_id  TEXT NOT NULL REFERENCES prompts(id) ON DELETE CASCADE,
  tag_name   TEXT NOT NULL REFERENCES tags(name)  ON DELETE CASCADE,
  PRIMARY KEY (prompt_id, tag_name)
);

CREATE VIRTUAL TABLE prompts_fts USING fts5(
  title, body, tags,
  content='prompts',
  content_rowid='rowid',
  tokenize='porter unicode61'
);

-- Triggers to keep FTS in sync
CREATE TRIGGER prompts_ai AFTER INSERT ON prompts BEGIN
  INSERT INTO prompts_fts(rowid, title, body, tags)
  VALUES (new.rowid, new.title, new.body, '');
END;

CREATE TRIGGER prompts_au AFTER UPDATE ON prompts BEGIN
  UPDATE prompts_fts SET title = new.title, body = new.body
  WHERE rowid = new.rowid;
END;

CREATE TRIGGER prompts_ad AFTER DELETE ON prompts BEGIN
  DELETE FROM prompts_fts WHERE rowid = old.rowid;
END;

CREATE TABLE schema_version (version INTEGER PRIMARY KEY);
INSERT INTO schema_version VALUES (1);
```

### 4.3 Migrations

- Hand-rolled migration runner: read `schema_version`, apply numbered SQL files from `Migrations/` folder in order.
- Each migration wrapped in `BEGIN IMMEDIATE` transaction.
- Backup DB file to `library.db.bak.<timestamp>` before any migration.

### 4.4 Repository Interfaces

```csharp
public interface IPromptRepository {
    Task<Prompt?> GetAsync(string id);
    Task<IReadOnlyList<Prompt>> ListAsync(PromptQuery query);
    Task<IReadOnlyList<Prompt>> SearchAsync(string text, PromptQuery query);
    Task<string> CreateAsync(Prompt prompt);
    Task UpdateAsync(Prompt prompt);
    Task SoftDeleteAsync(string id);
    Task IncrementUsageAsync(string id);
}
```

`PromptQuery` filters: `FolderId`, `Tags`, `IsFavorite`, `IncludeDeleted`, `SortBy`, `Skip`, `Take`.

### 4.5 Search

- All search via FTS5 `MATCH`. Build query with `prefix='2 3'` for incremental search.
- Sub-50ms target on 100k rows. Verify with seed data benchmark.
- Fallback to `LIKE` if FTS query parse fails (user typed special chars).

### 4.6 Backup & Export

- **Auto-backup**: nightly copy of `library.db` to `Backups/` folder, keep last 7.
- **Manual export**: JSON dump (all prompts + folders + tags) via `File → Export`.
- **Import**: JSON merge with conflict resolution (skip / overwrite / duplicate).

---

## 5. UI Approach

### 5.1 Visual Language

- **Backdrop:** Mica on main window (Win 11), fallback solid on Win 10.
- **Palette window:** DesktopAcrylic, frameless, rounded corners, 600x420px centered.
- **Theme:** auto-follow system (`ElementTheme.Default`). Manual override in settings.
- **Color tokens:** Linear-inspired neutral grays + single accent (Indigo `#6366F1`). Defined in `Styles/Colors.xaml` as `ResourceDictionary` with light/dark variants.
- **Typography:** Segoe UI Variable (system). Mono for prompt body: `Cascadia Code` (Win 11 default) → fallback `Consolas`.
- **Spacing:** 4/8/12/16/24/32 scale. List rows ~36px tall.
- **Motion:** WinUI implicit transitions, `EntranceThemeTransition` on items. 150ms ease-out.

### 5.2 Window Layout

**Main Window** — three-pane, custom title bar:

```
┌─ Custom title bar (drag region) ──────────────────── _ □ × ┐
├──────────────┬─────────────────────────┬───────────────────┤
│ NavigationView │ Prompt list           │ Detail view       │
│  Search box    │  - Sort dropdown      │  - Title (large)  │
│  All / Fav /   │  - Active filter chips│  - Tag chips      │
│   Recent       │  - Virtualized list   │  - Body (mono)    │
│  Folders tree  │                       │  - Variables      │
│  Tag list      │                       │  - Usage meta     │
│                │                       │  - Copy / Edit    │
└──────────────┴─────────────────────────┴───────────────────┘
```

- `NavigationView` (left) in `LeftCompact` mode, expandable.
- `ListView` (center) virtualized. Custom `ItemTemplate` = `PromptListItem` control.
- Detail (right) hidden on narrow widths (<900px), opens as overlay.

**Palette Window** — frameless, AOT shown via global hotkey:

```
┌──────────────────────────────────────────┐
│  🔍  Search prompts...                   │
├──────────────────────────────────────────┤
│  ★ Email reply — formal           ⏎ Copy │
│    Code review checklist                 │
│    Refactor function — concise           │
│    ... (max 8 results)                   │
├──────────────────────────────────────────┤
│  ⏎ copy   ⌥⏎ edit   esc close            │
└──────────────────────────────────────────┘
```

### 5.3 Key Pages

| Page | Purpose |
|------|---------|
| `LibraryPage` | Default view. Three-pane. Hosts list + detail. |
| `EditorPage` | Create/edit prompt. Title, body (multiline), folder picker, tag autocomplete, variable preview. |
| `SettingsPage` | Theme, hotkeys, backup, import/export, about. Uses `SettingsCard`. |
| `PalettePage` | Hosted in palette window. Search + result list only. |

### 5.4 Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Win+Shift+Space` | Toggle palette (global) |
| `Ctrl+N` | New prompt |
| `Ctrl+F` | Focus search |
| `Ctrl+E` | Edit selected |
| `Ctrl+D` | Duplicate |
| `Ctrl+C` (in list) | Copy prompt body |
| `Ctrl+Shift+F` | Toggle favorite |
| `Delete` | Soft delete |
| `F2` | Rename inline |
| `Ctrl+,` | Settings |
| `Ctrl+K` | Quick palette (in-app) |
| `?` | Show shortcut sheet |

Implemented via `KeyboardAccelerator` on root content + global hook for `Win+Shift+Space`.

### 5.5 Accessibility

- All interactive elements have `AutomationProperties.Name`.
- Min contrast ratio 4.5:1 for body text.
- Full keyboard nav, no mouse-only paths.
- Respect Windows "Reduce motion" setting.
- High-contrast theme tested.

---

## 6. Variables / Placeholders

### 6.1 Syntax

- `{{name}}` — required variable.
- `{{name|default}}` — optional with default.
- Detected at save-time via regex `\{\{([a-zA-Z_][a-zA-Z0-9_]*)(?:\|([^}]*))?\}\}`.
- Cached in `prompts.variables_json` (denormalized) for fast palette use.

### 6.2 Fill Flow

1. User triggers Copy on prompt with variables.
2. If vars exist → modal opens with text inputs (one per var).
3. User fills → resolved text copied to clipboard + usage tracked.
4. If no vars → copy directly, no modal.
5. Last-used values per variable cached (per-prompt) for repeat use.

---

## 7. Platform Integration

### 7.1 Global Hotkey

P/Invoke to `user32.dll` `RegisterHotKey` / `UnregisterHotKey`. Hidden message-only window receives `WM_HOTKEY`. Encapsulated in `GlobalHotkey` class with `HotkeyPressed` event.

```csharp
[DllImport("user32.dll")]
static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
```

Configurable from settings; gracefully handle conflicts (return error, prompt user to choose another).

### 7.2 Tray Icon

`H.NotifyIcon.WinUI`. Menu items:
- Open Library
- New Prompt
- Recent (5)
- Favorites submenu
- Settings
- Quit

Single-click → toggle main window. Double-click → palette.

### 7.3 Single-Instance

Use `AppInstance.FindOrRegisterForKey` (WinAppSDK). On second launch, forward args (e.g. deep link) to primary instance and exit.

### 7.4 Clipboard

`Windows.ApplicationModel.DataTransfer.Clipboard.SetContent`. Set both `Text` and `Html` formats when prompt body is markdown (rendered HTML on side).

### 7.5 Notifications

Toast on copy: "Copied 'Title'". Use `AppNotificationBuilder`. Suppress if user disabled in settings.

### 7.6 Jump List

Pinned prompts appear on taskbar right-click. Updated whenever favorites change. `JumpList.SaveAsync`.

---

## 8. Application Bootstrap

`App.xaml.cs`:

```csharp
public partial class App : Application {
    public static IHost Host { get; private set; } = null!;

    public App() {
        InitializeComponent();
        Host = Microsoft.Extensions.Hosting.Host
            .CreateDefaultBuilder()
            .ConfigureServices((_, s) => {
                s.AddSingleton<IPathProvider, PathProvider>();
                s.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
                s.AddSingleton<IPromptRepository, PromptRepository>();
                s.AddSingleton<IFolderRepository, FolderRepository>();
                s.AddSingleton<ITagRepository, TagRepository>();
                s.AddSingleton<IPromptService, PromptService>();
                s.AddSingleton<ISearchService, SearchService>();
                s.AddSingleton<IClipboardService, ClipboardService>();
                s.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
                s.AddSingleton<ITrayService, TrayService>();
                s.AddSingleton<MainViewModel>();
                s.AddTransient<EditorViewModel>();
                s.AddTransient<PaletteViewModel>();
                s.AddSerilog();
            })
            .Build();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args) {
        await Host.StartAsync();
        await Host.Services.GetRequiredService<IMigrationRunner>().MigrateAsync();
        Host.Services.GetRequiredService<IGlobalHotkeyService>().Register();
        Host.Services.GetRequiredService<ITrayService>().Show();
        var window = new MainWindow();
        window.Activate();
    }
}
```

---

## 9. Logging & Diagnostics

- **Serilog** writing to `%LOCALAPPDATA%\PromptLibrary\logs\app-.log` with daily rolling, 7-day retention.
- Log levels: `Information` default, `Debug` in dev builds.
- No PII / prompt content in logs. Only IDs and operation names.
- Crash handler: `AppDomain.UnhandledException` + `TaskScheduler.UnobservedTaskException` → log + show toast → graceful shutdown.

---

## 10. Performance Targets

| Metric | Target |
|--------|--------|
| Cold start | <1s |
| Palette show (after hotkey) | <100ms |
| Search keystroke → results | <50ms (10k prompts) |
| Copy action | <30ms |
| RAM at idle | <120MB |
| Installer size | <40MB |

Strategies:
- Async-only DB calls.
- `ListView` virtualization + recycled containers.
- Debounce search input 80ms.
- Compile XAML (default in WinUI 3).
- AOT compile if feasible (test compatibility).

---

## 11. Testing Strategy

### 11.1 Unit Tests
- `PromptLibrary.Core.Tests`: services, variable parser, validators.
- xUnit + FluentAssertions + Moq.

### 11.2 Integration Tests
- `PromptLibrary.Data.Tests`: repos against in-memory SQLite (`Data Source=:memory:`).
- Schema migrations applied per test fixture.

### 11.3 UI Tests (optional v1)
- WinAppDriver for smoke tests on key flows: create, search, copy.
- Run on CI with self-hosted Windows runner.

### 11.4 Coverage Goal
- Core/services: 80%+
- Data: 70%+
- ViewModels: 60%+
- Views: smoke tests only.

---

## 12. Build & CI (Free, Fully Automated)

### 12.1 Local Build (CLI only — no IDE required)
- `dotnet restore`
- `dotnet build PromptLibrary.sln -c Release`
- `dotnet test --no-build -c Release`
- `dotnet publish src/PromptLibrary.App -c Release -r win-x64 --self-contained false -p:Platform=x64`

All steps invokable by AI agent via shell. No GUI tooling required.

### 12.2 GitHub Actions (Free)

Public GitHub repo → unlimited Actions minutes on `windows-latest` runner. Free.

`.github/workflows/ci.yml` — runs on every push/PR:
- Checkout
- Setup .NET 8 SDK (`actions/setup-dotnet`)
- `dotnet restore` (with NuGet cache via `actions/cache`)
- `dotnet build -c Release`
- `dotnet test -c Release --collect:"XPlat Code Coverage"`
- Upload coverage as artifact (Codecov free tier optional, or self-hosted report)

`.github/workflows/release.yml` — runs on tag `v*.*.*`:
- Build + test (gate)
- Generate self-signed cert in-runner via PowerShell `New-SelfSignedCertificate` (free, no external CA)
- Package MSIX with `MakeAppx.exe` (bundled in Windows SDK on runner — free)
- Sign MSIX with `SignTool.exe` using generated cert (free, Windows SDK)
- Build portable ZIP (publish output + cert `.cer` for manual trust)
- Run Velopack `vpk pack` → produces auto-update bundle (Velopack is OSS, free)
- Create GitHub Release via `softprops/action-gh-release` (free, OSS)
- Upload: `.msix`, `.cer`, portable `.zip`, Velopack bundle, `RELEASES` manifest

### 12.3 Versioning
- Auto-bump via Conventional Commits + `dotnet-gitversion` (free, OSS) — derives semver from commit messages.
- Tag created automatically by release workflow on `main` after passing CI. No human tagging.

### 12.4 No Human Steps
- No store submission.
- No paid cert purchase.
- No manual signing on dev machine.
- No manual GitHub release authoring (workflow handles it).
- AI agent triggers release by pushing a Conventional Commit + running `gh workflow run release.yml` (or by tag push).

---

## 13. Packaging & Distribution (Free)

### 13.1 MSIX (Self-Signed)
- `Package.appxmanifest` configured with identity, capabilities (`runFullTrust` for global hotkey), file associations.
- `Windows.Protocol` for `promptlib://` deep links.
- Packaged with `MakeAppx.exe` (free, Windows SDK).
- Signed with self-signed cert generated in CI. Cert `.cer` published alongside `.msix`.
- README documents one-time trust step: import `.cer` into `Trusted People` store, then double-click `.msix`. Single PowerShell one-liner provided for users.
- **No Microsoft Store.** No paid dev account. No human review process.

### 13.2 Portable ZIP
- `dotnet publish` output zipped. Run from any folder. No install.
- For users who don't want to trust the self-signed cert.

### 13.3 Velopack Auto-Update (Free, OSS)
- `vpk` CLI generates delta bundles + RELEASES manifest.
- Hosted on GitHub Releases (free).
- App checks for updates on launch + every 6h via Velopack runtime.
- Fully automated: AI tags release → CI publishes → users auto-update.

### 13.4 Channels
- `stable` — GitHub Releases `latest`.
- `beta` — GitHub Releases marked pre-release.
- `dev` — workflow artifact download.

All free. No external services beyond GitHub.

---

## 14. Security Considerations

- All data local; no network calls in v1 except update check (signed manifest).
- DB file readable only by current user (default NTFS ACL on `LocalAppData`).
- No telemetry by default. Opt-in only, anonymized, sent to own endpoint (post-v1).
- Validate all imported JSON: schema check, size cap, sanitize before insert.
- No code execution from prompt content. Body always treated as text.
- Update channel verifies signature before install.

---

## 15. Milestones

| Phase | Scope | Est. Duration |
|-------|-------|---------------|
| **M1: Foundation** | Solution scaffold, DI, DB, migrations, repos, basic CRUD service. Headless tests pass. | 1 week |
| **M2: Library UI** | MainWindow, three-pane, list+detail, theme, custom title bar. | 1 week |
| **M3: Editor + Tags + Folders** | EditorPage, tag autocomplete, folder tree, drag-drop. | 1 week |
| **M4: Search + FTS** | Search service, sidebar filters, sort options, keyboard shortcuts. | 4 days |
| **M5: Palette + Global Hotkey** | Palette window, AcrylicBackdrop, hotkey registration, tray icon. | 4 days |
| **M6: Variables + Copy Flow** | Variable parser, fill modal, clipboard service, usage tracking, toasts. | 3 days |
| **M7: Settings + Import/Export** | SettingsPage, JSON import/export, backup, hotkey customization. | 3 days |
| **M8: Polish** | Animations, empty states, accessibility audit, perf pass. | 4 days |
| **M9: Packaging + Release** | MSIX, signing, Velopack, CI, Store submission. | 1 week |

**Total estimate:** ~7 weeks for v1.0.

---

## 16. Out of Scope (v1)

- Cloud sync / multi-device
- Sharing / collaboration
- Versioning / history
- Browser extension
- Mobile companion
- AI-assisted prompt generation
- Markdown rendering preview (text-only in v1)
- Localization (English-only)

These move to v1.1+ roadmap.

---

## 17. Open Questions

All resolved for AI-driven, free-only build:

1. **Distribution:** MSIX (self-signed) + portable ZIP via GitHub Releases. No Store.
2. **Telemetry:** none. Avoids paid analytics services and human privacy review.
3. **Cloud sync (post-v1):** BYO file-vault (user points app at OneDrive/Dropbox/Git folder). Zero infra cost.
4. **Pricing:** free, OSS license (MIT). No payment processor needed.
5. **Signing:** self-signed in CI. Documented trust step. No paid cert.

---

## 18. Definition of Done (v1.0)

- [ ] All M1–M9 milestones complete.
- [ ] All keyboard shortcuts functional.
- [ ] Cold start <1s, palette <100ms verified via automated benchmark in CI.
- [ ] 80%+ Core test coverage (enforced by CI gate).
- [ ] Accessibility audit clean (`Accessibility Insights` CLI in CI — free, OSS).
- [ ] CI green on `main`.
- [ ] MSIX self-signed + portable ZIP + Velopack bundle published to GitHub Releases by automated workflow.
- [ ] README auto-generated with install/trust instructions.
- [ ] No human action required between commit and shipped release.

---

## 19. Free-Tools Inventory

Every tool, framework, and service used. All free / OSS.

| Category | Tool | License / Cost |
|----------|------|----------------|
| Language | C# / .NET 8 SDK | MIT, free |
| UI | WinUI 3 / WinAppSDK | MIT, free |
| IDE (optional) | VS 2022 Community / VS Code / Rider Community | Free |
| Build | `dotnet` CLI | Free |
| Packages | NuGet.org | Free |
| MVVM | CommunityToolkit.Mvvm | MIT |
| DB | SQLite + Microsoft.Data.Sqlite | Public domain / MIT |
| ORM | Dapper | Apache 2.0 |
| Tray | H.NotifyIcon.WinUI | MIT |
| Logging | Serilog | Apache 2.0 |
| Validation | FluentValidation | Apache 2.0 |
| Testing | xUnit, FluentAssertions, Moq | MIT / Apache |
| UI Test | WinAppDriver | MIT |
| Coverage | Coverlet | MIT |
| Versioning | GitVersion | MIT |
| MSIX tooling | MakeAppx, SignTool (Windows SDK) | Free |
| Cert | `New-SelfSignedCertificate` (PowerShell) | Free |
| Auto-update | Velopack | MIT |
| CI | GitHub Actions (public repo) | Free, unlimited |
| Hosting | GitHub Releases | Free |
| Source control | Git + GitHub | Free |
| Accessibility | Accessibility Insights CLI | MIT |
| Issue tracking | GitHub Issues | Free |
| Docs | Markdown in repo | Free |

**Zero paid services. Zero subscriptions. Zero certs purchased.**

---

## 20. AI-Driven Execution Plan

End-to-end flow performed by AI agent. No human in loop.

### 20.1 Capability Matrix

| Phase | AI does | Human does |
|-------|---------|------------|
| Repo bootstrap | `git init`, push to GitHub via `gh` CLI | none |
| Solution scaffold | `dotnet new`, write `.csproj`, `.sln` | none |
| Code generation | Write all `.cs` / `.xaml` / `.sql` | none |
| Dependency mgmt | `dotnet add package` | none |
| Test authoring | xUnit fixtures + assertions | none |
| Test execution | `dotnet test` locally + in CI | none |
| Build verification | `dotnet build` exit code check | none |
| UI smoke (headless) | WinAppDriver scripted | none |
| Performance bench | Custom BenchmarkDotNet harness in CI | none |
| Accessibility | Accessibility Insights CLI in CI | none |
| Packaging | MakeAppx + SignTool scripted | none |
| Release | GitHub Actions workflow + `gh release create` | none |
| Docs | Generate README from template + plan | none |

### 20.2 AI Workflow Loop

1. **Plan task** (read `IMPLEMENTATION_PLAN.md` milestone).
2. **Write code** for milestone scope.
3. **Write tests** alongside code.
4. **Run tests locally**: `dotnet test`. Fix failures, iterate.
5. **Commit** with Conventional Commit message (`feat:`, `fix:`, `chore:`).
6. **Push** → CI runs.
7. **Read CI logs** via `gh run view`. Fix any failures.
8. **On milestone complete:** push tag → release workflow ships artifacts.
9. **Verify release** via `gh release view`.
10. **Move to next milestone.**

### 20.3 Determinism Requirements

For AI to reliably build without human:
- **All inputs in repo**: no out-of-band config. Secrets only for cert (generated fresh each release, no stored secret).
- **Pinned versions**: `global.json` pins .NET SDK; `Directory.Packages.props` pins NuGet versions (Central Package Management).
- **Reproducible builds**: `Deterministic=true`, `ContinuousIntegrationBuild=true` in csproj.
- **No interactive prompts**: all CLI invocations use non-interactive flags (`--yes`, `-y`, `--non-interactive`).
- **Idempotent scripts**: re-running any script is safe.

### 20.4 Self-Verification Gates

CI must enforce (no human review):
- Build succeeds on `windows-latest`.
- All tests pass.
- Coverage ≥ thresholds (Core 80%, Data 70%).
- Lint clean: `dotnet format --verify-no-changes`.
- No new analyzer warnings (`TreatWarningsAsErrors=true`).
- Accessibility Insights CLI report has zero errors.
- Performance benchmarks within target envelope.

Failure → red CI → AI reads logs → fixes → retries. No human intervention.

### 20.5 Release Trigger

AI agent ships v1.0 by:
```bash
git tag v1.0.0
git push origin v1.0.0
```

Workflow handles: build → test → sign → package → publish → done.

End-users download from GitHub Releases page. URL stable, no auth required.
