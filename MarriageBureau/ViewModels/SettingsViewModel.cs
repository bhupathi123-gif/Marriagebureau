using System.Collections.ObjectModel;
using System.Windows.Input;
using MarriageBureau.Data;
using MarriageBureau.Models;
using MarriageBureau.Services;
using Microsoft.EntityFrameworkCore;

namespace MarriageBureau.ViewModels
{
    /// <summary>
    /// Settings page: business name, security code, user management.
    /// Only accessible to Admin role users.
    /// </summary>
    public class SettingsViewModel : BaseViewModel
    {
        private readonly MainViewModel _mainVm;

        // ── Business Name ────────────────────────────────────────────
        private string _businessName    = string.Empty;
        private string _settingsStatus  = string.Empty;
        private bool   _isSavingSettings;

        // ── Security Code ─────────────────────────────────────────────
        private string _securityCode    = string.Empty;
        private string _codeStatus      = string.Empty;
        private bool   _codeValid;

        // ── Users ─────────────────────────────────────────────────────
        private ObservableCollection<AppUser> _users = new();
        private AppUser? _selectedUser;
        private string _newUsername   = string.Empty;
        private string _newPassword   = string.Empty;
        private string _newFullName   = string.Empty;
        private string _newRole       = "User";
        private string _userStatus    = string.Empty;
        private bool   _isSavingUser;

        // ── Change Own Password ────────────────────────────────────────
        private string _oldPassword    = string.Empty;
        private string _newPwd1        = string.Empty;
        private string _newPwd2        = string.Empty;
        private string _pwdStatus      = string.Empty;

        // ────────────────────────────────────────────────────────────────

        public AppUser CurrentUser { get; }
        public bool    IsAdmin => CurrentUser.Role == "Admin";

        // ── Business Name Properties ─────────────────────────────────

        public string BusinessName
        {
            get => _businessName;
            set => SetProperty(ref _businessName, value);
        }

        public string SettingsStatus
        {
            get => _settingsStatus;
            set => SetProperty(ref _settingsStatus, value);
        }

        public bool IsSavingSettings
        {
            get => _isSavingSettings;
            set => SetProperty(ref _isSavingSettings, value);
        }

        // ── Security Code ─────────────────────────────────────────────

        public string SecurityCode
        {
            get => _securityCode;
            set { SetProperty(ref _securityCode, value); ValidateCode(); }
        }

        public string CodeStatus
        {
            get => _codeStatus;
            set => SetProperty(ref _codeStatus, value);
        }

        public bool CodeValid
        {
            get => _codeValid;
            set => SetProperty(ref _codeValid, value);
        }

        // ── User Management ──────────────────────────────────────────

        public ObservableCollection<AppUser> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
        }

