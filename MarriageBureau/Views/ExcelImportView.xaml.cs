using System.Windows.Controls;
using MarriageBureau.ViewModels;

namespace MarriageBureau.Views
{
    public partial class ExcelImportView : Page
    {
        public ExcelImportViewModel ViewModel { get; }

        public ExcelImportView(MainViewModel mainVm)
        {
            InitializeComponent();
            ViewModel   = new ExcelImportViewModel(mainVm);
            DataContext = ViewModel;
        }
    }
}
