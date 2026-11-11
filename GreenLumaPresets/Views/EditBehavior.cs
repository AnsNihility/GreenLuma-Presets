using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace GreenLumaPresets.Views;

/// <summary>
/// Focuses a TextBox and selects its text when its item enters edit mode.
/// </summary>
public static class EditBehavior
{
    public static readonly DependencyProperty IsEditingProperty = DependencyProperty.RegisterAttached(
        "IsEditing",
        typeof(bool),
        typeof(EditBehavior),
        new PropertyMetadata(false, OnIsEditingChanged));

    public static bool GetIsEditing(DependencyObject element) => (bool)element.GetValue(IsEditingProperty);

    public static void SetIsEditing(DependencyObject element, bool value) => element.SetValue(IsEditingProperty, value);

    private static void OnIsEditingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox || e.NewValue is not true) return;

        // Deferred: the style trigger making the TextBox focusable is applied after this callback.
        textBox.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            textBox.Focus();
            textBox.SelectAll();
        });
    }
}
