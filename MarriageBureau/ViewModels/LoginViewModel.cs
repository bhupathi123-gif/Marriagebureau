using System.Windows;
using System.Windows.Input;
using MarriageBureau.Data;
using MarriageBureau.Models;
using MarriageBureau.Services;

namespace MarriageBureau.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private string _username      = string.Empty;
        private string _password      = string.Empty;
        private string _errorMessage  = string.Empty;
        private bool   _isLoggingIn;
        private bool   _isLicenceExpired;
        private string _licenceMessage = string.Empty;
        private string _businessName   = string.Empty;

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        // Password is set from code-behind (PasswordBox cannot bind directly)
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                SetProperty(ref _errorMessage, value);
                OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public bool IsLoggingIn
        {
            get => _isLoggingIn;
            set
            {
                SetProperty(ref _isLoggingIn, value);
                // Refresh command can-execute on the UI thread
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsLicenceExpired
        {
            get => _isLicenceExpired;
            set
            {
                SetProperty(ref _isLicenceExpired, value);
                OnPropertyChanged(nameof(HasLicenceWarning));
            }
        }

        public string LicenceMessage
        {
            get => _licenceMessage;
            set
            {
                SetProperty(ref _licenceMessage, value);
                OnPropertyChanged(nameof(HasLicenceWarning));
            }
        }

        /// <summary>True when the licence is valid but there is still a message to display (e.g. expiry warning).</summary>
        public bool HasLicenceWarning => !IsLicenceExpired && !string.IsNullOrWhiteSpace(LicenceMessage);

        public string BusinessName
        {
            get => _businessName;
            set => SetProperty(ref _businessName, value);
        }

        public ICommand LoginCommand { get; }

        // Raised on the UI thread after successful authentication
        public event EventHandler<AppUser>? LoginSucceeded;

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(
                async () => await LoginAsync(),
                () => !IsLoggingIn);
        }

        public void LoadLicence()
        {
            BusinessName = LicenceService.BusinessName;
            IsLicenceExpired = !LicenceService.IsValid;
            LicenceMessage   = LicenceService.Message;
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

            AppUser? loggedInUser = null;
            string?  errorMsg    = null;

            try
            {
                // Run DB work on a thread-pool thread; capture results in locals
                string capturedUsername = Username.Trim();
                string capturedPassword = Password;

                await Task.Run(() =>
                {
                    try
                    {
                        using var ctx = new AppDbContext();

                        var user = ctx.AppUsers
                                      .FirstOrDefault(u =>
                                          u.Username == capturedUsername &&
                                          u.IsActive);

                        if (user == null)
                        {
                            errorMsg = "Invalid username or password.";
                            return;
                        }

                        if (!CryptoService.VerifyPassword(capturedPassword, user.PasswordHash))
                        {
                            errorMsg = "Invalid username or password.";
                            return;
                        }

                        // Update last-login timestamp
                        user.LastLogin = DateTime.Now;
                        ctx.SaveChanges();

                        loggedInUser = user;
                    }
                    catch (Exception ex)
                    {
                        errorMsg = $"Login failed: {ex.Message}";
                    }
                });

                // Back on UI thread — safe to touch bound properties
                if (errorMsg != null)
                {
                    ErrorMessage = errorMsg;
                    return;
                }

                if (loggedInUser != null)
                    LoginSucceeded?.Invoke(this, loggedInUser);
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
    }
}
