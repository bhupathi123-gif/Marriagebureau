using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MarriageBureau.Data;
using MarriageBureau.Models;

namespace MarriageBureau.ViewModels
{
    public class AddEditViewModel : BaseViewModel
    {
        private Biodata _biodata;
        private BitmapImage? _photoPreview;
        private bool _isSaving;
        private bool _isEditMode;
        private string _statusMessage = string.Empty;

        private readonly MainViewModel _mainVm;

        // ─── Bound to Form ────────────────────────────────────────────────

        public Biodata Biodata
        {
            get => _biodata;
            set => SetProperty(ref _biodata, value);
        }

        public BitmapImage? PhotoPreview
        {
            get => _photoPreview;
            set => SetProperty(ref _photoPreview, value);
        }

        public bool IsSaving
        {
            get => _isSaving;
            set => SetProperty(ref _isSaving, value);
        }

        public bool IsEditMode
        {
            get => _isEditMode;
            set { SetProperty(ref _isEditMode, value); OnPropertyChanged(nameof(WindowTitle)); }
        }

        public string WindowTitle => IsEditMode ? "Edit Biodata" : "Add New Biodata";

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string? PdfFileName => Path.GetFileName(Biodata.PdfPath);
        public bool HasPdf => Biodata.HasPdf;

        public List<string> GenderOptions { get; } = new() { "MALE", "FEMALE" };
        public List<string> ComplexionOptions { get; } = new() { "FAIR", "VERY FAIR", "WHITE", "MEDIUM", "RED", "DARK", "WHEATISH" };
        public List<string> RasiOptions { get; } = new() { "MESHA", "VRUSHABA", "MIDHUNA", "KARKATAKA", "SIMHA", "KANYA", "TULA", "VRUCHIKA", "DHANU", "MAKARA", "KUMBHA", "MEENA" };

        // ─── Commands ─────────────────────────────────────────────────────

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand UploadPhotoCommand { get; }
        public ICommand RemovePhotoCommand { get; }
        public ICommand UploadPdfCommand { get; }
        public ICommand RemovePdfCommand { get; }
        public ICommand ViewPdfCommand { get; }

        public AddEditViewModel(MainViewModel mainVm, Biodata? existingBiodata = null)
        {
            _mainVm  = mainVm;
            _biodata = existingBiodata != null
                ? CopyBiodata(existingBiodata)
                : new Biodata();

            IsEditMode = existingBiodata != null;
            LoadPhotoPreview();

            SaveCommand        = new RelayCommand(async () => await SaveAsync(), () => !IsSaving);
            CancelCommand      = new RelayCommand(() => _mainVm.Navigate(AppPage.Browse));
            UploadPhotoCommand = new RelayCommand(UploadPhoto);
            RemovePhotoCommand = new RelayCommand(RemovePhoto, () => !string.IsNullOrWhiteSpace(Biodata.PhotoPath));
            UploadPdfCommand   = new RelayCommand(UploadPdf);
            RemovePdfCommand   = new RelayCommand(RemovePdf, () => !string.IsNullOrWhiteSpace(Biodata.PdfPath));
            ViewPdfCommand     = new RelayCommand(OpenPdf, () => Biodata.HasPdf);
        }

        // ─── Photo ────────────────────────────────────────────────────────

        private void UploadPhoto()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Select Profile Photo",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp",
                Multiselect = false
            };
            if (dlg.ShowDialog() != true) return;

            // Copy to app data folder
            var destDir = GetFilesDirectory("Photos");
            var destFile = Path.Combine(destDir, $"{Guid.NewGuid()}{Path.GetExtension(dlg.FileName)}");
            File.Copy(dlg.FileName, destFile, overwrite: true);

