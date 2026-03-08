using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MarriageBureau.Data;
using MarriageBureau.Models;
using Microsoft.EntityFrameworkCore;

namespace MarriageBureau.ViewModels
{
    /// <summary>
    /// Wraps a BiodataPhoto and adds a computed BitmapImage for UI binding.
    /// </summary>
    public class PhotoItem : BaseViewModel
    {
        private BitmapImage? _image;
        private bool _isSelected;

        public BiodataPhoto Photo { get; }

        public BitmapImage? Image
        {
            get => _image;
            set => SetProperty(ref _image, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string FileName => Path.GetFileName(Photo.FilePath);

        public PhotoItem(BiodataPhoto photo)
        {
            Photo = photo;
            LoadImage();
        }

        private void LoadImage()
        {
            if (!Photo.Exists) return;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource     = new Uri(Photo.FilePath, UriKind.Absolute);
                bmp.CacheOption   = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 400;
                bmp.EndInit();
                bmp.Freeze();
                Image = bmp;
            }
            catch { Image = null; }
        }
    }

    public class AddEditViewModel : BaseViewModel
    {
        private Biodata _biodata;
        private bool _isSaving;
        private bool _isEditMode;
        private string _statusMessage = string.Empty;

        // ── Multi-photo slideshow ────────────────────────────────────
        private ObservableCollection<PhotoItem> _photos = new();
        private PhotoItem? _currentPhoto;
        private int _currentPhotoIndex = -1;

        private readonly MainViewModel _mainVm;

        // ── Bound to Form ────────────────────────────────────────────

