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
        private int _currentIndex = 0;
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
                LoadCurrentPhoto();
                OnPropertyChanged(nameof(CounterText));
                OnPropertyChanged(nameof(HasCurrentProfile));
            }
        }

        public BitmapImage? CurrentPhoto
        {
            get => _currentPhoto;
            set => SetProperty(ref _currentPhoto, value);
        }

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
        public string CounterText => Profiles.Count == 0 ? "No profiles" : $"{_currentIndex + 1} / {Profiles.Count}";
        public bool HasCurrentProfile => CurrentProfile != null;

        public int SlideIntervalSeconds { get; set; } = 5;

        public List<string> GenderOptions { get; } = new() { "All", "MALE", "FEMALE" };

        public ICommand PreviousCommand { get; }
        public ICommand NextCommand { get; }
        public ICommand PlayPauseCommand { get; }
        public ICommand RefreshCommand { get; }

        private readonly MainViewModel _mainVm;

        public SlideshowViewModel(MainViewModel mainVm)
        {
            _mainVm = mainVm;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(SlideIntervalSeconds) };
            _timer.Tick += (_, _) => MoveNext();

            PreviousCommand  = new RelayCommand(MovePrev, () => Profiles.Count > 1);
            NextCommand      = new RelayCommand(MoveNext, () => Profiles.Count > 1);
            PlayPauseCommand = new RelayCommand(TogglePlay, () => Profiles.Count > 0);
            RefreshCommand   = new RelayCommand(async () => await LoadAsync());
        }

        public async Task LoadAsync()
        {
            IsPlaying = false;
            IsLoading = true;
            try
            {
                using var ctx = new AppDbContext();
                var q = ctx.Biodatas.AsQueryable();
                if (GenderFilter != "All")
                    q = q.Where(b => b.Gender!.ToUpper() == GenderFilter.ToUpper());

                var list = await q.OrderBy(b => b.Name).ToListAsync();
                Profiles = new ObservableCollection<Biodata>(list);
                _currentIndex = 0;
                CurrentProfile = Profiles.Count > 0 ? Profiles[0] : null;
            }
            finally { IsLoading = false; }
        }

        private void MoveNext()
        {
            if (Profiles.Count == 0) return;
            _currentIndex = (_currentIndex + 1) % Profiles.Count;
            CurrentProfile = Profiles[_currentIndex];
        }

        private void MovePrev()
        {
            if (Profiles.Count == 0) return;
            _currentIndex = (_currentIndex - 1 + Profiles.Count) % Profiles.Count;
            CurrentProfile = Profiles[_currentIndex];
        }

        private void TogglePlay()
        {
            IsPlaying = !IsPlaying;
        }

        private void LoadCurrentPhoto()
        {
            if (CurrentProfile?.HasPhoto == true)
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(CurrentProfile.PhotoPath!, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 500;
                    bmp.EndInit();
                    bmp.Freeze();
                    CurrentPhoto = bmp;
                    return;
                }
                catch { }
            }
            CurrentPhoto = null;
        }
    }
}
