using System.Windows.Controls;
using MarriageBureau.ViewModels;

namespace MarriageBureau.Views
{
    public partial class PhotoImportView : Page
    {
        public PhotoImportView(PhotoImportViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
