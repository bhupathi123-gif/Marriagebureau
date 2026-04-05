using System.Windows.Input;
using MarriageBureau.Models;
using MarriageBureau.Views;

namespace MarriageBureau.ViewModels
{
    public enum AppPage
    {
        Dashboard,
        Browse,
        AddEdit,
        Slideshow,
        ExcelImport,
        Export,
        Settings,
        PhotoImport
    }

    public class MainViewModel : BaseViewModel
    {
        private AppPage _currentPage = AppPage.Dashboard;
        private object? _currentView;
        private string _title = "Marriage Bureau - Management System";

        public AppUser CurrentUser { get; }
        public bool IsAdmin => CurrentUser.Role == "Admin";
        public AppPage CurrentPage
        {
            get => _currentPage;
            set
            {
                SetProperty(ref _currentPage, value);
                OnPropertyChanged(nameof(IsDashboard));
                OnPropertyChanged(nameof(IsBrowse));
                OnPropertyChanged(nameof(IsAddEdit));
                OnPropertyChanged(nameof(IsSlideshow));
                OnPropertyChanged(nameof(IsExcelImport));
                OnPropertyChanged(nameof(IsExport));
                OnPropertyChanged(nameof(IsSettings));
                OnPropertyChanged(nameof(IsPhotoImport));
            }
        }

        public object? CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public bool IsDashboard   => CurrentPage == AppPage.Dashboard;
        public bool IsBrowse      => CurrentPage == AppPage.Browse;
        public bool IsAddEdit     => CurrentPage == AppPage.AddEdit;
        public bool IsSlideshow   => CurrentPage == AppPage.Slideshow;
        public bool IsExcelImport => CurrentPage == AppPage.ExcelImport;
        public bool IsExport      => CurrentPage == AppPage.Export;
        public bool IsSettings    => CurrentPage == AppPage.Settings;
        public bool IsPhotoImport => CurrentPage == AppPage.PhotoImport;

        public ICommand NavigateDashboardCommand    { get; }
        public ICommand NavigateBrowseCommand      { get; }
        public ICommand NavigateAddCommand         { get; }
        public ICommand NavigateSlideshowCommand   { get; }
        public ICommand NavigateExcelImportCommand { get; }
        public ICommand NavigateExportCommand      { get; }
        public ICommand NavigateSettingsCommand    { get; }
        public ICommand NavigatePhotoImportCommand { get; }

        private readonly MainWindow _mainWindow;

        public MainViewModel(MainWindow mainWindow, AppUser currentUser)
        {
            _mainWindow  = mainWindow;
            CurrentUser  = currentUser;

            NavigateDashboardCommand    = new RelayCommand(() => Navigate(AppPage.Dashboard));
            NavigateBrowseCommand      = new RelayCommand(() => Navigate(AppPage.Browse));
            NavigateAddCommand         = new RelayCommand(() => Navigate(AppPage.AddEdit));
            NavigateSlideshowCommand   = new RelayCommand(() => Navigate(AppPage.Slideshow));
            NavigateExcelImportCommand = new RelayCommand(() => Navigate(AppPage.ExcelImport));
            NavigateExportCommand      = new RelayCommand(() => Navigate(AppPage.Export));
            NavigateSettingsCommand    = new RelayCommand(() => Navigate(AppPage.Settings));
            NavigatePhotoImportCommand = new RelayCommand(() => Navigate(AppPage.PhotoImport));
        }

        public void Navigate(AppPage page, object? parameter = null)
        {
            CurrentPage = page;
            switch (page)
            {
                case AppPage.Dashboard:
                    _mainWindow.LoadDashboard();
                    break;
                case AppPage.Browse:
                    _mainWindow.LoadBrowse();
                    break;
                case AppPage.AddEdit:
                    _mainWindow.LoadAddEdit(parameter as Models.Biodata);
                    break;
                case AppPage.Slideshow:
                    _mainWindow.LoadSlideshow();
                    break;
                case AppPage.ExcelImport:
                    _mainWindow.LoadExcelImport();
                    break;
                case AppPage.Export:
                    _mainWindow.LoadExport(parameter as Models.Biodata);
                    break;
                case AppPage.Settings:
                    _mainWindow.LoadSettings();
                    break;
                case AppPage.PhotoImport:
                    _mainWindow.LoadPhotoImport();
                    break;
            }
        }
    }
}
