using System.Windows.Controls;
using MarriageBureau.Models;
using MarriageBureau.ViewModels;

namespace MarriageBureau.Views
{
    public partial class SettingsView : Page
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsView(MainViewModel mainVm, AppUser currentUser)
        {
            InitializeComponent();
            ViewModel   = new SettingsViewModel(mainVm, currentUser);
            DataContext = ViewModel;
        }

        private void NewPwdBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
            => ViewModel.NewPassword = ((PasswordBox)sender).Password;

        private void OldPwdBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
            => ViewModel.OldPassword = ((PasswordBox)sender).Password;

        private void NewPwd1Box_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
            => ViewModel.NewPwd1 = ((PasswordBox)sender).Password;

        private void NewPwd2Box_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
            => ViewModel.NewPwd2 = ((PasswordBox)sender).Password;
    }
}
