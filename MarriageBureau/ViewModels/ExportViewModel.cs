using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MarriageBureau.Data;
using MarriageBureau.Models;
using MarriageBureau.Services;
using Microsoft.EntityFrameworkCore;

namespace MarriageBureau.ViewModels
{
    public class ExportViewModel : BaseViewModel
    {
        private readonly MainViewModel _mainVm;

        // ── Profile selection ────────────────────────────────────────
        private ObservableCollection<Biodata> _allProfiles = new();
        private ObservableCollection<Biodata> _filteredProfiles = new();
        private Biodata? _selectedProfile;
        private string _searchText = string.Empty;
        private string _genderFilter = "All";

        // ── Photo list for the selected profile ─────────────────────
        private ObservableCollection<string> _photoPaths = new();
        private BitmapImage? _preview1;
        private BitmapImage? _preview2;

        // ── Export state ─────────────────────────────────────────────
        private bool _isExporting;
        private string _statusMessage = string.Empty;
        private string _lastExportPath = string.Empty;

        // ── Properties ──────────────────────────────────────────────

        public ObservableCollection<Biodata> FilteredProfiles
        {
            get => _filteredProfiles;
            set => SetProperty(ref _filteredProfiles, value);
        }

        public Biodata? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                SetProperty(ref _selectedProfile, value);
                LoadProfilePhotos();
                OnPropertyChanged(nameof(HasSelection));
            }
        }

        public string SearchText
        {
            get => _searchText;
            set { SetProperty(ref _searchText, value); ApplyFilter(); }
        }

        public string GenderFilter
        {
            get => _genderFilter;
            set { SetProperty(ref _genderFilter, value); ApplyFilter(); }
        }

        public ObservableCollection<string> PhotoPaths
        {
            get => _photoPaths;
            set => SetProperty(ref _photoPaths, value);
        }

        public BitmapImage? Preview1
        {
            get => _preview1;
            set => SetProperty(ref _preview1, value);
        }

        public BitmapImage? Preview2
        {
            get => _preview2;
            set => SetProperty(ref _preview2, value);
        }

        public bool IsExporting
        {
            get => _isExporting;
            set => SetProperty(ref _isExporting, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string LastExportPath
        {
            get => _lastExportPath;
            set { SetProperty(ref _lastExportPath, value); OnPropertyChanged(nameof(HasExportFile)); }
        }

        public bool HasSelection  => SelectedProfile != null;
        public bool HasExportFile => !string.IsNullOrWhiteSpace(LastExportPath) && File.Exists(LastExportPath);

        public List<string> GenderOptions { get; } = new() { "All", "MALE", "FEMALE" };

        // ── Commands ────────────────────────────────────────────────

        public ICommand LoadProfilesCommand { get; }
        public ICommand ExportPdfCommand    { get; }
        public ICommand ExportImageCommand  { get; }
        public ICommand OpenExportCommand   { get; }
        public ICommand CancelCommand       { get; }
        public ICommand ClearSearchCommand  { get; }

        // ── Constructor ─────────────────────────────────────────────

        public ExportViewModel(MainViewModel mainVm, Biodata? preSelected = null)
        {
            _mainVm = mainVm;

            LoadProfilesCommand = new RelayCommand(async () => await LoadProfilesAsync());
            ExportPdfCommand    = new RelayCommand(async () => await ExportAsync(false),
                                                    () => HasSelection && !IsExporting);
            ExportImageCommand  = new RelayCommand(async () => await ExportAsync(true),
                                                    () => HasSelection && !IsExporting);
            OpenExportCommand   = new RelayCommand(OpenLastExport, () => HasExportFile);
            CancelCommand       = new RelayCommand(() => _mainVm.Navigate(AppPage.Browse));
            ClearSearchCommand  = new RelayCommand(ClearSearch);

            // Load then auto-select
            _ = LoadProfilesAndSelectAsync(preSelected);
        }

        // ── Data Loading ─────────────────────────────────────────────

        private async Task LoadProfilesAsync()
        {
            using var ctx = new AppDbContext();
            var list = await ctx.Biodatas
                               .Include(b => b.Photos.OrderBy(p => p.SortOrder))
                               .OrderBy(b => b.Name)
                               .ToListAsync();
            _allProfiles = new ObservableCollection<Biodata>(list);
            ApplyFilter();
        }

        private async Task LoadProfilesAndSelectAsync(Biodata? preSelected)
        {
            await LoadProfilesAsync();
            if (preSelected != null)
            {
                SelectedProfile = _allProfiles.FirstOrDefault(p => p.Id == preSelected.Id)
                               ?? _allProfiles.FirstOrDefault();
            }
        }

        private void ApplyFilter()
        {
            var q = _allProfiles.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var st = SearchText.ToLower();
                q = q.Where(p =>
                    (p.Name?.ToLower().Contains(st) ?? false) ||
                    (p.Caste?.ToLower().Contains(st) ?? false) ||
                    (p.District?.ToLower().Contains(st) ?? false));
            }

            if (GenderFilter != "All")
                q = q.Where(p => p.Gender?.ToUpper() == GenderFilter.ToUpper());

            FilteredProfiles = new ObservableCollection<Biodata>(q);
        }

        private void ClearSearch()
        {
            SearchText   = string.Empty;
            GenderFilter = "All";
        }

        private void LoadProfilePhotos()
        {
            Preview1 = Preview2 = null;
            PhotoPaths.Clear();

            if (SelectedProfile == null) return;

            // Gather photos: gallery first, then legacy
            var paths = new List<string>();
            if (SelectedProfile.Photos != null)
                foreach (var p in SelectedProfile.Photos.OrderBy(x => x.SortOrder))
                    if (p.Exists) paths.Add(p.FilePath);

            if (!string.IsNullOrWhiteSpace(SelectedProfile.PhotoPath)
                && File.Exists(SelectedProfile.PhotoPath)
                && !paths.Contains(SelectedProfile.PhotoPath))
                paths.Insert(0, SelectedProfile.PhotoPath);

            foreach (var p in paths) PhotoPaths.Add(p);

            Preview1 = LoadImage(paths.Count > 0 ? paths[0] : null);
            Preview2 = LoadImage(paths.Count > 1 ? paths[1] : null);
        }

        private static BitmapImage? LoadImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource     = new Uri(path, UriKind.Absolute);
                bmp.CacheOption   = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 300;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        // ── Export ───────────────────────────────────────────────────

        private async Task ExportAsync(bool asImage)
        {
            if (SelectedProfile == null) return;

            var ext  = asImage ? ".jpg" : ".pdf";
            var desc = asImage ? "JPEG Image|*.jpg" : "PDF Document|*.pdf";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title            = asImage ? "Save as Image" : "Save as PDF",
                Filter           = desc,
                FileName         = $"Biodata_{SelectedProfile.Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}",
                DefaultExt       = ext
            };
            if (dlg.ShowDialog() != true) return;

            IsExporting   = true;
            StatusMessage = asImage ? "Generating image…" : "Generating PDF…";

            try
            {
                var photos = PhotoPaths.Take(2).ToList();
                var outPath = dlg.FileName;

                await Task.Run(() =>
                {
                    if (asImage)
                        BiodataExportService.ExportToImage(SelectedProfile, photos, outPath);
                    else
                        BiodataExportService.ExportToPdf(SelectedProfile, photos, outPath);
                });

                LastExportPath = outPath;
                StatusMessage  = $"Exported successfully → {Path.GetFileName(outPath)}";
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
            }
            finally
            {
                IsExporting = false;
            }
        }

        private void OpenLastExport()
        {
            if (!HasExportFile) return;
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(LastExportPath)
                    { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Cannot open file: {ex.Message}";
            }
        }
    }
}
