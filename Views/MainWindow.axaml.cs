using System;
using Avalonia.Controls;
using Avalonia.Input;
using G33kSeek.ViewModels;

namespace G33kSeek.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel m_viewModel;

    public MainWindow()
        : this(new MainWindowViewModel())
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        m_viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = m_viewModel;
        Opened += MainWindow_OnOpened;
        Deactivated += MainWindow_OnDeactivated;
        KeyDown += MainWindow_OnKeyDown;
    }

    public void PrepareForActivation()
    {
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
        SearchTextBox.CaretIndex = SearchTextBox.Text?.Length ?? 0;
    }

    private void MainWindow_OnOpened(object sender, EventArgs e) =>
        PrepareForActivation();

    private void MainWindow_OnDeactivated(object sender, EventArgs e) =>
        Hide();

    private void MainWindow_OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Hide();
                e.Handled = true;
                break;

            case Key.Down:
                if (ResultsListBox.ItemCount > 0)
                {
                    ResultsListBox.Focus();
                    ResultsListBox.SelectedIndex = Math.Max(ResultsListBox.SelectedIndex, 0);
                    e.Handled = true;
                }

                break;
        }
    }
}
