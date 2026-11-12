using GreenLumaPresets.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GreenLumaPresets.Views;

/// <summary>
/// Only view concerns live here: window chrome, menus and the TextBox side of inline editing.
/// Everything else is in <see cref="MainViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        ImportContextMenu.PlacementTarget = sender as Button;
        ImportContextMenu.IsOpen = true;
    }

    private void TextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox) CommitEdit(textBox);
    }

    private void TextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox) return;

        if (e.Key == Key.Enter)
        {
            CommitEdit(textBox);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && textBox.DataContext is IEditable { IsEditing: true } editable)
        {
            // Discard the typed text: the binding only pushes on LostFocus, so restore it from the source.
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            viewModel.CancelEditCommand.Execute(editable);
            e.Handled = true;
        }
    }

    private void CommitEdit(TextBox textBox)
    {
        if (textBox.DataContext is not IEditable { IsEditing: true } editable) return;

        // The binding only pushes on LostFocus: push the typed text before saving it.
        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        viewModel.CommitEditCommand.Execute(editable);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximized();
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void TitleBar_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var screenPoint = PointToScreen(e.GetPosition(this));
        SystemCommands.ShowSystemMenu(this, screenPoint);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximized();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximized()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }
}
