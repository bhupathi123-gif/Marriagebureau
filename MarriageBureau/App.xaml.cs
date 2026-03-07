using System.Windows;
using MarriageBureau.Data;

namespace MarriageBureau
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Ensure database is created and all tables exist (including new BiodataPhotos)
            AppDbContext.EnsureCreated();
        }
    }
}
