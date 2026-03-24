using MarriageBureau.Models;
using MarriageBureau.Services;
using Microsoft.EntityFrameworkCore;
using System.Configuration;
using System.IO;
using System.Text.RegularExpressions;

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
            var appData = GetAppDataPath();
            Directory.CreateDirectory(appData);

            var dbPath = Path.Combine(appData, "marriage_bureau.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        /// <summary>
        /// Returns the resolved application-data directory.
        /// Prefers the configured DataRootPath (App.config) if the drive exists;
        /// otherwise falls back to %APPDATA%\MarriageBureau so the app always starts.
        /// </summary>
        public static string GetAppDataPath()
        {
            var configuredRoot = ConfigurationManager.AppSettings["DataRootPath"];

            if (!string.IsNullOrWhiteSpace(configuredRoot))
            {
                var root = Path.GetPathRoot(configuredRoot);
                if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
                    return Path.Combine(configuredRoot, "MarriageBureau");
            }

            // Fallback: %APPDATA%\MarriageBureau (always writable on Windows)
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MarriageBureau");
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
                )",

                // v5: ProfileId column on Biodatas
                @"ALTER TABLE Biodatas ADD COLUMN ProfileId TEXT"
            };

            foreach (var sql in migrations)
            {
                try { ctx.Database.ExecuteSqlRaw(sql); }
                catch { /* column / table already exists – skip */ }
            }
        }

        // ── ProfileId generation ─────────────────────────────────────────────

        /// <summary>
        /// Returns a new unique ProfileId string, e.g. "TS001", "TS002" …
        /// The prefix comes from App.config key "ProfileIdPrefix" (default "MB").
        /// The numeric part is the next sequential number across ALL existing profiles
        /// that already have a ProfileId with the same prefix.
        /// </summary>
        public static string GenerateNextProfileId(AppDbContext ctx)
        {
            var prefix = (ConfigurationManager.AppSettings["ProfileIdPrefix"] ?? "MB").Trim().ToUpper();

            // Find the highest existing numeric suffix for this prefix
            var existing = ctx.Biodatas
                .Where(b => b.ProfileId != null && b.ProfileId.StartsWith(prefix))
                .Select(b => b.ProfileId!)
                .ToList();

            int maxNum = 0;
            foreach (var pid in existing)
            {
                var numPart = pid.Substring(prefix.Length);
                if (int.TryParse(numPart, out int n) && n > maxNum)
                    maxNum = n;
            }

            // Zero-pad to at least 3 digits
            int nextNum = maxNum + 1;
            int padWidth = Math.Max(3, (maxNum + 1).ToString().Length);
            return $"{prefix}{nextNum.ToString().PadLeft(padWidth, '0')}";
        }

        private static void SeedDefaultAdmin(AppDbContext ctx)
        {
            if (ctx.AppUsers.Any()) return;

            // Default admin: username = admin, password = Admin@123
            ctx.AppUsers.Add(new AppUser
            {
                Username     = "GCS",
                PasswordHash = CryptoService.HashPassword("Sams@1978"),
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
