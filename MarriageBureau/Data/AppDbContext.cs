using MarriageBureau.Models;
using MarriageBureau.Services;
using Microsoft.EntityFrameworkCore;
using System.Configuration;
using System.IO;

namespace MarriageBureau.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Biodata>      Biodatas     { get; set; }
        public DbSet<BiodataPhoto> BiodataPhotos { get; set; }
        public DbSet<AppUser>      AppUsers      { get; set; }
        public DbSet<AppSettings>  AppSettings   { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var rootPath = ConfigurationManager.AppSettings["DataRootPath"];

            var appData = Path.Combine(rootPath, "MarriageBureau");
            Directory.CreateDirectory(appData);

            var dbPath = Path.Combine(appData, "marriage_bureau.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── Biodata ──────────────────────────────────────────────────
            modelBuilder.Entity<Biodata>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.Gender).IsRequired().HasDefaultValue("Male");
                entity.Property(e => e.Status).HasDefaultValue(ProfileStatus.Active);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasMany(e => e.Photos)
                      .WithOne(p => p.Biodata)
                      .HasForeignKey(p => p.BiodataId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── BiodataPhoto ─────────────────────────────────────────────
            modelBuilder.Entity<BiodataPhoto>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FilePath).IsRequired();
                entity.Property(e => e.AddedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // ── AppUser ──────────────────────────────────────────────────
            modelBuilder.Entity<AppUser>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).IsRequired();
                entity.HasIndex(e => e.Username).IsUnique();
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.Role).HasDefaultValue("User");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // ── AppSettings ───────────────────────────────────────────────
            modelBuilder.Entity<AppSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
            });
        }

        /// <summary>
        /// Ensures the database and all tables exist.
        /// Also seeds a default admin user if no users exist.
        /// </summary>
        public static void EnsureCreated()
        {
            using var ctx = new AppDbContext();
            ctx.Database.EnsureCreated();

            // Schema migrations for tables added in later versions
            RunSchemaMigrations(ctx);

            // Seed default admin if no users exist
            SeedDefaultAdmin(ctx);

            // Seed default AppSettings row if missing
            SeedAppSettings(ctx);
        }

        private static void RunSchemaMigrations(AppDbContext ctx)
        {
            var migrations = new[]
            {
                // v1: BiodataPhotos table
                @"CREATE TABLE IF NOT EXISTS BiodataPhotos (
                    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                    BiodataId INTEGER NOT NULL,
                    FilePath  TEXT    NOT NULL,
                    SortOrder INTEGER NOT NULL DEFAULT 0,
                    Caption   TEXT,
                    AddedAt   TEXT    NOT NULL DEFAULT (CURRENT_TIMESTAMP),
                    FOREIGN KEY (BiodataId) REFERENCES Biodatas(Id) ON DELETE CASCADE
                )",

                // v2: ProfileStatus column on Biodatas (default Active = 0)
                @"ALTER TABLE Biodatas ADD COLUMN Status INTEGER NOT NULL DEFAULT 0",

                // v3: AppUsers table
                @"CREATE TABLE IF NOT EXISTS AppUsers (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username     TEXT    NOT NULL UNIQUE,
                    PasswordHash TEXT    NOT NULL,
                    FullName     TEXT,
                    Role         TEXT    NOT NULL DEFAULT 'User',
                    IsActive     INTEGER NOT NULL DEFAULT 1,
                    CreatedAt    TEXT    NOT NULL DEFAULT (CURRENT_TIMESTAMP),
                    LastLogin    TEXT
                )",

                // v4: AppSettings table
                @"CREATE TABLE IF NOT EXISTS AppSettings (
                    Id                     INTEGER PRIMARY KEY DEFAULT 1,
                    EncryptedBusinessName  TEXT,
                    SecurityCode           TEXT
                )"
            };

            foreach (var sql in migrations)
            {
                try { ctx.Database.ExecuteSqlRaw(sql); }
                catch { /* column / table already exists – skip */ }
            }
        }

        private static void SeedDefaultAdmin(AppDbContext ctx)
        {
            if (ctx.AppUsers.Any()) return;

            // Default admin: username = admin, password = Admin@123
            ctx.AppUsers.Add(new AppUser
            {
                Username     = "admin",
                PasswordHash = CryptoService.HashPassword("Admin@123"),
                FullName     = "Administrator",
                Role         = "Admin",
                IsActive     = true
            });
            ctx.SaveChanges();
        }

        private static void SeedAppSettings(AppDbContext ctx)
        {
            if (ctx.AppSettings.Any()) return;

            ctx.AppSettings.Add(new AppSettings { Id = 1 });
            ctx.SaveChanges();
        }
    }
}