            Biodata.PhotoPath = destFile;
            OnPropertyChanged(nameof(Biodata));
            LoadPhotoPreview();
        }

        private void RemovePhoto()
        {
            Biodata.PhotoPath = null;
            PhotoPreview = null;
            OnPropertyChanged(nameof(Biodata));
        }

        private void LoadPhotoPreview()
        {
            if (!string.IsNullOrWhiteSpace(Biodata.PhotoPath) && File.Exists(Biodata.PhotoPath))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(Biodata.PhotoPath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 300;
                    bmp.EndInit();
                    bmp.Freeze();
                    PhotoPreview = bmp;
                    return;
                }
                catch { }
            }
            PhotoPreview = null;
        }

        // ─── PDF ──────────────────────────────────────────────────────────

        private void UploadPdf()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Select Biodata PDF",
                Filter = "PDF Files|*.pdf",
                Multiselect = false
            };
            if (dlg.ShowDialog() != true) return;

            var destDir  = GetFilesDirectory("PDFs");
            var destFile = Path.Combine(destDir, $"{Guid.NewGuid()}.pdf");
            File.Copy(dlg.FileName, destFile, overwrite: true);

            Biodata.PdfPath = destFile;
            OnPropertyChanged(nameof(Biodata));
            OnPropertyChanged(nameof(PdfFileName));
            OnPropertyChanged(nameof(HasPdf));
        }

        private void RemovePdf()
        {
            Biodata.PdfPath = null;
            OnPropertyChanged(nameof(Biodata));
            OnPropertyChanged(nameof(PdfFileName));
            OnPropertyChanged(nameof(HasPdf));
        }

        private void OpenPdf()
        {
            if (Biodata.HasPdf)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Biodata.PdfPath!)
                    { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Cannot open PDF: {ex.Message}");
                }
            }
        }

        // ─── Save ─────────────────────────────────────────────────────────

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Biodata.Name))
            {
                StatusMessage = "Name is required.";
                return;
            }

            IsSaving = true;
            StatusMessage = "Saving...";
            try
            {
                using var ctx = new AppDbContext();
                Biodata.UpdatedAt = DateTime.Now;

                if (IsEditMode)
                {
                    ctx.Biodatas.Update(Biodata);
                }
                else
                {
                    Biodata.CreatedAt = DateTime.Now;
                    ctx.Biodatas.Add(Biodata);
                }

                await ctx.SaveChangesAsync();
                StatusMessage = "Saved successfully!";

                await Task.Delay(800);
                _mainVm.Navigate(AppPage.Browse);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsSaving = false;
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────

        private static string GetFilesDirectory(string subfolder)
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MarriageBureau", subfolder);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static Biodata CopyBiodata(Biodata src) => new()
        {
            Id                    = src.Id,
            Name                  = src.Name,
            Caste                 = src.Caste,
            Gender                = src.Gender,
            DateOfBirth           = src.DateOfBirth,
            TimeOfBirth           = src.TimeOfBirth,
            AmPm                  = src.AmPm,
            PlaceOfBirth          = src.PlaceOfBirth,
            Height                = src.Height,
            Complexion            = src.Complexion,
            BirthStar             = src.BirthStar,
            Padam                 = src.Padam,
            Raasi                 = src.Raasi,
            Religion              = src.Religion,
            PaternalGotram        = src.PaternalGotram,
            MaternalGotram        = src.MaternalGotram,
            Qualification         = src.Qualification,
            Designation           = src.Designation,
            CompanyAddress        = src.CompanyAddress,
            FatherName            = src.FatherName,
            FatherOccupation      = src.FatherOccupation,
            MotherName            = src.MotherName,
            MotherOccupation      = src.MotherOccupation,
            NoOfSiblings          = src.NoOfSiblings,
            BrotherCount          = src.BrotherCount,
            BrotherOccupation     = src.BrotherOccupation,
            SisterCount           = src.SisterCount,
            SisterOccupation      = src.SisterOccupation,
            BrotherInLaw          = src.BrotherInLaw,
            GrandFatherName       = src.GrandFatherName,
            ElderFather           = src.ElderFather,
            ElderFatherPhone      = src.ElderFatherPhone,
            DoorNumber            = src.DoorNumber,
            AddressLine           = src.AddressLine,
            TownVillage           = src.TownVillage,
            District              = src.District,
            State                 = src.State,
            Country               = src.Country,
            PinCode               = src.PinCode,
            LivingIn              = src.LivingIn,
            Phone1                = src.Phone1,
            Phone2                = src.Phone2,
            References            = src.References,
            ExpectationsFromPartner = src.ExpectationsFromPartner,
            PhotoPath             = src.PhotoPath,
            PdfPath               = src.PdfPath,
            CreatedAt             = src.CreatedAt,
            UpdatedAt             = src.UpdatedAt,
        };
    }
}
