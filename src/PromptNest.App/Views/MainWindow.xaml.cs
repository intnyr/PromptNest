using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using PromptNest.App.ViewModels;
using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.App.Views;

public sealed partial class MainWindow : Window
{
    private readonly DispatcherTimer searchDebounceTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public MainWindow(MainViewModel viewModel, ITrayService trayService)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(trayService);
        ViewModel = viewModel;
        InitializeComponent();
        ShellRoot.DataContext = ViewModel;
        TrayCommandList.ItemsSource = trayService.MenuCommands;
        Title = "PromptNest";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SystemBackdrop = new MicaBackdrop();
        SizeChanged += OnSizeChanged;
        searchDebounceTimer.Tick += OnSearchDebounceTimerTick;
    }

    public MainViewModel ViewModel { get; }

    public void ShowSettingsPane()
    {
        SettingsPane.Visibility = Visibility.Visible;
        Activate();
    }

    private void OnSizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        bool compact = args.Size.Width < 1040;
        EditorPane.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        EditorColumn.Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        EditorColumn.MinWidth = compact ? 0 : 420;
    }

    private void OnNavigationClick(object sender, RoutedEventArgs args)
    {
        if (TryGetTag(sender, out string id))
        {
            ViewModel.SelectNavigationCommand.Execute(id);
        }
    }

    private void OnFolderClick(object sender, RoutedEventArgs args)
    {
        if (TryGetTag(sender, out string id))
        {
            ViewModel.SelectFolderCommand.Execute(id);
        }
    }

    private void OnFolderToggleClick(object sender, RoutedEventArgs args)
    {
        if (TryGetTag(sender, out string id))
        {
            ViewModel.ToggleFolderCommand.Execute(id);
        }
    }

    private void OnCollectionClick(object sender, RoutedEventArgs args)
    {
        if (TryGetTag(sender, out string id))
        {
            ViewModel.SelectCollectionCommand.Execute(id);
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (sender is not TextBox)
        {
            return;
        }

        searchDebounceTimer.Stop();
        searchDebounceTimer.Start();
    }

    private void OnSearchDebounceTimerTick(object? sender, object args)
    {
        ArgumentNullException.ThrowIfNull(args);
        searchDebounceTimer.Stop();
        ViewModel.UpdateSearchCommand.Execute(SearchBox.Text);
    }

    private void OnClearSearchClick(object sender, RoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(args);
        searchDebounceTimer.Stop();
        SearchBox.Text = string.Empty;
        ViewModel.ClearSearchCommand.Execute(null);
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void OnFocusSearchClick(object sender, RoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(args);
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void OnSortClick(object sender, RoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(args);
        IReadOnlyList<string> options = ViewModel.Library.Toolbar.SortOptions;
        int currentIndex = options.ToList().IndexOf(ViewModel.Library.SortLabel);
        string nextSort = options[(currentIndex + 1) % options.Count];

        ViewModel.ChangeSortCommand.Execute(nextSort);
        SortButton.Content = $"Sort: {ViewModel.Library.SortLabel}";
    }

    private void OnPromptItemClick(object sender, ItemClickEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (args.ClickedItem is PromptListItemViewModel prompt)
        {
            ViewModel.SelectPromptCommand.Execute(prompt.Id);
            UpdateEditorStatus();
        }
    }

    private void OnFavoriteClick(object sender, RoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (TryGetTag(sender, out string id))
        {
            ViewModel.TogglePromptFavoriteCommand.Execute(id);
        }
    }

    private void OnPreviousPageClick(object sender, RoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(args);
        ViewModel.GoToPreviousPageCommand.Execute(null);
    }

    private void OnNextPageClick(object sender, RoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(args);
        ViewModel.GoToNextPageCommand.Execute(null);
    }

    private void OnSettingsClick(object sender, RoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(args);
        SettingsPane.Visibility = Visibility.Visible;
    }

    private void OnCloseSettingsClick(object sender, RoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(args);
        SettingsPane.Visibility = Visibility.Collapsed;
    }

    private async void OnCheckForUpdatesClick(object sender, RoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(args);
        await ViewModel.CheckForUpdatesAsync(CancellationToken.None);
    }

    private void OnEditorFieldChanged(object sender, TextChangedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(args);
        ViewModel.MarkEditorDirtyCommand.Execute(null);
    }

    private void OnTitleTextChanged(object sender, TextChangedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (sender is TextBox textBox)
        {
            ViewModel.UpdatePromptTitleCommand.Execute(textBox.Text);
        }
    }

    private void OnPromptBodyTextChanged(object sender, TextChangedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (sender is TextBox textBox)
        {
            ViewModel.UpdatePromptBodyCommand.Execute(textBox.Text);
        }

        UpdateEditorStatus();
    }

    private void OnPromptBodySelectionChanged(object sender, RoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(args);
        UpdateEditorStatus();
    }

    private void OnSaveEditorClick(object sender, RoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(args);
        ViewModel.SaveEditorCommand.Execute(null);
    }

    private void OnCancelEditorClick(object sender, RoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(args);
        ViewModel.CancelEditorCommand.Execute(null);
    }

    private async void OnCopySelectedPromptClick(object sender, RoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(args);

        OperationResult<PromptCopyForm> formResult = await ViewModel.PrepareCopySelectedPromptAsync(CancellationToken.None);
        if (!formResult.Succeeded || formResult.Value is null)
        {
            return;
        }

        IReadOnlyDictionary<string, string>? values = await ShowVariableFillDialogAsync(formResult.Value);
        if (values is null)
        {
            return;
        }

        await ViewModel.CopySelectedPromptAsync(values, CancellationToken.None);
    }

    private void OnTagInputTextChanged(object sender, TextChangedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (sender is TextBox textBox)
        {
            ViewModel.UpdateTagInputCommand.Execute(textBox.Text);
        }
    }

    private void OnTagInputKeyDown(object sender, KeyRoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (args.Key == Windows.System.VirtualKey.Enter)
        {
            IReadOnlyList<string> suggestions = ViewModel.Library.FilteredTagSuggestions;
            string tagName = suggestions.Count == 0 ? textBox.Text : suggestions[0];
            AddTag(tagName);
            args.Handled = true;
            return;
        }

        if (args.Key == Windows.System.VirtualKey.Back && string.IsNullOrWhiteSpace(textBox.Text))
        {
            IReadOnlyList<TagChipViewModel>? tags = ViewModel.Library.SelectedPrompt?.Tags;
            string? lastTag = tags is { Count: > 0 } ? tags[^1].Name : null;
            if (lastTag is not null)
            {
                ViewModel.RemoveTagFromSelectedPromptCommand.Execute(lastTag);
                args.Handled = true;
            }
        }
    }

    private void OnTagSuggestionClick(object sender, RoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (TryGetTag(sender, out string tagName))
        {
            AddTag(tagName);
        }
    }

    private void OnRemoveTagClick(object sender, RoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (TryGetTag(sender, out string tagName))
        {
            ViewModel.RemoveTagFromSelectedPromptCommand.Execute(tagName);
        }
    }

    private void OnShellRootKeyDown(object sender, KeyRoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (!IsControlKeyDown())
        {
            if (args.Key == Windows.System.VirtualKey.F2)
            {
                TitleBox.Focus(FocusState.Programmatic);
                args.Handled = true;
            }

            if (args.Key == Windows.System.VirtualKey.Delete)
            {
                ViewModel.DeleteSelectedPromptCommand.Execute(null);
                args.Handled = true;
            }

            return;
        }

        switch (args.Key)
        {
            case Windows.System.VirtualKey.K:
            case Windows.System.VirtualKey.F:
                SearchBox.Focus(FocusState.Programmatic);
                args.Handled = true;
                break;
            case Windows.System.VirtualKey.N:
                ViewModel.InvokeToolbarCommandCommand.Execute(LibraryToolbarCommandKind.NewPrompt);
                args.Handled = true;
                break;
            case Windows.System.VirtualKey.D:
                ViewModel.DuplicateSelectedPromptCommand.Execute(null);
                args.Handled = true;
                break;
            case Windows.System.VirtualKey.E:
                PromptBodyEditor.Focus(FocusState.Programmatic);
                args.Handled = true;
                break;
            case (Windows.System.VirtualKey)188:
                SettingsPane.Visibility = Visibility.Visible;
                args.Handled = true;
                break;
        }
    }

    private void OnNewPromptClick(object sender, RoutedEventArgs args)
    {
        InvokeToolbarCommand(sender, args, LibraryToolbarCommandKind.NewPrompt);
    }

    private void OnDuplicateClick(object sender, RoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(args);
        ViewModel.DuplicateSelectedPromptCommand.Execute(null);
    }

    private void OnNewFolderClick(object sender, RoutedEventArgs args)
    {
        InvokeToolbarCommand(sender, args, LibraryToolbarCommandKind.NewFolder);
    }

    private void OnManageTagsClick(object sender, RoutedEventArgs args)
    {
        InvokeToolbarCommand(sender, args, LibraryToolbarCommandKind.ManageTags);
    }

    private void OnFilterClick(object sender, RoutedEventArgs args)
    {
        InvokeToolbarCommand(sender, args, LibraryToolbarCommandKind.ToggleFilters);
    }

    private void OnOverflowClick(object sender, RoutedEventArgs args)
    {
        InvokeToolbarCommand(sender, args, LibraryToolbarCommandKind.OpenOverflow);
    }

    private void OnSearchBoxKeyDown(object sender, KeyRoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (args.Key != Windows.System.VirtualKey.Escape)
        {
            return;
        }

        SearchBox.Text = string.Empty;
        args.Handled = true;
    }

    private void InvokeToolbarCommand(object sender, RoutedEventArgs args, LibraryToolbarCommandKind command)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(args);
        ViewModel.InvokeToolbarCommandCommand.Execute(command);
    }

    private static bool TryGetTag(object sender, out string id)
    {
        id = string.Empty;
        if (sender is not FrameworkElement { Tag: string tag } || string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        id = tag;
        return true;
    }

    private void UpdateEditorStatus()
    {
        if (PromptBodyEditor is null || EditorPositionText is null || EditorCharacterCountText is null || EditorLineNumbers is null)
        {
            return;
        }

        string text = PromptBodyEditor.Text ?? string.Empty;
        int selectionStart = Math.Clamp(PromptBodyEditor.SelectionStart, 0, text.Length);
        int line = 1;
        int column = 1;

        for (int index = 0; index < selectionStart; index++)
        {
            if (text[index] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        EditorPositionText.Text = $"Ln {line}, Col {column}";
        EditorCharacterCountText.Text = $"{text.Length} characters";
        EditorLineNumbers.Text = string.Join(Environment.NewLine, Enumerable.Range(1, Math.Max(1, text.Split('\n').Length)));
    }

    private void AddTag(string tagName)
    {
        ViewModel.AddTagToSelectedPromptCommand.Execute(tagName);
        TagInput.Text = string.Empty;
        TagInput.Focus(FocusState.Programmatic);
    }

    private async Task<IReadOnlyDictionary<string, string>?> ShowVariableFillDialogAsync(PromptCopyForm form)
    {
        if (!form.RequiresInput)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var fields = new Dictionary<string, TextBox>(StringComparer.OrdinalIgnoreCase);
        var panel = new StackPanel { Spacing = 10 };
        foreach (PromptCopyVariable variable in form.Variables)
        {
            var box = new TextBox
            {
                Header = variable.IsRequired ? $"{variable.Name} *" : variable.Name,
                Text = variable.CurrentValue ?? string.Empty,
                PlaceholderText = variable.DefaultValue ?? variable.Name,
                Style = (Style)Application.Current.Resources["PromptNestSearchBoxStyle"]
            };
            fields[variable.Name] = box;
            panel.Children.Add(box);
        }

        var dialog = new ContentDialog
        {
            XamlRoot = ShellRoot.XamlRoot,
            Title = $"Copy {form.Title}",
            Content = panel,
            PrimaryButtonText = "Copy",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        while (true)
        {
            ContentDialogResult result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return null;
            }

            Dictionary<string, string> values = fields.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Text,
                StringComparer.OrdinalIgnoreCase);

            PromptCopyVariable? missing = form.Variables.FirstOrDefault(variable =>
                variable.IsRequired && string.IsNullOrWhiteSpace(values.GetValueOrDefault(variable.Name)));
            if (missing is null)
            {
                return values;
            }

            fields[missing.Name].Focus(FocusState.Programmatic);
        }
    }

    private static bool IsControlKeyDown()
    {
        Windows.UI.Core.CoreVirtualKeyStates leftState =
            Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        return leftState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
    }
}