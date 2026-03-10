// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using G33kSeek.Models;
using G33kSeek.Services;
using G33kSeek.ViewModels;
using JetBrains.Annotations;

namespace G33kSeek.Views;

public partial class MainWindow : Window
{
    private const int DefaultVisibleLauncherHeight = 220;

    private readonly MainWindowViewModel m_viewModel;

    [UsedImplicitly]
    public MainWindow()
        : this(new MainWindowViewModel(new QueryEngine([])))
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        m_viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = m_viewModel;
        Opened += OnOpened;
        Deactivated += OnDeactivated;
        AddHandler(KeyDownEvent, OnPreviewKeyDownAsync, RoutingStrategies.Tunnel);
    }

    public void PrepareForActivation()
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = GetLauncherPosition();
        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
        SearchTextBox.CaretIndex = SearchTextBox.Text?.Length ?? 0;
    }

    private void OnOpened(object sender, EventArgs e) =>
        PrepareForActivation();

    private void OnDeactivated(object sender, EventArgs e) =>
        Hide();

    private async void OnPreviewKeyDownAsync(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Hide();
                e.Handled = true;
                break;

            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;

            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;

            case Key.Enter:
                await ExecuteSelectedResultAsync();

                e.Handled = true;
                break;
        }
    }

    private async void OnResultsListBoxDoubleTappedAsync(object sender, TappedEventArgs e)
    {
        var result = TryGetResultFromSource(e.Source) ?? ResultsListBox.SelectedItem as QueryResult;
        if (result == null)
            return;

        m_viewModel.SelectedResult = result;
        await ExecuteSelectedResultAsync();
        e.Handled = true;
    }

    private async Task ExecuteSelectedResultAsync()
    {
        if (m_viewModel.SelectedResult == null)
            return;

        var executionResult = await QueryExecutionService.ExecuteAsync(m_viewModel.SelectedResult, this);
        if (!string.IsNullOrWhiteSpace(executionResult.StatusText))
            m_viewModel.StatusText = executionResult.StatusText;

        if (executionResult.ShouldHideLauncher)
            Hide();
    }

    private PixelPoint GetLauncherPosition()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        var workingArea = screen?.WorkingArea ?? new PixelRect(0, 0, 1440, 900);
        var windowWidth = Bounds.Width > 0 ? (int)Bounds.Width : (int)Width;
        var x = workingArea.X + Math.Max(0, (workingArea.Width - windowWidth) / 2);
        var y = workingArea.Y + Math.Max(0, (workingArea.Height / 4) - (DefaultVisibleLauncherHeight / 2));
        return new PixelPoint(x, y);
    }

    private void MoveSelection(int offset)
    {
        if (ResultsListBox.ItemCount <= 0)
            return;

        var selectedIndex = GetNextSelectedIndex(ResultsListBox.SelectedIndex, ResultsListBox.ItemCount, offset);
        if (selectedIndex < 0)
            return;

        ResultsListBox.SelectedIndex = selectedIndex;
        ResultsListBox.ScrollIntoView(selectedIndex);
    }

    internal static int GetNextSelectedIndex(int currentIndex, int itemCount, int offset)
    {
        if (itemCount <= 0 || offset == 0)
            return -1;

        if (currentIndex < 0)
            return offset > 0 ? 0 : itemCount - 1;

        return Math.Clamp(currentIndex + offset, 0, itemCount - 1);
    }

    internal static QueryResult TryGetResultFromSource(object source)
    {
        for (var current = source as StyledElement; current != null; current = current.Parent)
        {
            if (current.DataContext is QueryResult result)
                return result;
        }

        return null;
    }
}
