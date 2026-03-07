using System.Windows.Controls;
using MarriageBureau.Models;
using MarriageBureau.ViewModels;

namespace MarriageBureau.Views
{
    public partial class AddEditView : Page
    {
        public AddEditViewModel ViewModel { get; }

        public AddEditView(MainViewModel mainVm, Biodata? biodata = null)
        {
            InitializeComponent();
            ViewModel   = new AddEditViewModel(mainVm, biodata);
            DataContext = ViewModel;
        }
    }
}
