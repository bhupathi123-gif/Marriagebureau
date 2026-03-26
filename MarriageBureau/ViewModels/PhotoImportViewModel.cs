using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using MarriageBureau.Data;
using MarriageBureau.Models;
using Microsoft.EntityFrameworkCore;

namespace MarriageBureau.ViewModels
{
    /// <summary>
    /// Represents a profile found during the photo scan — shows photo match info.
    /// </summary>
    public class PhotoImportRow
    {
        public string IntId { get; set; } = string.Empty;
        public string ProfileId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool HasExistingPhoto { get; set; }
        public List<string> MatchedPhotoPaths { get; set; } = new();
        public string MatchedPhotoDisplay => string.Join(", ", MatchedPhotoPaths.Select(Path.GetFileName));
        public int PhotoCount => MatchedPhotoPaths.Count;
        public string Status { get; set; } = "Pending";
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// ViewModel for bulk photo import — reads photos from a local folder
    /// and assigns them to profiles by IntId.
    ///
    /// Supported folder structures:
    ///   rootPath\&lt;intid&gt;\&lt;intid&gt;.ext       ← subfolder per person
    ///   rootPath\&lt;intid&gt;\&lt;intid&gt;_1.ext     ← subfolder per person, multiple photos
    ///   rootPath\&lt;intid&gt;.ext                      ← flat, single photo
    ///   rootPath\&lt;intid&gt;_1.ext                    ← flat, multiple photos
    ///   rootPath\&lt;intid&gt;_2.ext
    ///
    /// Stored as: Photos folder / &lt;intid&gt;_1.ext, &lt;intid&gt;_2.ext, …
    /// </summary>
    public class PhotoImportViewModel : BaseViewModel
    {
        private readonly MainViewModel _mainVm;

        private string _sourcePath = string.Empty;
        private bool   _isLoading;
        private bool   _isImporting;
        private bool   _skipExisting = true;
        private bool   _overwriteExisting;
        private string _statusMessage = string.Empty;
        private int    _importedCount;
        private int    _skippedCount;
        private int    _errorCount;
        private ObservableCollection<PhotoImportRow> _rows = new();

        // ── Properties ──────────────────────────────────────────────────────

        public string SourcePath
        {
            get => _sourcePath;
            set { SetProperty(ref _sourcePath, value); OnPropertyChanged(nameof(PathSelected)); }
        }

        public bool PathSelected => !string.IsNullOrWhiteSpace(SourcePath);

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool IsImporting
        {
            get => _isImporting;
            set => SetProperty(ref _isImporting, value);
        }

        public bool SkipExisting
        {
            get => _skipExisting;
            set => SetProperty(ref _skipExisting, value);
        }

        public bool OverwriteExisting
        {
            get => _overwriteExisting;
            set => SetProperty(ref _overwriteExisting, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public int ImportedCount
        {
            get => _importedCount;
            set => SetProperty(ref _importedCount, value);
        }

        public int SkippedCount
        {
            get => _skippedCount;
            set => SetProperty(ref _skippedCount, value);
        }

        public int ErrorCount
        {
            get => _errorCount;
            set => SetProperty(ref _errorCount, value);
        }

        public ObservableCollection<PhotoImportRow> Rows
        {
            get => _rows;
            set => SetProperty(ref _rows, value);
        }

        public int TotalRows => Rows.Count;

        // ── Commands ────────────────────────────────────────────────────────

        public ICommand BrowseFolderCommand  { get; }
        public ICommand ScanCommand          { get; }
        public ICommand ImportPhotosCommand  { get; }
        public ICommand CancelCommand        { get; }
        public ICommand ClearCommand         { get; }

        // ── Constructor ─────────────────────────────────────────────────────

        public PhotoImportViewModel(MainViewModel mainVm)
        {
            _mainVm = mainVm;

            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            ScanCommand         = new RelayCommand(async () => await ScanAsync(),
                                                    () => PathSelected && !IsLoading);
            ImportPhotosCommand = new RelayCommand(async () => await ImportAsync(),
                                                    () => Rows.Count > 0 && !IsImporting && !IsLoading);
            CancelCommand       = new RelayCommand(() => _mainVm.Navigate(AppPage.Browse));
            ClearCommand        = new RelayCommand(Clear);
        }

        // ── Browse Folder ────────────────────────────────────────────────────

        private void BrowseFolder()
        {
            // WPF doesn't have a built-in folder dialog – use OpenFileDialog trick or
            // System.Windows.Forms.FolderBrowserDialog via interop.
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description         = "Select the root folder containing profile photos",
                ShowNewFolderButton = false
            };

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SourcePath = dlg.SelectedPath;
                Rows.Clear();
                StatusMessage = $"Folder selected: {SourcePath}";
                ImportedCount = SkippedCount = ErrorCount = 0;
            }
        }

