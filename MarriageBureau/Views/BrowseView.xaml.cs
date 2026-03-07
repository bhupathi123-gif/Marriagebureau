using System.Windows.Controls;
using System.Windows.Input;
using MarriageBureau.ViewModels;

namespace MarriageBureau.Views
{
    public partial class BrowseView : Page
    {
        public BrowseViewModel ViewModel { get; }
        private readonly MainViewModel _mainVm;

        public BrowseView(MainViewModel mainVm)
        {
            InitializeComponent();
            _mainVm   = mainVm;
            ViewModel = new BrowseViewModel(mainVm);
            DataContext = ViewModel;
        }

        private void ProfileGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel.SelectedProfile != null)
                _mainVm.Navigate(AppPage.AddEdit, ViewModel.SelectedProfile);
        }
    }
}
