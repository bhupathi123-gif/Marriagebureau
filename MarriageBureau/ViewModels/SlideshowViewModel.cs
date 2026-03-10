using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MarriageBureau.Data;
using MarriageBureau.Models;
using Microsoft.EntityFrameworkCore;

namespace MarriageBureau.ViewModels
{
    public class SlideshowViewModel : BaseViewModel
    {
        private ObservableCollection<Biodata> _profiles = new();
        private Biodata? _currentProfile;
        private int _currentProfileIndex = 0;

        // Per-profile photo slideshow
        private List<string> _currentProfilePhotoPaths = new();
        private int _currentPhotoIndex = 0;
        private BitmapImage? _currentPhoto;

        private bool _isPlaying;
        private bool _isLoading;

        // ── Filters ───────────────────────────────────────────────────
        private string _genderFilter   = "All";
        private string _casteFilter    = string.Empty;
        private string _districtFilter = string.Empty;
        private string _starFilter     = "All";
        private string _raasiFilter    = "All";
        private string _statusFilter   = "All";
        private int    _minAge         = 18;
        private int    _maxAge         = 50;
        private bool   _filterPanelOpen = false;

        // Available option lists (populated after load)
        private List<string> _casteOptions     = new() { "" };
        private List<string> _districtOptions  = new() { "" };

        private readonly DispatcherTimer _timer;

        // ── Profiles & current ──────────────────────────────────────

        public ObservableCollection<Biodata> Profiles
        {
            get => _profiles;
            set => SetProperty(ref _profiles, value);
        }

        public Biodata? CurrentProfile
        {
            get => _currentProfile;
            set
            {
                SetProperty(ref _currentProfile, value);
                LoadProfilePhotos();
                OnPropertyChanged(nameof(ProfileCounterText));
                OnPropertyChanged(nameof(HasCurrentProfile));
            }
        }

        public BitmapImage? CurrentPhoto
        {
            get => _currentPhoto;
            set => SetProperty(ref _currentPhoto, value);
        }

        // ── Photo slideshow ────────────────────────────────────────
        public int CurrentPhotoIndex
        {
            get => _currentPhotoIndex;
            set
            {
                SetProperty(ref _currentPhotoIndex, value);
                LoadCurrentPhoto();
                OnPropertyChanged(nameof(PhotoCounterText));
                OnPropertyChanged(nameof(PhotoDots));
            }
        }

        public List<string> CurrentProfilePhotoPaths
        {
            get => _currentProfilePhotoPaths;
            set
            {
                SetProperty(ref _currentProfilePhotoPaths, value);
                OnPropertyChanged(nameof(HasMultiplePhotos));
                OnPropertyChanged(nameof(PhotoCounterText));
                OnPropertyChanged(nameof(PhotoDots));
            }
        }

        public List<bool> PhotoDots
        {
            get
            {
                var dots = new List<bool>();
                for (int i = 0; i < _currentProfilePhotoPaths.Count; i++)
                    dots.Add(i == _currentPhotoIndex);
                return dots;
            }
        }

        public bool HasMultiplePhotos => _currentProfilePhotoPaths.Count > 1;
        public string PhotoCounterText => _currentProfilePhotoPaths.Count <= 1
                                            ? string.Empty
                                            : $"Photo {_currentPhotoIndex + 1}/{_currentProfilePhotoPaths.Count}";

        // ── Playback ────────────────────────────────────────────────

        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                SetProperty(ref _isPlaying, value);
                if (_isPlaying) _timer.Start();
                else _timer.Stop();
                OnPropertyChanged(nameof(PlayPauseLabel));
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string PlayPauseLabel => IsPlaying ? "⏸ Pause" : "▶ Play";
        public string ProfileCounterText => Profiles.Count == 0 ? "No profiles"
                                           : $"{_currentProfileIndex + 1} / {Profiles.Count}";
        public bool HasCurrentProfile => CurrentProfile != null;

