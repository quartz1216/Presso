using System;
using System.Windows;

namespace Presso;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global handlers — keep the user from seeing a bare crash dialog
        AppDomain.CurrentDomain.UnhandledException += (_, ev) =>
        {
            MessageBox.Show($"予期しないエラー:\n{ev.ExceptionObject}", "Presso",
                MessageBoxButton.OK, MessageBoxImage.Error);
        };
        DispatcherUnhandledException += (_, ev) =>
        {
            MessageBox.Show($"予期しないエラー:\n{ev.Exception}", "Presso",
                MessageBoxButton.OK, MessageBoxImage.Error);
            ev.Handled = true;
        };

        if (e.Args.Length >= 2 && string.Equals(e.Args[0], "--shell", StringComparison.OrdinalIgnoreCase))
        {
            var shell = new ShellWindow(e.Args[1]);
            shell.Show();
        }
        else
        {
            var main = new MainWindow();
            main.Show();
        }
    }
}
