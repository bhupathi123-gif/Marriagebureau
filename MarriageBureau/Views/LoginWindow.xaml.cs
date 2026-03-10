using System.Windows;
using MarriageBureau.Models;
using MarriageBureau.Services;
using MarriageBureau.ViewModels;

namespace MarriageBureau.Views
{
    public partial class LoginWindow : Window
    {
        public AppUser? LoggedInUser { get; private set; }

        private readonly LoginViewModel _vm;

        public LoginWindow()
        {
            InitializeComponent();

            // Validate licence before showing login
            LicenceService.Validate();

            _vm = new LoginViewModel();
            _vm.LoadLicence();
            _vm.LoginSucceeded += OnLoginSucceeded;
            DataContext = _vm;

            Loaded += (_, _) => UsernameBox.Focus();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _vm.Password = PasswordBox.Password;
        }

        private void OnLoginSucceeded(object? sender, AppUser user)
        {
            LoggedInUser = user;
            DialogResult = true;
            Close();
        }
    }
}
