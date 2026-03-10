using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MBKeyGen
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Set default expiry to 1 year from today
            ExpiryDatePicker.SelectedDate = DateTime.Today.AddYears(1);
        }

        // ── Security Code Generation ──────────────────────────────────────────

        private void OnInputChanged(object sender, EventArgs e)
        {
            // Clear outputs when inputs change
            if (SecurityCodeOutputBox != null)
                SecurityCodeOutputBox.Text = string.Empty;
            if (EncryptedNameOutputBox != null)
                EncryptedNameOutputBox.Text = string.Empty;
        }

        private void QuickDate_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            var today = DateTime.Today;
            ExpiryDatePicker.SelectedDate = btn.Name switch
            {
                "Btn3M" => today.AddMonths(3),
                "Btn6M" => today.AddMonths(6),
                "Btn1Y" => today.AddYears(1),
                "Btn2Y" => today.AddYears(2),
                _       => today.AddYears(1)
            };
        }

        private void GenerateCode_Click(object sender, RoutedEventArgs e)
        {
            var bizName = BusinessNameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(bizName))
            {
                ShowCodeStatus("Please enter a business name.", false);
                return;
            }

            if (ExpiryDatePicker.SelectedDate == null)
            {
                ShowCodeStatus("Please select an expiry date.", false);
                return;
            }

            var expiry = ExpiryDatePicker.SelectedDate.Value;
            if (expiry <= DateTime.Today)
            {
                ShowCodeStatus("Expiry date must be in the future.", false);
                return;
            }

            try
            {
                var code = CryptoService.GenerateSecurityCode(bizName, expiry);
                var encName = CryptoService.EncryptBusinessName(bizName);

                SecurityCodeOutputBox.Text  = code;
                EncryptedNameOutputBox.Text = encName;

                var days = (expiry - DateTime.Today).Days;
                ShowCodeStatus(
                    $"✔ Code generated successfully!\n" +
                    $"Business: {bizName}\n" +
                    $"Expires: {expiry:dd-MMM-yyyy}  ({days} days)",
                    true);
            }
            catch (Exception ex)
            {
                ShowCodeStatus($"Error: {ex.Message}", false);
            }
        }

        private void ShowCodeStatus(string msg, bool isSuccess)
        {
            CodeStatusText.Text       = msg;
            CodeStatusText.Foreground = new SolidColorBrush(
                isSuccess ? Color.FromRgb(0x2E, 0x7D, 0x32)   // green
                          : Color.FromRgb(0xB7, 0x1C, 0x1C));  // red
            CodeStatusBorder.Background = new SolidColorBrush(
                isSuccess ? Color.FromRgb(0xE8, 0xF5, 0xE9)
                          : Color.FromRgb(0xFF, 0xEB, 0xEE));
        }

        private void CopySecurityCode_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(SecurityCodeOutputBox.Text))
            {
                Clipboard.SetText(SecurityCodeOutputBox.Text);
                ShowCodeStatus("Security code copied to clipboard!", true);
            }
        }

        private void CopyEncryptedName_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(EncryptedNameOutputBox.Text))
            {
                Clipboard.SetText(EncryptedNameOutputBox.Text);
                ShowCodeStatus("Encrypted business name copied to clipboard!", true);
            }
        }

        // ── Validate Code ─────────────────────────────────────────────────────

        private void ValidateCode_Changed(object sender, TextChangedEventArgs e)
        {
            // Reset result panel
            if (ValidateResultPanel != null)
            {
                ValidateResultPanel.Visibility      = Visibility.Collapsed;
                ValidatePlaceholderText.Visibility  = Visibility.Visible;
            }
        }

        private void ValidateCode_Click(object sender, RoutedEventArgs e)
        {
            var code = ValidateCodeBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                ValidatePlaceholderText.Text       = "Please paste a security code first.";
                ValidatePlaceholderText.Foreground = new SolidColorBrush(Colors.Crimson);
                return;
            }

            var (isValid, bizName, expiry, message) = CryptoService.ValidateSecurityCode(code);

            ValidateResultPanel.Visibility     = Visibility.Visible;
            ValidatePlaceholderText.Visibility = Visibility.Collapsed;

            ValBizNameText.Text  = string.IsNullOrWhiteSpace(bizName) ? "(not decoded)" : bizName;
            ValExpiryText.Text   = expiry == DateTime.MinValue ? "(not decoded)" : expiry.ToString("dd-MMM-yyyy");
            ValMessageText.Text  = message;

            if (isValid)
            {
                ValStatusText.Text       = "✔ VALID";
                ValStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
                ValidateBorder.Background= new SolidColorBrush(Color.FromRgb(0xE8, 0xF5, 0xE9));
            }
            else
            {
                ValStatusText.Text       = "✗ INVALID / EXPIRED";
                ValStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xB7, 0x1C, 0x1C));
                ValidateBorder.Background= new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0xEE));
            }
        }

        // ── Password Hash ─────────────────────────────────────────────────────

        private void HashPassword_Click(object sender, RoutedEventArgs e)
        {
            var pwd = PwdBox.Password;
            if (string.IsNullOrWhiteSpace(pwd))
            {
                HashStatusText.Text       = "Please enter a password.";
                HashStatusText.Foreground = new SolidColorBrush(Colors.Crimson);
                return;
            }

            try
            {
                var hash = CryptoService.HashPassword(pwd);
                HashOutputBox.Text  = hash;
                HashStatusText.Text = "✔ Hash generated. Copy the HASH:SALT string to the database.";
                HashStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
            }
            catch (Exception ex)
            {
                HashStatusText.Text       = $"Error: {ex.Message}";
                HashStatusText.Foreground = new SolidColorBrush(Colors.Crimson);
            }
        }

        private void CopyHash_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(HashOutputBox.Text))
            {
                Clipboard.SetText(HashOutputBox.Text);
                HashStatusText.Text = "Hash copied to clipboard!";
            }
        }

        // ── Encrypt / Decrypt ─────────────────────────────────────────────────

        private void Encrypt_Click(object sender, RoutedEventArgs e)
        {
            var input = EncDecInputBox.Text;
            if (string.IsNullOrWhiteSpace(input)) { EncDecOutputBox.Text = ""; return; }
            try { EncDecOutputBox.Text = CryptoService.Encrypt(input); }
            catch (Exception ex) { EncDecOutputBox.Text = $"Error: {ex.Message}"; }
        }

        private void Decrypt_Click(object sender, RoutedEventArgs e)
        {
            var input = EncDecInputBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(input)) { EncDecOutputBox.Text = ""; return; }
            try
            {
                var result = CryptoService.Decrypt(input);
                EncDecOutputBox.Text = string.IsNullOrWhiteSpace(result)
                    ? "(Decryption failed – wrong key or corrupt data)"
                    : result;
            }
            catch (Exception ex) { EncDecOutputBox.Text = $"Error: {ex.Message}"; }
        }

        private void CopyEncDec_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(EncDecOutputBox.Text))
                Clipboard.SetText(EncDecOutputBox.Text);
        }
    }
}
