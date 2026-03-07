using System.Windows;
using MarriageBureau.Data;
using Microsoft.EntityFrameworkCore;

namespace MarriageBureau
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Ensure database is created and migrated
            using var context = new AppDbContext();
            context.Database.EnsureCreated();
        }
    }
}
