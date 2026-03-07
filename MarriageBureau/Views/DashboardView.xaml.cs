using System.Windows.Controls;
using MarriageBureau.ViewModels;

namespace MarriageBureau.Views
{
    public partial class DashboardView : Page
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardView(MainViewModel mainVm)
        {
            InitializeComponent();
            ViewModel   = new DashboardViewModel(mainVm);
            DataContext = ViewModel;
        }
    }
}
