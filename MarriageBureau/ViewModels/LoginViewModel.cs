using System.Windows.Input;
using MarriageBureau.Data;
using MarriageBureau.Models;
using MarriageBureau.Services;
using Microsoft.EntityFrameworkCore;

namespace MarriageBureau.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private string _username   = string.Empty;
        private string _password   = string.Empty;
        private string _errorMessage = string.Empty;
        private bool   _isLoggingIn;
        private bool   _isLicenceExpired;
        private string _licenceMessage = string.Empty;
        private string _businessName   = string.Empty;

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        // Password is bound via code-behind (PasswordBox.Password can't bind directly)
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { SetProperty(ref _errorMessage, value); OnPropertyChanged(nameof(HasError)); }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public bool IsLoggingIn
        {
            get => _isLoggingIn;
            set => SetProperty(ref _isLoggingIn, value);
        }

        public bool IsLicenceExpired
        {
            get => _isLicenceExpired;
            set => SetProperty(ref _isLicenceExpired, value);
        }

        public string LicenceMessage
        {
            get => _licenceMessage;
            set => SetProperty(ref _licenceMessage, value);
        }

        public string BusinessName
        {
            get => _businessName;
            set => SetProperty(ref _businessName, value);
        }

        // Set externally from the Window after successful login
        public AppUser? LoggedInUser { get; private set; }

        public ICommand LoginCommand { get; }

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(async () => await LoginAsync(),
                                            () => !IsLoggingIn);
        }

        public void LoadLicence()
        {
            BusinessName = LicenceService.BusinessName;
            if (!LicenceService.IsValid)
            {
                IsLicenceExpired = true;
                LicenceMessage   = LicenceService.Message;
            }
            else
            {
                IsLicenceExpired = false;
                LicenceMessage   = LicenceService.Message;
            }
        }

        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                ErrorMessage = "Please enter your username.";
                return;
            }
            if (string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter your password.";
                return;
            }

            IsLoggingIn  = true;
            ErrorMessage = string.Empty;

            try
            {
                await Task.Run(() =>
                {
                    using var ctx = new AppDbContext();
                    var user = ctx.AppUsers
                                  .AsNoTracking()
                                  .FirstOrDefault(u =>
                                      u.Username == Username.Trim() &&
                                      u.IsActive);

                    if (user == null)
                    {
                        ErrorMessage = "Invalid username or password.";
                        return;
                    }

                    if (!CryptoService.VerifyPassword(Password, user.PasswordHash))
                    {
                        ErrorMessage = "Invalid username or password.";
                        return;
                    }

                    // Update last login timestamp
                    var tracked = ctx.AppUsers.Find(user.Id)!;
                    tracked.LastLogin = DateTime.Now;
                    ctx.SaveChanges();

                    LoggedInUser = user;
                });

                if (LoggedInUser != null)
                    LoginSucceeded?.Invoke(this, LoggedInUser);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Login failed: {ex.Message}";
            }
            finally
            {
                IsLoggingIn = false;
            }
        }

        /// <summary>Raised when the user has been authenticated successfully.</summary>
        public event EventHandler<AppUser>? LoginSucceeded;
    }
}
