using GreenLumaPresets.Services;
using System.Windows;

namespace GreenLumaPresets.Views;

public partial class DialogWindow : Window
{
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

    public DialogWindow(string title, string message, DialogType dialogType)
    {
        InitializeComponent();
        this.Title = title;
        TitleBlock.Text = title;
        MessageBlock.Text = message;

        switch (dialogType)
        {
            case DialogType.Information:
                ConfigureInformationDialog();
                break;
            case DialogType.Warning:
                ConfigureWarningDialog();
                break;
            case DialogType.Error:
                ConfigureErrorDialog();
                break;
            case DialogType.Question:
                ConfigureQuestionDialog();
                break;
        }
    }

    private void ConfigureInformationDialog()
    {
        Button1.Content = "OK";
        Button1.Click -= Button1_Click;
        Button1.Click += (s, e) => { Result = MessageBoxResult.OK; Close(); };
    }

    private void ConfigureWarningDialog()
    {
        Button1.Content = "OK";
        Button1.Click -= Button1_Click;
        Button1.Click += (s, e) => { Result = MessageBoxResult.OK; Close(); };
    }

    private void ConfigureErrorDialog()
    {
        Button1.Content = "OK";
        Button1.Click -= Button1_Click;
        Button1.Click += (s, e) => { Result = MessageBoxResult.OK; Close(); };
    }

    private void ConfigureQuestionDialog()
    {
        Button1.Content = "Yes";
        Button1.Click -= Button1_Click;
        Button1.Click += (s, e) => { Result = MessageBoxResult.Yes; Close(); };

        Button2.Content = "No";
        Button2.Visibility = Visibility.Visible;
        Button2.Click -= Button2_Click;
        Button2.Click += (s, e) => { Result = MessageBoxResult.No; Close(); };
    }

    private void Button1_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.OK;
        Close();
    }

    private void Button2_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.Cancel;
        Close();
    }
}
