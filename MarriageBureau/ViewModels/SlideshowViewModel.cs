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
        private string _genderFilter = "All";
        private readonly DispatcherTimer _timer;

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

        // ── Profile-level photo slideshow ────────────────────────────
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

        /// <summary>Dot indicators (one bool per photo – true = selected)</summary>
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

        public string GenderFilter
        {
            get => _genderFilter;
            set { SetProperty(ref _genderFilter, value); _ = LoadAsync(); }
        }

        public string PlayPauseLabel => IsPlaying ? "⏸ Pause" : "▶ Play";
        public string ProfileCounterText => Profiles.Count == 0 ? "No profiles"
                                           : $"{_currentProfileIndex + 1} / {Profiles.Count}";
        public bool HasCurrentProfile => CurrentProfile != null;

        public int SlideIntervalSeconds { get; set; } = 5;

        public List<string> GenderOptions { get; } = new() { "All", "MALE", "FEMALE" };

        // ── Commands ────────────────────────────────────────────────
        public ICommand PreviousProfileCommand { get; }
        public ICommand NextProfileCommand     { get; }
        public ICommand PrevPhotoCommand       { get; }
        public ICommand NextPhotoCommand       { get; }
        public ICommand PlayPauseCommand       { get; }
        public ICommand RefreshCommand         { get; }

        // Legacy aliases kept for XAML backward compat
        public ICommand PreviousCommand => PreviousProfileCommand;
        public ICommand NextCommand     => NextProfileCommand;

        private readonly MainViewModel _mainVm;

        public SlideshowViewModel(MainViewModel mainVm)
        {
            _mainVm = mainVm;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(SlideIntervalSeconds) };
            _timer.Tick += OnTimerTick;

            PreviousProfileCommand = new RelayCommand(MovePrevProfile, () => Profiles.Count > 1);
            NextProfileCommand     = new RelayCommand(MoveNextProfile, () => Profiles.Count > 1);
            PrevPhotoCommand       = new RelayCommand(PrevPhoto, () => _currentPhotoIndex > 0);
            NextPhotoCommand       = new RelayCommand(NextPhoto, () => _currentPhotoIndex < _currentProfilePhotoPaths.Count - 1);
            PlayPauseCommand       = new RelayCommand(TogglePlay, () => Profiles.Count > 0);
            RefreshCommand         = new RelayCommand(async () => await LoadAsync());
        }

        public async Task LoadAsync()
        {
            IsPlaying = false;
            IsLoading = true;
            try
            {
                using var ctx = new AppDbContext();
                var q = ctx.Biodatas
                           .Include(b => b.Photos.OrderBy(p => p.SortOrder))
                           .AsQueryable();

                if (GenderFilter != "All")
                    q = q.Where(b => b.Gender!.ToUpper() == GenderFilter.ToUpper());

                var list = await q.OrderBy(b => b.Name).ToListAsync();
                Profiles = new ObservableCollection<Biodata>(list);
                _currentProfileIndex = 0;
                CurrentProfile = Profiles.Count > 0 ? Profiles[0] : null;
            }
            finally { IsLoading = false; }
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

        // ── Timer tick: advance photos within a profile first, then move to next profile ──

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_currentProfilePhotoPaths.Count > 1 &&
                _currentPhotoIndex < _currentProfilePhotoPaths.Count - 1)
            {
                // Show next photo of the same profile
                CurrentPhotoIndex++;
            }
            else
            {
                // Move to next profile
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

            // Add gallery photos
            if (CurrentProfile.Photos != null)
                foreach (var p in CurrentProfile.Photos.OrderBy(x => x.SortOrder))
                    if (p.Exists) paths.Add(p.FilePath);

            // Back-compat: also add legacy PhotoPath if not already in gallery
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
