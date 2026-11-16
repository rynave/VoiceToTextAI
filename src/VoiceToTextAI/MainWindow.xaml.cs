using System.Windows;
using VoiceToTextAI.ViewModels;

namespace VoiceToTextAI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        LogBox.TextChanged += (_, _) => LogBox.ScrollToEnd();

        // PasswordBox.Password can't be data-bound, so keep the view-model in sync here.
        ApiKeyBox.PasswordChanged += (_, _) => _vm.ApiKey = ApiKeyBox.Password;
        ApiKeyText.TextChanged += (_, _) => _vm.ApiKey = ApiKeyText.Text;

        ShowKeyToggle.Checked += (_, _) =>
        {
            ApiKeyText.Text = ApiKeyBox.Password;
            ApiKeyText.Visibility = Visibility.Visible;
            ApiKeyBox.Visibility = Visibility.Collapsed;
            ShowKeyToggle.Content = "Hide";
        };
        ShowKeyToggle.Unchecked += (_, _) =>
        {
            ApiKeyBox.Password = ApiKeyText.Text;
            ApiKeyBox.Visibility = Visibility.Visible;
            ApiKeyText.Visibility = Visibility.Collapsed;
            ShowKeyToggle.Content = "Show";
        };
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            _vm.SetFile(files[0]);
    }
}
