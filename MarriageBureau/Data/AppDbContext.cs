using MarriageBureau.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace MarriageBureau.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Biodata> Biodatas { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MarriageBureau");

            Directory.CreateDirectory(appData);

            var dbPath = Path.Combine(appData, "marriage_bureau.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Biodata>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.Gender).IsRequired().HasDefaultValue("Male");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
        }
    }
}