        public int SlideIntervalSeconds { get; set; } = 5;

        // ── Filter Properties ────────────────────────────────────────

        public string GenderFilter
        {
            get => _genderFilter;
            set { SetProperty(ref _genderFilter, value); _ = LoadAsync(); }
        }

        public string CasteFilter
        {
            get => _casteFilter;
            set { SetProperty(ref _casteFilter, value); _ = LoadAsync(); }
        }

        public string DistrictFilter
        {
            get => _districtFilter;
            set { SetProperty(ref _districtFilter, value); _ = LoadAsync(); }
        }

        public string StarFilter
        {
            get => _starFilter;
            set { SetProperty(ref _starFilter, value); _ = LoadAsync(); }
        }

        public string RaasiFilter
        {
            get => _raasiFilter;
            set { SetProperty(ref _raasiFilter, value); _ = LoadAsync(); }
        }

        public string StatusFilter
        {
            get => _statusFilter;
            set { SetProperty(ref _statusFilter, value); _ = LoadAsync(); }
        }

        public int MinAge
        {
            get => _minAge;
            set { SetProperty(ref _minAge, value); _ = LoadAsync(); }
        }

        public int MaxAge
        {
            get => _maxAge;
            set { SetProperty(ref _maxAge, value); _ = LoadAsync(); }
        }

        public bool FilterPanelOpen
        {
            get => _filterPanelOpen;
            set => SetProperty(ref _filterPanelOpen, value);
        }

        // ── Option Lists ─────────────────────────────────────────────

        public List<string> GenderOptions  { get; } = new() { "All", "MALE", "FEMALE" };
        public List<string> StatusOptions  { get; } = new List<string> { "All" }
            .Concat(Enum.GetNames<ProfileStatus>()).ToList();

        public static readonly List<string> StarOptions = new()
        {
            "All",
            "ASHWINI","BHARANI","KARTHIKA","ROHINI","MRIGASIRA","ARIDRA","PUNARVASU",
            "PUSHYAMI","ASLESHA","MAGHA","PUBBA","UTTARA","HASTA","CHITTA","SWATHI",
            "VISAKHA","ANURADHA","JYESHTHA","MOOLA","POORVASHADA","UTTARASHADA",
            "SRAVANA","DHANISHTHA","SATABHISHA","POORVABHADRA","UTTARABHADRA","REVATHI"
        };

        public static readonly List<string> RaasiOptions = new()
        {
            "All",
            "MESHA","VRUSHABA","MIDHUNA","KARKATAKA","SIMHA","KANYA",
            "TULA","VRUCHIKA","DHANU","MAKARA","KUMBHA","MEENA"
        };

        public List<string> CasteOptions
        {
            get => _casteOptions;
            set => SetProperty(ref _casteOptions, value);
        }

        public List<string> DistrictOptions
        {
            get => _districtOptions;
            set => SetProperty(ref _districtOptions, value);
        }

        public bool HasActiveFilters =>
            GenderFilter   != "All" ||
            !string.IsNullOrWhiteSpace(CasteFilter) ||
            !string.IsNullOrWhiteSpace(DistrictFilter) ||
            StarFilter     != "All" ||
            RaasiFilter    != "All" ||
            StatusFilter   != "All" ||
            MinAge         != 18    ||
            MaxAge         != 50;

        // ── Commands ────────────────────────────────────────────────
        public ICommand PreviousProfileCommand { get; }
        public ICommand NextProfileCommand     { get; }
        public ICommand PrevPhotoCommand       { get; }
        public ICommand NextPhotoCommand       { get; }
        public ICommand PlayPauseCommand       { get; }
        public ICommand RefreshCommand         { get; }
        public ICommand ToggleFilterPanelCommand { get; }
        public ICommand ClearFiltersCommand    { get; }

        // Legacy aliases kept for XAML backward compat
        public ICommand PreviousCommand => PreviousProfileCommand;
        public ICommand NextCommand     => NextProfileCommand;

