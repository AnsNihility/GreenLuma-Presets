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

    /// <summary>
    /// What the TextBox edits when it isn't its DataContext, e.g. the name of an AppID row.
    /// </summary>
    public static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached(
        "Target",
        typeof(object),
        typeof(EditBehavior),
        new PropertyMetadata(null));

    public static object? GetTarget(DependencyObject element) => element.GetValue(TargetProperty);

    public static void SetTarget(DependencyObject element, object? value) => element.SetValue(TargetProperty, value);

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
