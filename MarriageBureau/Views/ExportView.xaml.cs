using System.Windows.Controls;
using MarriageBureau.Models;
using MarriageBureau.ViewModels;

namespace MarriageBureau.Views
{
    public partial class ExportView : Page
    {
        public ExportViewModel ViewModel { get; }

        public ExportView(MainViewModel mainVm, Biodata? preSelected = null)
        {
            InitializeComponent();
            ViewModel   = new ExportViewModel(mainVm, preSelected);
            DataContext = ViewModel;
        }
    }
}
