using System.Collections.ObjectModel;
using System.Windows.Input;
using MarriageBureau.Data;
using MarriageBureau.Models;
using Microsoft.EntityFrameworkCore;

namespace MarriageBureau.ViewModels
{
    public class BrowseViewModel : BaseViewModel
    {
        private ObservableCollection<Biodata> _allProfiles = new();
        private ObservableCollection<Biodata> _filteredProfiles = new();
        private Biodata? _selectedProfile;
        private string _searchText   = string.Empty;
        private string _genderFilter = "All";
        private string _casteFilter  = string.Empty;
        private string _statusFilter = "All";
        private bool   _isLoading;

        private readonly MainViewModel _mainVm;

        public ObservableCollection<Biodata> FilteredProfiles
        {
            get => _filteredProfiles;
            set => SetProperty(ref _filteredProfiles, value);
        }

        public Biodata? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                SetProperty(ref _selectedProfile, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set { SetProperty(ref _searchText, value); ApplyFilter(); }
        }

        public string GenderFilter
        {
            get => _genderFilter;
            set { SetProperty(ref _genderFilter, value); ApplyFilter(); }
        }

        public string CasteFilter
        {
            get => _casteFilter;
            set { SetProperty(ref _casteFilter, value); ApplyFilter(); }
        }

        public string StatusFilter
        {
            get => _statusFilter;
            set { SetProperty(ref _statusFilter, value); ApplyFilter(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public int TotalCount    => _allProfiles.Count;
        public int MaleCount     => _allProfiles.Count(p => p.Gender?.ToUpper() == "MALE");
        public int FemaleCount   => _allProfiles.Count(p => p.Gender?.ToUpper() == "FEMALE");
        public int FilteredCount => FilteredProfiles.Count;

        public ICommand RefreshCommand      { get; }
        public ICommand AddNewCommand       { get; }
        public ICommand EditCommand         { get; }
        public ICommand DeleteCommand       { get; }
        public ICommand ViewPdfCommand      { get; }
        public ICommand ClearSearchCommand  { get; }
        public ICommand ImportExcelCommand  { get; }
        public ICommand ExportCommand       { get; }

        public List<string> GenderOptions { get; } = new() { "All", "MALE", "FEMALE" };
        public List<string> StatusOptions { get; } = new[] { "All" }
            .Concat(Enum.GetNames<ProfileStatus>()).ToList();

        public BrowseViewModel(MainViewModel mainVm)
        {
            _mainVm = mainVm;
            RefreshCommand   = new RelayCommand(async () => await LoadAsync());
            AddNewCommand    = new RelayCommand(() => _mainVm.Navigate(AppPage.AddEdit));
            EditCommand      = new RelayCommand(() => _mainVm.Navigate(AppPage.AddEdit, SelectedProfile),
                                                 () => SelectedProfile != null);
            DeleteCommand    = new RelayCommand(async () => await DeleteSelectedAsync(),
                                                 () => SelectedProfile != null);
            ViewPdfCommand     = new RelayCommand(OpenPdf, () => SelectedProfile?.HasPdf == true);
            ClearSearchCommand = new RelayCommand(ClearSearch);
            ImportExcelCommand = new RelayCommand(() => _mainVm.Navigate(AppPage.ExcelImport));
            ExportCommand      = new RelayCommand(() => _mainVm.Navigate(AppPage.Export, SelectedProfile),
                                                   () => SelectedProfile != null);
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                using var ctx = new AppDbContext();
                var list = await ctx.Biodatas
                               .Include(b => b.Photos.OrderBy(p => p.SortOrder))
                               .OrderBy(b => b.Name)
                               .ToListAsync();
                _allProfiles = new ObservableCollection<Biodata>(list);
                ApplyFilter();
                OnPropertyChanged(nameof(TotalCount));
                OnPropertyChanged(nameof(MaleCount));
                OnPropertyChanged(nameof(FemaleCount));
            }
            finally { IsLoading = false; }
        }

        private void ApplyFilter()
        {
            var q = _allProfiles.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var st = SearchText.ToLower();
                q = q.Where(p =>
                    (p.Name?.ToLower().Contains(st) ?? false) ||
                    (p.Caste?.ToLower().Contains(st) ?? false) ||
                    (p.Qualification?.ToLower().Contains(st) ?? false) ||
                    (p.Designation?.ToLower().Contains(st) ?? false) ||
                    (p.District?.ToLower().Contains(st) ?? false) ||
                    (p.Phone1?.Contains(st) ?? false));
            }

            if (GenderFilter != "All")
                q = q.Where(p => p.Gender?.ToUpper() == GenderFilter.ToUpper());

            if (!string.IsNullOrWhiteSpace(CasteFilter))
                q = q.Where(p => p.Caste?.ToLower().Contains(CasteFilter.ToLower()) ?? false);

            if (StatusFilter != "All" && Enum.TryParse<ProfileStatus>(StatusFilter, out var statusEnum))
                q = q.Where(p => p.Status == statusEnum);

            FilteredProfiles = new ObservableCollection<Biodata>(q);
            OnPropertyChanged(nameof(FilteredCount));
        }

        private async Task DeleteSelectedAsync()
        {
            if (SelectedProfile == null) return;
            var result = System.Windows.MessageBox.Show(
                $"Delete profile of '{SelectedProfile.Name}'?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            using var ctx = new AppDbContext();
            var entity = await ctx.Biodatas.FindAsync(SelectedProfile.Id);
            if (entity != null)
            {
                ctx.Biodatas.Remove(entity);
                await ctx.SaveChangesAsync();
            }
            await LoadAsync();
        }

        private void OpenPdf()
        {
            if (SelectedProfile?.HasPdf == true)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo(SelectedProfile.PdfPath!)
                    { UseShellExecute = true };
                    System.Diagnostics.Process.Start(psi);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Cannot open PDF:\n{ex.Message}",
                        "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void ClearSearch()
        {
            SearchText   = string.Empty;
            GenderFilter = "All";
            CasteFilter  = string.Empty;
            StatusFilter = "All";
        }
    }
}
