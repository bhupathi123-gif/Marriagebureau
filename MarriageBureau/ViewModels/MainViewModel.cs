using System.Windows.Input;
using MarriageBureau.Views;

namespace MarriageBureau.ViewModels
{
    public enum AppPage
    {
        Dashboard,
        Browse,
        AddEdit,
        Slideshow
    }

    public class MainViewModel : BaseViewModel
    {
        private AppPage _currentPage = AppPage.Dashboard;
        private object? _currentView;
        private string _title = "Marriage Bureau - Management System";

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

        public bool IsDashboard => CurrentPage == AppPage.Dashboard;
        public bool IsBrowse => CurrentPage == AppPage.Browse;
        public bool IsAddEdit => CurrentPage == AppPage.AddEdit;
        public bool IsSlideshow => CurrentPage == AppPage.Slideshow;

        public ICommand NavigateDashboardCommand { get; }
        public ICommand NavigateBrowseCommand { get; }
        public ICommand NavigateAddCommand { get; }
        public ICommand NavigateSlideshowCommand { get; }

        private readonly MainWindow _mainWindow;

        public MainViewModel(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;

            NavigateDashboardCommand = new RelayCommand(() => Navigate(AppPage.Dashboard));
            NavigateBrowseCommand    = new RelayCommand(() => Navigate(AppPage.Browse));
            NavigateAddCommand       = new RelayCommand(() => Navigate(AppPage.AddEdit));
            NavigateSlideshowCommand = new RelayCommand(() => Navigate(AppPage.Slideshow));
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
            }
        }
    }
}
