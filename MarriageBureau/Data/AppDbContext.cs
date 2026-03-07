using MarriageBureau.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace MarriageBureau.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Biodata> Biodatas { get; set; }
        public DbSet<BiodataPhoto> BiodataPhotos { get; set; }

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

                // One Biodata → many BiodataPhotos
                entity.HasMany(e => e.Photos)
                      .WithOne(p => p.Biodata)
                      .HasForeignKey(p => p.BiodataId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<BiodataPhoto>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FilePath).IsRequired();
                entity.Property(e => e.AddedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
        }

        /// <summary>
        /// Ensures the database and all tables exist (creates if missing, applies migrations automatically).
        /// Call once at startup.
        /// </summary>
        public static void EnsureCreated()
        {
            using var ctx = new AppDbContext();
            ctx.Database.EnsureCreated();

            // Run any missing schema additions (e.g. BiodataPhotos table added later)
            try
            {
                ctx.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS BiodataPhotos (
                        Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                        BiodataId INTEGER NOT NULL,
                        FilePath  TEXT    NOT NULL,
                        SortOrder INTEGER NOT NULL DEFAULT 0,
                        Caption   TEXT,
                        AddedAt   TEXT    NOT NULL DEFAULT (CURRENT_TIMESTAMP),
                        FOREIGN KEY (BiodataId) REFERENCES Biodatas(Id) ON DELETE CASCADE
                    )");
            }
            catch { /* Table might already exist */ }
        }
    }
}
