using MarriageBureau.Data;
using MarriageBureau.Models;
using MarriageBureau.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace MarriageBureau.ViewModels
{
    public class DashboardViewModel : BaseViewModel
    {
        private int _totalProfiles;
        private int _maleProfiles;
        private int _femaleProfiles;
        private int _withPhotos;
        private int _withPdfs;
        private ObservableCollection<Biodata> _recentProfiles = new();
        private bool _isLoading;
        private string _businessName;

        public int TotalProfiles  { get => _totalProfiles;  set => SetProperty(ref _totalProfiles,  value); }
        public int MaleProfiles   { get => _maleProfiles;   set => SetProperty(ref _maleProfiles,   value); }
        public int FemaleProfiles { get => _femaleProfiles; set => SetProperty(ref _femaleProfiles, value); }
        public int WithPhotos     { get => _withPhotos;     set => SetProperty(ref _withPhotos,     value); }
        public int WithPdfs       { get => _withPdfs;       set => SetProperty(ref _withPdfs,       value); }
        public bool IsLoading     { get => _isLoading;      set => SetProperty(ref _isLoading,      value); }

        public string BusinessName { get => _businessName; set => SetProperty(ref _businessName, value); }

        public ObservableCollection<Biodata> RecentProfiles
        {
            get => _recentProfiles;
            set => SetProperty(ref _recentProfiles, value);
        }

        public ICommand RefreshCommand   { get; }
        public ICommand AddNewCommand    { get; }
        public ICommand BrowseCommand    { get; }
        public ICommand SlideshowCommand { get; }
        public ICommand ImportExcelCommand { get; }

        private readonly MainViewModel _mainVm;

        public DashboardViewModel(MainViewModel mainVm)
        {
            _mainVm            = mainVm;
            RefreshCommand     = new RelayCommand(async () => await LoadAsync());
            AddNewCommand      = new RelayCommand(() => _mainVm.Navigate(AppPage.AddEdit));
            BrowseCommand      = new RelayCommand(() => _mainVm.Navigate(AppPage.Browse));
            SlideshowCommand   = new RelayCommand(() => _mainVm.Navigate(AppPage.Slideshow));
            ImportExcelCommand = new RelayCommand(() => _mainVm.Navigate(AppPage.ExcelImport));
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                using var ctx = new AppDbContext();
                var all = await ctx.Biodatas.ToListAsync();
                TotalProfiles  = all.Count;
                MaleProfiles   = all.Count(p => p.Gender?.ToUpper() == "MALE");
                FemaleProfiles = all.Count(p => p.Gender?.ToUpper() == "FEMALE");
                WithPhotos     = all.Count(p => !string.IsNullOrWhiteSpace(p.PhotoPath));
                WithPdfs       = all.Count(p => !string.IsNullOrWhiteSpace(p.PdfPath));
                BusinessName = "Welcome to " + LicenceService.BusinessName;
                var recent = all.OrderByDescending(p => p.CreatedAt).Take(10).ToList();
                RecentProfiles = new ObservableCollection<Biodata>(recent);
            }
            finally { IsLoading = false; }
        }
    }
}