        public Biodata Biodata
        {
            get => _biodata;
            set => SetProperty(ref _biodata, value);
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

        // ── Photos ───────────────────────────────────────────────────

        public ObservableCollection<PhotoItem> Photos
        {
            get => _photos;
            set => SetProperty(ref _photos, value);
        }

        public PhotoItem? CurrentPhoto
        {
            get => _currentPhoto;
            set
            {
                if (_currentPhoto != null) _currentPhoto.IsSelected = false;
                SetProperty(ref _currentPhoto, value);
                if (_currentPhoto != null) _currentPhoto.IsSelected = true;
                OnPropertyChanged(nameof(HasPhotos));
                OnPropertyChanged(nameof(PhotoCounterText));
                OnPropertyChanged(nameof(CanGoPrev));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }

        public int CurrentPhotoIndex
        {
            get => _currentPhotoIndex;
            set
            {
                SetProperty(ref _currentPhotoIndex, value);
                if (_currentPhotoIndex >= 0 && _currentPhotoIndex < Photos.Count)
                    CurrentPhoto = Photos[_currentPhotoIndex];
                OnPropertyChanged(nameof(PhotoCounterText));
                OnPropertyChanged(nameof(CanGoPrev));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }

        public bool HasPhotos    => Photos.Count > 0;
        public bool CanGoPrev    => Photos.Count > 1 && _currentPhotoIndex > 0;
        public bool CanGoNext    => Photos.Count > 1 && _currentPhotoIndex < Photos.Count - 1;
        public string PhotoCounterText => Photos.Count == 0 ? "No photos"
                                        : $"Photo {_currentPhotoIndex + 1} of {Photos.Count}";

        public List<string> GenderOptions    { get; } = new() { "MALE", "FEMALE" };
        public List<string> ComplexionOptions { get; } = new() { "FAIR", "VERY FAIR", "WHITE", "MEDIUM", "RED", "DARK", "WHEATISH" };
        public List<string> RasiOptions      { get; } = new() { "MESHA", "VRUSHABA", "MIDHUNA", "KARKATAKA", "SIMHA", "KANYA", "TULA", "VRUCHIKA", "DHANU", "MAKARA", "KUMBHA", "MEENA" };

        // ── Commands ─────────────────────────────────────────────────

        public ICommand SaveCommand        { get; }
        public ICommand CancelCommand      { get; }
        public ICommand AddPhotosCommand   { get; }
        public ICommand RemovePhotoCommand { get; }
        public ICommand SetCoverPhotoCommand { get; }
        public ICommand PrevPhotoCommand   { get; }
        public ICommand NextPhotoCommand   { get; }
        public ICommand UploadPdfCommand   { get; }
        public ICommand RemovePdfCommand   { get; }
        public ICommand ViewPdfCommand     { get; }

        public AddEditViewModel(MainViewModel mainVm, Biodata? existingBiodata = null)
        {
            _mainVm  = mainVm;
            _biodata = existingBiodata != null
                ? CopyBiodata(existingBiodata)
                : new Biodata();

            IsEditMode = existingBiodata != null;

            // Load existing photos if editing
            if (existingBiodata != null)
                LoadExistingPhotos(existingBiodata.Id);

            SaveCommand          = new RelayCommand(async () => await SaveAsync(), () => !IsSaving);
            CancelCommand        = new RelayCommand(() => _mainVm.Navigate(AppPage.Browse));
            AddPhotosCommand     = new RelayCommand(AddPhotos);
            RemovePhotoCommand   = new RelayCommand(RemoveCurrentPhoto, () => CurrentPhoto != null);
            SetCoverPhotoCommand = new RelayCommand(SetAsCover, () => CurrentPhoto != null);
            PrevPhotoCommand     = new RelayCommand(PrevPhoto, () => CanGoPrev);
            NextPhotoCommand     = new RelayCommand(NextPhoto, () => CanGoNext);
            UploadPdfCommand     = new RelayCommand(UploadPdf);
            RemovePdfCommand     = new RelayCommand(RemovePdf, () => !string.IsNullOrWhiteSpace(Biodata.PdfPath));
            ViewPdfCommand       = new RelayCommand(OpenPdf, () => Biodata.HasPdf);
        }

        // ── Photos ────────────────────────────────────────────────────

        private void LoadExistingPhotos(int biodataId)
        {
            try
            {
                using var ctx = new AppDbContext();
                var photos = ctx.BiodataPhotos
                               .Where(p => p.BiodataId == biodataId)
                               .OrderBy(p => p.SortOrder)
                               .ToList();

                foreach (var p in photos)
                    Photos.Add(new PhotoItem(p));

                if (Photos.Count > 0)
                    CurrentPhotoIndex = 0;

                // Back-compat: also load legacy single PhotoPath
                if (Photos.Count == 0 && !string.IsNullOrWhiteSpace(_biodata.PhotoPath)
                    && File.Exists(_biodata.PhotoPath))
                {
                    var legacyPhoto = new BiodataPhoto
                    {
                        BiodataId = biodataId,
                        FilePath  = _biodata.PhotoPath,
                        SortOrder = 0
                    };
                    Photos.Add(new PhotoItem(legacyPhoto));
                    CurrentPhotoIndex = 0;
                }

                OnPropertyChanged(nameof(HasPhotos));
                OnPropertyChanged(nameof(PhotoCounterText));
            }
            catch { }
        }

        private void AddPhotos()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title      = "Add Profile Photos",
                Filter     = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp",
                Multiselect = true
            };
            if (dlg.ShowDialog() != true) return;

            var destDir = GetFilesDirectory("Photos");

            foreach (var srcFile in dlg.FileNames)
            {
                try
                {
                    var destFile = Path.Combine(destDir,
                        $"{Guid.NewGuid()}{Path.GetExtension(srcFile)}");
                    File.Copy(srcFile, destFile, overwrite: true);

                    var photo = new BiodataPhoto
                    {
                        BiodataId = Biodata.Id,   // 0 for new records; fixed on save
                        FilePath  = destFile,
                        SortOrder = Photos.Count
                    };
                    var item = new PhotoItem(photo);
                    Photos.Add(item);
                }
                catch { /* skip bad files */ }
            }

            if (Photos.Count > 0 && CurrentPhoto == null)
                CurrentPhotoIndex = 0;

            OnPropertyChanged(nameof(HasPhotos));
            OnPropertyChanged(nameof(PhotoCounterText));
            CommandManager.InvalidateRequerySuggested();
        }

        private void RemoveCurrentPhoto()
        {
            if (CurrentPhoto == null) return;
            int idx = Photos.IndexOf(CurrentPhoto);
            Photos.RemoveAt(idx);

            if (Photos.Count == 0)
            {
                CurrentPhoto = null;
                _currentPhotoIndex = -1;
            }
            else
            {
                CurrentPhotoIndex = Math.Min(idx, Photos.Count - 1);
            }
            OnPropertyChanged(nameof(HasPhotos));
            OnPropertyChanged(nameof(PhotoCounterText));
            CommandManager.InvalidateRequerySuggested();
        }

        private void SetAsCover()
        {
            if (CurrentPhoto == null) return;
            int idx = Photos.IndexOf(CurrentPhoto);
            if (idx == 0) return;

            // Move to front
            Photos.Move(idx, 0);
            for (int i = 0; i < Photos.Count; i++)
                Photos[i].Photo.SortOrder = i;

            CurrentPhotoIndex = 0;
            OnPropertyChanged(nameof(PhotoCounterText));
        }

        private void PrevPhoto()
        {
            if (CanGoPrev) CurrentPhotoIndex--;
        }

        private void NextPhoto()
        {
            if (CanGoNext) CurrentPhotoIndex++;
        }

        // ── PDF ──────────────────────────────────────────────────────

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
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(Biodata.PdfPath!)
                        { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Cannot open PDF: {ex.Message}");
                }
            }
        }

