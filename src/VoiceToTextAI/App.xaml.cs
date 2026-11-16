using System.Windows;
using System.Windows.Threading;

namespace VoiceToTextAI;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnUnhandled;
    }

    private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show("An unexpected error occurred:\n\n" + e.Exception.Message,
            "Voice to Text AI", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
