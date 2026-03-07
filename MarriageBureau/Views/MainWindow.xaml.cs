using System.Windows;
using System.Windows.Controls;
using MarriageBureau.Models;
using MarriageBureau.ViewModels;

namespace MarriageBureau.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel _vm;
        private BrowseView?    _browseView;
        private SlideshowView? _slideshowView;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel(this);
            DataContext = _vm;

            // Load dashboard on startup
            LoadDashboard();
        }

        public void LoadDashboard()
        {
            var view = new DashboardView(_vm);
            MainFrame.Content = view;
            _ = view.ViewModel.LoadAsync();
        }

        public void LoadBrowse()
        {
            _browseView = new BrowseView(_vm);
            MainFrame.Content = _browseView;
            _ = _browseView.ViewModel.LoadAsync();
        }

        public void LoadAddEdit(Biodata? biodata = null)
        {
            var view = new AddEditView(_vm, biodata);
            MainFrame.Content = view;
        }

        public void LoadSlideshow()
        {
            _slideshowView = new SlideshowView(_vm);
            MainFrame.Content = _slideshowView;
            _ = _slideshowView.ViewModel.LoadAsync();
        }
    }
}