        private readonly MainViewModel _mainVm;

        public SlideshowViewModel(MainViewModel mainVm)
        {
            _mainVm = mainVm;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(SlideIntervalSeconds) };
            _timer.Tick += OnTimerTick;

            PreviousProfileCommand   = new RelayCommand(MovePrevProfile, () => Profiles.Count > 1);
            NextProfileCommand       = new RelayCommand(MoveNextProfile, () => Profiles.Count > 1);
            PrevPhotoCommand         = new RelayCommand(PrevPhoto, () => _currentPhotoIndex > 0);
            NextPhotoCommand         = new RelayCommand(NextPhoto, () => _currentPhotoIndex < _currentProfilePhotoPaths.Count - 1);
            PlayPauseCommand         = new RelayCommand(TogglePlay, () => Profiles.Count > 0);
            RefreshCommand           = new RelayCommand(async () => await LoadAsync());
            ToggleFilterPanelCommand = new RelayCommand(() => FilterPanelOpen = !FilterPanelOpen);
            ClearFiltersCommand      = new RelayCommand(ClearFilters);
        }

        // ── Load / Filter ─────────────────────────────────────────────

        public async Task LoadAsync()
        {
            if (IsLoading) return;
            IsPlaying = false;
            IsLoading = true;
            try
            {
                using var ctx = new AppDbContext();

                // Load ALL profiles with photos for building option lists
                var allProfiles = await ctx.Biodatas
                                           .Include(b => b.Photos)
                                           .ToListAsync();

                // Rebuild dropdown lists from all profiles
                var casteList = allProfiles
                    .Select(b => b.Caste ?? "")
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList();
                CasteOptions = new List<string> { "" }.Concat(casteList).ToList();

                var districtList = allProfiles
                    .Select(b => b.District ?? "")
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Distinct()
                    .OrderBy(d => d)
                    .ToList();
                DistrictOptions = new List<string> { "" }.Concat(districtList).ToList();

                // Apply filters
                var q = allProfiles.AsEnumerable();

                if (GenderFilter != "All")
                    q = q.Where(b => b.Gender?.ToUpper() == GenderFilter.ToUpper());

                if (!string.IsNullOrWhiteSpace(CasteFilter))
                    q = q.Where(b => b.Caste?.ToUpper() == CasteFilter.ToUpper());

                if (!string.IsNullOrWhiteSpace(DistrictFilter))
                    q = q.Where(b => b.District?.ToUpper() == DistrictFilter.ToUpper());

                if (StarFilter != "All")
                    q = q.Where(b => b.BirthStar?.ToUpper() == StarFilter.ToUpper());

                if (RaasiFilter != "All")
                    q = q.Where(b => b.Raasi?.ToUpper() == RaasiFilter.ToUpper());

                if (StatusFilter != "All" && Enum.TryParse<ProfileStatus>(StatusFilter, out var statusEnum))
                    q = q.Where(b => b.Status == statusEnum);

                // Age filter
                q = q.Where(b =>
                {
                    if (string.IsNullOrWhiteSpace(b.DateOfBirth)) return true;
                    // Parse age from AgeDisplay (e.g. "25 yrs")
                    var ageStr = b.AgeDisplay?.Replace("yrs","").Trim();
                    if (int.TryParse(ageStr, out int age))
                        return age >= MinAge && age <= MaxAge;
                    return true;
                });

                var list = q.OrderBy(b => b.Name).ToList();
                Profiles = new ObservableCollection<Biodata>(list);
                _currentProfileIndex = 0;
                CurrentProfile = Profiles.Count > 0 ? Profiles[0] : null;

                OnPropertyChanged(nameof(HasActiveFilters));
            }
            finally { IsLoading = false; }
        }

