using System.Windows;
using MarriageBureau.Data;
using MarriageBureau.Services;
using MarriageBureau.Views;

namespace MarriageBureau
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Prevent WPF from shutting down when LoginWindow closes
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Ensure database, tables, and seed data exist
            AppDbContext.EnsureCreated();

            // Validate licence once here (LoginWindow uses LicenceService.IsValid directly)
            LicenceService.Validate();

            // Show login window
            var loginWindow = new LoginWindow();
            bool? result = loginWindow.ShowDialog();

            if (result != true || loginWindow.LoggedInUser == null)
            {
                // User cancelled, closed window, or login failed — shut down
                Shutdown();
                return;
            }

            // Switch shutdown mode back to normal now that main window will own the app lifetime
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            var user = loginWindow.LoggedInUser;

            // Launch main window
            var mainWindow = new MainWindow(user);
            MainWindow = mainWindow;          
            mainWindow.Show();
        }
    }
}
