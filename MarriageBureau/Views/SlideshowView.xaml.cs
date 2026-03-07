using System.Windows.Controls;
using MarriageBureau.ViewModels;

namespace MarriageBureau.Views
{
    public partial class SlideshowView : Page
    {
        public SlideshowViewModel ViewModel { get; }

        public SlideshowView(MainViewModel mainVm)
        {
            InitializeComponent();
            ViewModel   = new SlideshowViewModel(mainVm);
            DataContext = ViewModel;
        }
    }
}
