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

            // Ensure database, tables, and seed data exist
            AppDbContext.EnsureCreated();

            // Validate licence before showing login
            LicenceService.Validate();

            // Show login window
            var loginWindow = new LoginWindow();
            bool? result = loginWindow.ShowDialog();

            if (result != true || loginWindow.LoggedInUser == null)
            {
                // User cancelled or login failed – shut down
                Shutdown();
                return;
            }

            // Store logged-in user in application resources for global access
            var user = loginWindow.LoggedInUser;
            Resources["CurrentUser"] = user;

            // Launch main window
            var mainWindow = new MainWindow(user);
            MainWindow = mainWindow;
            mainWindow.Show();
        }
    }
}