        private void ClearFilters()
        {
            // Suppress multiple reload triggers by setting backing fields directly
            _genderFilter   = "All";
            _casteFilter    = string.Empty;
            _districtFilter = string.Empty;
            _starFilter     = "All";
            _raasiFilter    = "All";
            _statusFilter   = "All";
            _minAge         = 18;
            _maxAge         = 50;

            OnPropertyChanged(nameof(GenderFilter));
            OnPropertyChanged(nameof(CasteFilter));
            OnPropertyChanged(nameof(DistrictFilter));
            OnPropertyChanged(nameof(StarFilter));
            OnPropertyChanged(nameof(RaasiFilter));
            OnPropertyChanged(nameof(StatusFilter));
            OnPropertyChanged(nameof(MinAge));
            OnPropertyChanged(nameof(MaxAge));
            OnPropertyChanged(nameof(HasActiveFilters));

            _ = LoadAsync();
        }

        // ── Profile Navigation ────────────────────────────────────────

        private void MoveNextProfile()
        {
            if (Profiles.Count == 0) return;
            _currentProfileIndex = (_currentProfileIndex + 1) % Profiles.Count;
            CurrentProfile = Profiles[_currentProfileIndex];
            CommandManager.InvalidateRequerySuggested();
        }

        private void MovePrevProfile()
        {
            if (Profiles.Count == 0) return;
            _currentProfileIndex = (_currentProfileIndex - 1 + Profiles.Count) % Profiles.Count;
            CurrentProfile = Profiles[_currentProfileIndex];
            CommandManager.InvalidateRequerySuggested();
        }

        // ── Per-profile Photo Navigation ──────────────────────────────

        private void PrevPhoto()
        {
            if (_currentPhotoIndex > 0)
                CurrentPhotoIndex--;
            CommandManager.InvalidateRequerySuggested();
        }

        private void NextPhoto()
        {
            if (_currentPhotoIndex < _currentProfilePhotoPaths.Count - 1)
                CurrentPhotoIndex++;
            CommandManager.InvalidateRequerySuggested();
        }

        // ── Timer tick ────────────────────────────────────────────────

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_currentProfilePhotoPaths.Count > 1 &&
                _currentPhotoIndex < _currentProfilePhotoPaths.Count - 1)
            {
                CurrentPhotoIndex++;
            }
            else
            {
                MoveNextProfile();
            }
        }

        private void TogglePlay() => IsPlaying = !IsPlaying;

        // ── Photo Loading ─────────────────────────────────────────────

        private void LoadProfilePhotos()
        {
            if (CurrentProfile == null)
            {
                CurrentProfilePhotoPaths = new List<string>();
                CurrentPhoto = null;
                return;
            }

            var paths = new List<string>();

            if (CurrentProfile.Photos != null)
                foreach (var p in CurrentProfile.Photos.OrderBy(x => x.SortOrder))
                    if (p.Exists) paths.Add(p.FilePath);

            if (!string.IsNullOrWhiteSpace(CurrentProfile.PhotoPath)
                && System.IO.File.Exists(CurrentProfile.PhotoPath)
                && !paths.Contains(CurrentProfile.PhotoPath))
            {
                paths.Insert(0, CurrentProfile.PhotoPath);
            }

            _currentPhotoIndex = 0;
            CurrentProfilePhotoPaths = paths;
            LoadCurrentPhoto();
            CommandManager.InvalidateRequerySuggested();
        }

        private void LoadCurrentPhoto()
        {
            if (_currentProfilePhotoPaths.Count == 0
                || _currentPhotoIndex < 0
                || _currentPhotoIndex >= _currentProfilePhotoPaths.Count)
            {
                CurrentPhoto = null;
                return;
            }

            var path = _currentProfilePhotoPaths[_currentPhotoIndex];
            if (!System.IO.File.Exists(path))
            {
                CurrentPhoto = null;
                return;
            }

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource        = new Uri(path, UriKind.Absolute);
                bmp.CacheOption      = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 600;
                bmp.EndInit();
                bmp.Freeze();
                CurrentPhoto = bmp;
            }
            catch { CurrentPhoto = null; }
        }
    }
}
