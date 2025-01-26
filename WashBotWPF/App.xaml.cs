using System.Windows;

namespace WashBotWPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Current.DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show(
                    $"Произошла непредвиденная ошибка:\n{args.Exception.Message}\n\n" +
                    "Пожалуйста, сообщите об этой ошибке разработчику.",
                    "Критическая ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                args.Handled = true;
            };
        }
    }
}