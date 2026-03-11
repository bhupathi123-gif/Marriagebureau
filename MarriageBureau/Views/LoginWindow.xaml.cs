using System.Windows;
using MarriageBureau.Models;
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

            // LicenceService is already validated in App.xaml.cs – just load state
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
            // This event is raised from the UI thread (see LoginViewModel)
            LoggedInUser = user;
            DialogResult = true;   // closes the dialog and returns true to ShowDialog()
            Close();
        }
    }
}
