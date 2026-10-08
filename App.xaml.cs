using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace QRGen;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        DispatcherUnhandledException += OnUnhandled;
        if (e.Args.Length >= 2 && e.Args[0] == "--selftest")
        {
            int fails = Core.SelfTest.Run(e.Args[1]);
            Shutdown(fails);
            return;
        }
        base.OnStartup(e);
        var win = new MainWindow();
        win.Show();
        if (e.Args.Length >= 2 && e.Args[0] == "--shot")
            Core.SelfTest.Screenshot(win, e.Args[1], e.Args.Length >= 3 ? e.Args[2] : null, e.Args.Length >= 4 ? int.Parse(e.Args[3]) : 0);
    }

    void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "QRGen - Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}

