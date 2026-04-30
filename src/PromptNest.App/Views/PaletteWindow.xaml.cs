using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using PromptNest.App.ViewModels;
using PromptNest.Core.Models;

namespace PromptNest.App.Views;

public sealed partial class PaletteWindow : Window
{
    private readonly DispatcherTimer searchTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };

    public PaletteWindow(PaletteViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ((FrameworkElement)Content).KeyDown += OnPaletteKeyDown;
        ((FrameworkElement)Content).DataContext = ViewModel;
        SystemBackdrop = new DesktopAcrylicBackdrop();
        searchTimer.Tick += OnSearchTimerTick;
    }

    public PaletteViewModel ViewModel { get; }

    public void Toggle()
    {
        if (isVisible)
        {
            AppWindow.Hide();
            isVisible = false;
            return;
        }

        Activate();
        isVisible = true;
        PaletteSearchBox.Focus(FocusState.Programmatic);
        _ = ViewModel.SearchAsync(CancellationToken.None);
    }

    private bool isVisible;

    private void OnSearchTextChanged(object sender, TextChangedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (sender is TextBox textBox)
        {
            ViewModel.SearchText = textBox.Text;
            searchTimer.Stop();
            searchTimer.Start();
        }
    }

    private async void OnSearchTimerTick(object? sender, object args)
    {
        ArgumentNullException.ThrowIfNull(args);
        searchTimer.Stop();
        await ViewModel.SearchAsync(CancellationToken.None);
    }

    private async void OnResultClick(object sender, ItemClickEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (args.ClickedItem is PalettePromptResultViewModel result)
        {
            ViewModel.SelectedResult = result;
            await CopySelectedAsync();
        }
    }

    private async void OnPaletteKeyDown(object sender, KeyRoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (args.Key == Windows.System.VirtualKey.Escape)
        {
            AppWindow.Hide();
            isVisible = false;
            args.Handled = true;
            return;
        }

        if (args.Key == Windows.System.VirtualKey.Down)
        {
            ViewModel.SelectNextCommand.Execute(null);
            args.Handled = true;
            return;
        }

        if (args.Key == Windows.System.VirtualKey.Up)
        {
            ViewModel.SelectPreviousCommand.Execute(null);
            args.Handled = true;
            return;
        }

        if (args.Key == Windows.System.VirtualKey.Enter)
        {
            await CopySelectedAsync();
            args.Handled = true;
        }
    }

    private async Task CopySelectedAsync()
    {
        OperationResult<PromptCopyForm> formResult = await ViewModel.PrepareSelectedCopyAsync(CancellationToken.None);
        if (!formResult.Succeeded || formResult.Value is null)
        {
            return;
        }

        IReadOnlyDictionary<string, string>? values = await ShowVariableFillDialogAsync(formResult.Value);
        if (values is not null)
        {
            await ViewModel.CopySelectedWithValuesAsync(values, CancellationToken.None);
        }
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
            XamlRoot = ((FrameworkElement)Content).XamlRoot,
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
}