        public AppUser? SelectedUser
        {
            get => _selectedUser;
            set
            {
                SetProperty(ref _selectedUser, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string NewUsername  { get => _newUsername;  set => SetProperty(ref _newUsername,  value); }
        public string NewPassword  { get => _newPassword;  set => SetProperty(ref _newPassword,  value); }
        public string NewFullName  { get => _newFullName;  set => SetProperty(ref _newFullName,  value); }
        public string NewRole      { get => _newRole;      set => SetProperty(ref _newRole,      value); }

        public string UserStatus
        {
            get => _userStatus;
            set => SetProperty(ref _userStatus, value);
        }

        public bool IsSavingUser
        {
            get => _isSavingUser;
            set => SetProperty(ref _isSavingUser, value);
        }

        // ── Change Password ───────────────────────────────────────────
        public string OldPassword  { get => _oldPassword;  set => SetProperty(ref _oldPassword,  value); }
        public string NewPwd1      { get => _newPwd1;      set => SetProperty(ref _newPwd1,      value); }
        public string NewPwd2      { get => _newPwd2;      set => SetProperty(ref _newPwd2,      value); }
        public string PwdStatus    { get => _pwdStatus;    set => SetProperty(ref _pwdStatus,    value); }

        public List<string> RoleOptions { get; } = new() { "Admin", "User" };

        // ── Commands ──────────────────────────────────────────────────
        public ICommand SaveBusinessNameCommand  { get; }
        public ICommand ApplySecurityCodeCommand { get; }
        public ICommand AddUserCommand           { get; }
        public ICommand ToggleUserActiveCommand  { get; }
        public ICommand DeleteUserCommand        { get; }
        public ICommand ChangePasswordCommand    { get; }
        public ICommand CancelCommand            { get; }
        public ICommand RefreshUsersCommand      { get; }

        // ── Constructor ───────────────────────────────────────────────

        public SettingsViewModel(MainViewModel mainVm, AppUser currentUser)
        {
            _mainVm     = mainVm;
            CurrentUser = currentUser;

            SaveBusinessNameCommand  = new RelayCommand(async () => await SaveBusinessNameAsync(),
                                                         () => !IsSavingSettings);
            ApplySecurityCodeCommand = new RelayCommand(async () => await ApplySecurityCodeAsync(),
                                                         () => !IsSavingSettings);
            AddUserCommand           = new RelayCommand(async () => await AddUserAsync(),
                                                         () => IsAdmin && !IsSavingUser);
            ToggleUserActiveCommand  = new RelayCommand(async () => await ToggleUserActiveAsync(),
                                                         () => IsAdmin && SelectedUser != null);
            DeleteUserCommand        = new RelayCommand(async () => await DeleteUserAsync(),
                                                         () => IsAdmin && SelectedUser != null
                                                              && SelectedUser.Username != "admin");
            ChangePasswordCommand    = new RelayCommand(async () => await ChangePasswordAsync());
            CancelCommand            = new RelayCommand(() => _mainVm.Navigate(AppPage.Dashboard));
            RefreshUsersCommand      = new RelayCommand(async () => await LoadUsersAsync());

            _ = LoadAsync();
        }

        // ── Load ──────────────────────────────────────────────────────

        public async Task LoadAsync()
        {
            using var ctx = new AppDbContext();
            var s = await ctx.AppSettings.FindAsync(1);
            if (s != null)
            {
                BusinessName = !string.IsNullOrWhiteSpace(s.EncryptedBusinessName)
                    ? CryptoService.Decrypt(s.EncryptedBusinessName)
                    : string.Empty;
                SecurityCode = s.SecurityCode ?? string.Empty;
            }

            await LoadUsersAsync();
        }

        private async Task LoadUsersAsync()
        {
            using var ctx = new AppDbContext();
            var list = await ctx.AppUsers.OrderBy(u => u.Username).ToListAsync();
            Users = new ObservableCollection<AppUser>(list);
        }

        // ── Business Name Save ────────────────────────────────────────

        private async Task SaveBusinessNameAsync()
        {
            IsSavingSettings = true;
            SettingsStatus   = string.Empty;
            try
            {
                var encrypted = CryptoService.Encrypt(BusinessName.Trim());
                using var ctx = new AppDbContext();
                var s = await ctx.AppSettings.FindAsync(1);
                if (s == null) { s = new AppSettings { Id = 1 }; ctx.AppSettings.Add(s); }
                s.EncryptedBusinessName = encrypted;
                await ctx.SaveChangesAsync();
                LicenceService.Reload();
                SettingsStatus = "Business name saved and encrypted successfully.";
            }
            catch (Exception ex) { SettingsStatus = $"Error: {ex.Message}"; }
            finally { IsSavingSettings = false; }
        }

        // ── Security Code Apply ───────────────────────────────────────

        private void ValidateCode()
        {
            if (string.IsNullOrWhiteSpace(SecurityCode)) { CodeStatus = ""; CodeValid = false; return; }
            var (valid, biz, expiry, msg) = CryptoService.ValidateSecurityCode(SecurityCode.Trim());
            CodeValid  = valid;
            CodeStatus = msg;
        }

        private async Task ApplySecurityCodeAsync()
        {
            IsSavingSettings = true;
            try
            {
                var (valid, biz, expiry, msg) = CryptoService.ValidateSecurityCode(SecurityCode.Trim());
                if (!valid) { CodeStatus = msg; return; }

                using var ctx = new AppDbContext();
                var s = await ctx.AppSettings.FindAsync(1);
                if (s == null) { s = new AppSettings { Id = 1 }; ctx.AppSettings.Add(s); }
                s.SecurityCode = SecurityCode.Trim();
                // Also update business name from the code
                s.EncryptedBusinessName = CryptoService.Encrypt(biz);
                await ctx.SaveChangesAsync();
                BusinessName = biz;
                LicenceService.Reload();
                CodeStatus = $"Security code applied. {msg}";
            }
            catch (Exception ex) { CodeStatus = $"Error: {ex.Message}"; }
            finally { IsSavingSettings = false; }
        }

        // ── User Management ───────────────────────────────────────────

        private async Task AddUserAsync()
        {
            if (string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(NewPassword))
            {
                UserStatus = "Username and password are required.";
                return;
            }

            IsSavingUser = true;
            UserStatus   = string.Empty;
            try
            {
                using var ctx = new AppDbContext();
                if (ctx.AppUsers.Any(u => u.Username == NewUsername.Trim()))
                {
                    UserStatus = "Username already exists.";
                    return;
                }
                ctx.AppUsers.Add(new AppUser
                {
                    Username     = NewUsername.Trim(),
                    PasswordHash = CryptoService.HashPassword(NewPassword),
                    FullName     = NewFullName.Trim(),
                    Role         = NewRole,
                    IsActive     = true
                });
                await ctx.SaveChangesAsync();
                UserStatus   = $"User '{NewUsername.Trim()}' created successfully.";
                NewUsername  = NewPassword = NewFullName = string.Empty;
                await LoadUsersAsync();
            }
            catch (Exception ex) { UserStatus = $"Error: {ex.Message}"; }
            finally { IsSavingUser = false; }
        }

        private async Task ToggleUserActiveAsync()
        {
            if (SelectedUser == null) return;
            using var ctx = new AppDbContext();
            var u = await ctx.AppUsers.FindAsync(SelectedUser.Id);
            if (u == null) return;
            u.IsActive = !u.IsActive;
            await ctx.SaveChangesAsync();
            await LoadUsersAsync();
        }

        private async Task DeleteUserAsync()
        {
            if (SelectedUser == null) return;
            var res = System.Windows.MessageBox.Show(
                $"Delete user '{SelectedUser.Username}'?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            if (res != System.Windows.MessageBoxResult.Yes) return;

            using var ctx = new AppDbContext();
            var u = await ctx.AppUsers.FindAsync(SelectedUser.Id);
            if (u != null) { ctx.AppUsers.Remove(u); await ctx.SaveChangesAsync(); }
            await LoadUsersAsync();
        }

        private async Task ChangePasswordAsync()
        {
            if (string.IsNullOrWhiteSpace(OldPassword) ||
                string.IsNullOrWhiteSpace(NewPwd1)     ||
                string.IsNullOrWhiteSpace(NewPwd2))
            {
                PwdStatus = "All password fields are required.";
                return;
            }
            if (NewPwd1 != NewPwd2)
            {
                PwdStatus = "New passwords do not match.";
                return;
            }
            if (NewPwd1.Length < 6)
            {
                PwdStatus = "Password must be at least 6 characters.";
                return;
            }

            try
            {
                using var ctx = new AppDbContext();
                var u = await ctx.AppUsers.FindAsync(CurrentUser.Id);
                if (u == null || !CryptoService.VerifyPassword(OldPassword, u.PasswordHash))
                {
                    PwdStatus = "Current password is incorrect.";
                    return;
                }
                u.PasswordHash = CryptoService.HashPassword(NewPwd1);
                await ctx.SaveChangesAsync();
                PwdStatus = "Password changed successfully.";
                OldPassword = NewPwd1 = NewPwd2 = string.Empty;
            }
            catch (Exception ex) { PwdStatus = $"Error: {ex.Message}"; }
        }
    }
}