        // ── Scan ─────────────────────────────────────────────────────────────

        private async Task ScanAsync()
        {
            if (string.IsNullOrWhiteSpace(SourcePath) || !Directory.Exists(SourcePath)) return;

            IsLoading = true;
            StatusMessage = "Scanning folder for photos…";
            Rows.Clear();
            ImportedCount = SkippedCount = ErrorCount = 0;

            try
            {
                // Load all profiles that have an IntId
                List<Biodata> profiles;
                using (var ctx = new AppDbContext())
                {
                    profiles = await ctx.Biodatas
                        .Include(b => b.Photos)
                        .Where(b => b.IntId != null && b.IntId != "")
                        .OrderBy(b => b.IntId)
                        .ToListAsync();
                }

                var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };

                var scanned = await Task.Run(() =>
                {
                    var result = new List<PhotoImportRow>();

                    foreach (var profile in profiles)
                    {
                        var intId = profile.IntId;
                        var photoPaths = FindPhotosForIntId(SourcePath, intId, imageExtensions);

                        result.Add(new PhotoImportRow
                        {
                            IntId             = intId,
                            ProfileId         = profile.ProfileId ?? "",
                            Name              = profile.Name,
                            HasExistingPhoto  = profile.Photos.Count > 0 || !string.IsNullOrWhiteSpace(profile.PhotoPath),
                            MatchedPhotoPaths = photoPaths,
                            Status            = photoPaths.Count == 0 ? "No Photos Found" : "Pending"
                        });
                    }

                    return result;
                });

                foreach (var r in scanned)
                    Rows.Add(r);

                OnPropertyChanged(nameof(TotalRows));

                int found    = scanned.Count(r => r.PhotoCount > 0);
                int notFound = scanned.Count(r => r.PhotoCount == 0);
                StatusMessage = $"Scanned {profiles.Count} profiles. " +
                                $"Photos found for {found}. No photos for {notFound}. " +
                                $"Click Import to proceed.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Scan error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Import ────────────────────────────────────────────────────────────

        private async Task ImportAsync()
        {
            if (Rows.Count == 0) return;

            IsImporting = true;
            ImportedCount = SkippedCount = ErrorCount = 0;
            StatusMessage = "Importing photos…";

            var destDir = Path.Combine(AppDbContext.GetAppDataPath(), "Photos");
            Directory.CreateDirectory(destDir);

            try
            {
                using var ctx = new AppDbContext();
                var profileMap = await ctx.Biodatas
                    .Include(b => b.Photos)
                    .Where(b => b.IntId != null && b.IntId != "")
                    .ToDictionaryAsync(b => b.IntId, StringComparer.OrdinalIgnoreCase);

                foreach (var row in Rows)
                {
                    if (row.PhotoCount == 0)
                    {
                        row.Status = "No Photos Found";
                        SkippedCount++;
                        continue;
                    }

                    if (!profileMap.TryGetValue(row.IntId, out var profile))
                    {
                        row.Status       = "Error";
                        row.ErrorMessage = "Profile not found in database";
                        ErrorCount++;
                        continue;
                    }

                    // Skip if already has photos and SkipExisting is on
                    if (SkipExisting && (profile.Photos.Count > 0 || !string.IsNullOrWhiteSpace(profile.PhotoPath)))
                    {
                        row.Status = "Skipped (already has photos)";
                        SkippedCount++;
                        continue;
                    }

                    try
                    {
                        if (OverwriteExisting)
                        {
                            // Remove existing photos
                            var oldPhotos = await ctx.BiodataPhotos
                                .Where(p => p.BiodataId == profile.Id)
                                .ToListAsync();
                            ctx.BiodataPhotos.RemoveRange(oldPhotos);
                            await ctx.SaveChangesAsync();
                        }

                        int photoIndex = 1;
                        foreach (var srcPath in row.MatchedPhotoPaths)
                        {
                            if (!File.Exists(srcPath)) continue;

                            var ext      = Path.GetExtension(srcPath);
                            // Naming: IntId_1.ext, IntId_2.ext, …
                            var fileName = $"{row.IntId}_{photoIndex}{ext}";
                            var destPath = Path.Combine(destDir, fileName);

                            // If file already exists at dest, add counter suffix
                            int safety = 0;
                            while (File.Exists(destPath) && safety < 100)
                            {
                                safety++;
                                fileName = $"{row.IntId}_{photoIndex}_{safety}{ext}";
                                destPath = Path.Combine(destDir, fileName);
                            }

                            File.Copy(srcPath, destPath, overwrite: true);

                            var photo = new BiodataPhoto
                            {
                                BiodataId = profile.Id,
                                FilePath  = destPath,
                                SortOrder = photoIndex - 1
                            };
                            ctx.BiodataPhotos.Add(photo);

                            // Update cover photo on first photo
                            if (photoIndex == 1)
                            {
                                profile.PhotoPath = destPath;
                                ctx.Biodatas.Update(profile);
                            }

                            photoIndex++;
                        }

                        await ctx.SaveChangesAsync();

                        row.Status = $"Imported ({row.PhotoCount} photo(s))";
                        ImportedCount++;
                    }
                    catch (Exception ex)
                    {
                        row.Status       = "Error";
                        row.ErrorMessage = ex.Message;
                        ErrorCount++;
                    }
                }

                StatusMessage = $"Done! Imported: {ImportedCount}  |  Skipped: {SkippedCount}  |  Errors: {ErrorCount}";
                OnPropertyChanged(nameof(Rows));
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Import failed: {ex.Message}";
            }
            finally
            {
                IsImporting = false;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Finds photos for a given IntId in the source folder.
        /// Looks for:
        ///   rootPath\{intid}\{intid}.ext
        ///   rootPath\{intid}\{intid}_1.ext, {intid}_2.ext, ...
        ///   rootPath\{intid}\*.ext  (any image in subfolder)
        ///   rootPath\{intid}.ext
        ///   rootPath\{intid}_1.ext, {intid}_2.ext, ...
        /// </summary>
        private static List<string> FindPhotosForIntId(
            string rootPath, string intId, HashSet<string> imageExtensions)
        {
            var found = new List<string>();
            string safeId = intId.Trim();

            // 1. Look in subfolder rootPath\{intid}\
            var subDir = Path.Combine(rootPath, safeId);
            if (Directory.Exists(subDir))
            {
                var subFiles = Directory.GetFiles(subDir)
                    .Where(f => imageExtensions.Contains(Path.GetExtension(f)))
                    .OrderBy(f => f)
                    .ToList();
                found.AddRange(subFiles);
            }

            // 2. Look in root: files matching {intid}.ext or {intid}_N.ext or {intid}_N_M.ext
            if (Directory.Exists(rootPath))
            {
                var rootFiles = Directory.GetFiles(rootPath)
                    .Where(f =>
                    {
                        if (!imageExtensions.Contains(Path.GetExtension(f))) return false;
                        var stem = Path.GetFileNameWithoutExtension(f);
                        // Exact match: intid
                        if (string.Equals(stem, safeId, StringComparison.OrdinalIgnoreCase)) return true;
                        // Prefix match: intid_ (e.g. M1_1, M1_2)
                        if (stem.StartsWith(safeId + "_", StringComparison.OrdinalIgnoreCase)) return true;
                        return false;
                    })
                    .OrderBy(f => f)
                    .ToList();

                // Add only files not already found via subfolder
                foreach (var f in rootFiles)
                    if (!found.Contains(f)) found.Add(f);
            }

            return found;
        }

        // ── Clear ─────────────────────────────────────────────────────────────

        private void Clear()
        {
            SourcePath = string.Empty;
            Rows.Clear();
            StatusMessage = string.Empty;
            ImportedCount = SkippedCount = ErrorCount = 0;
            OnPropertyChanged(nameof(TotalRows));
        }
    }
}