        // ── Save ─────────────────────────────────────────────────────

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Biodata.Name))
            {
                StatusMessage = "Name is required.";
                return;
            }

            IsSaving = true;
            StatusMessage = "Saving…";
            try
            {
                using var ctx = new AppDbContext();
                Biodata.UpdatedAt = DateTime.Now;

                // Set cover photo path for backward compat
                if (Photos.Count > 0 && Photos[0].Photo.Exists)
                    Biodata.PhotoPath = Photos[0].Photo.FilePath;

                if (IsEditMode)
                {
                    ctx.Biodatas.Update(Biodata);
                    await ctx.SaveChangesAsync();

                    // Delete old photos for this profile, then re-insert current list
                    var oldPhotos = await ctx.BiodataPhotos
                                             .Where(p => p.BiodataId == Biodata.Id)
                                             .ToListAsync();
                    ctx.BiodataPhotos.RemoveRange(oldPhotos);
                }
                else
                {
                    Biodata.CreatedAt = DateTime.Now;
                    ctx.Biodatas.Add(Biodata);
                    await ctx.SaveChangesAsync(); // Get the new Id
                }

                // Persist photos
                for (int i = 0; i < Photos.Count; i++)
                {
                    var p = Photos[i].Photo;
                    p.BiodataId = Biodata.Id;
                    p.SortOrder = i;
                    ctx.BiodataPhotos.Add(p);
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

        // ── Helpers ──────────────────────────────────────────────────

        private static string GetFilesDirectory(string subfolder)
        {
            var rootPath = ConfigurationManager.AppSettings["DataRootPath"];
            var dir = Path.Combine(
               rootPath,
                "MarriageBureau", subfolder);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static Biodata CopyBiodata(Biodata src) => new()
        {
            Id                      = src.Id,
            Name                    = src.Name,
            Caste                   = src.Caste,
            Gender                  = src.Gender,
            DateOfBirth             = src.DateOfBirth,
            TimeOfBirth             = src.TimeOfBirth,
            AmPm                    = src.AmPm,
            PlaceOfBirth            = src.PlaceOfBirth,
            Height                  = src.Height,
            Complexion              = src.Complexion,
            BirthStar               = src.BirthStar,
            Padam                   = src.Padam,
            Raasi                   = src.Raasi,
            Religion                = src.Religion,
            PaternalGotram          = src.PaternalGotram,
            MaternalGotram          = src.MaternalGotram,
            Qualification           = src.Qualification,
            Designation             = src.Designation,
            CompanyAddress          = src.CompanyAddress,
            FatherName              = src.FatherName,
            FatherOccupation        = src.FatherOccupation,
            MotherName              = src.MotherName,
            MotherOccupation        = src.MotherOccupation,
            NoOfSiblings            = src.NoOfSiblings,
            BrotherCount            = src.BrotherCount,
            BrotherOccupation       = src.BrotherOccupation,
            SisterCount             = src.SisterCount,
            SisterOccupation        = src.SisterOccupation,
            BrotherInLaw            = src.BrotherInLaw,
            GrandFatherName         = src.GrandFatherName,
            ElderFather             = src.ElderFather,
            ElderFatherPhone        = src.ElderFatherPhone,
            DoorNumber              = src.DoorNumber,
            AddressLine             = src.AddressLine,
            TownVillage             = src.TownVillage,
            District                = src.District,
            State                   = src.State,
            Country                 = src.Country,
            PinCode                 = src.PinCode,
            LivingIn                = src.LivingIn,
            Phone1                  = src.Phone1,
            Phone2                  = src.Phone2,
            References              = src.References,
            ExpectationsFromPartner = src.ExpectationsFromPartner,
            PhotoPath               = src.PhotoPath,
            PdfPath                 = src.PdfPath,
            CreatedAt               = src.CreatedAt,
            UpdatedAt               = src.UpdatedAt,
        };
    }
}
