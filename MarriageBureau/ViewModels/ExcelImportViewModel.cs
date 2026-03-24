using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using ClosedXML.Excel;
using MarriageBureau.Data;
using MarriageBureau.Models;
using Microsoft.EntityFrameworkCore;

namespace MarriageBureau.ViewModels
{
    /// <summary>
    /// Represents a single row parsed from the Excel sheet – used for preview.
    /// </summary>
    public class ImportPreviewRow
    {
        public int RowNum { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Caste { get; set; } = string.Empty;
        public string DateOfBirth { get; set; } = string.Empty;
        public string Height { get; set; } = string.Empty;
        public string Qualification { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Phone1 { get; set; } = string.Empty;
        public string BirthStar { get; set; } = string.Empty;
        public string Raasi { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";   // Pending / Imported / Skipped / Error
        public string? ErrorMessage { get; set; }

        // Full biodata built from the row (used during actual import)
        public Biodata? ParsedBiodata { get; set; }
    }

    public class ExcelImportViewModel : BaseViewModel
    {
        private readonly MainViewModel _mainVm;

        private string _filePath = string.Empty;
        private bool _isLoading;
        private bool _isImporting;
        private string _statusMessage = string.Empty;
        private int _importedCount;
        private int _skippedCount;
        private int _errorCount;
        private bool _skipDuplicates = true;
        private ObservableCollection<ImportPreviewRow> _previewRows = new();

        // ── Properties ──────────────────────────────────────────────────

        public string FilePath
        {
            get => _filePath;
            set { SetProperty(ref _filePath, value); OnPropertyChanged(nameof(FileSelected)); }
        }

        public bool FileSelected => !string.IsNullOrWhiteSpace(FilePath);

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool IsImporting
        {
            get => _isImporting;
            set => SetProperty(ref _isImporting, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public int ImportedCount
        {
            get => _importedCount;
            set => SetProperty(ref _importedCount, value);
        }

        public int SkippedCount
        {
            get => _skippedCount;
            set => SetProperty(ref _skippedCount, value);
        }

        public int ErrorCount
        {
            get => _errorCount;
            set => SetProperty(ref _errorCount, value);
        }

        public bool SkipDuplicates
        {
            get => _skipDuplicates;
            set => SetProperty(ref _skipDuplicates, value);
        }

        public ObservableCollection<ImportPreviewRow> PreviewRows
        {
            get => _previewRows;
            set => SetProperty(ref _previewRows, value);
        }

        public int TotalRows => PreviewRows.Count;

        // ── Commands ────────────────────────────────────────────────────

        public ICommand BrowseFileCommand { get; }
        public ICommand ParsePreviewCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ClearCommand { get; }

        // ── Constructor ─────────────────────────────────────────────────

        public ExcelImportViewModel(MainViewModel mainVm)
        {
            _mainVm = mainVm;

            BrowseFileCommand  = new RelayCommand(BrowseFile);
            ParsePreviewCommand = new RelayCommand(async () => await ParsePreviewAsync(), () => FileSelected && !IsLoading);
            ImportCommand      = new RelayCommand(async () => await ImportAsync(),
                                                   () => PreviewRows.Count > 0 && !IsImporting && !IsLoading);
            CancelCommand      = new RelayCommand(() => _mainVm.Navigate(AppPage.Browse));
            ClearCommand       = new RelayCommand(Clear);
        }

        // ── File Browse ─────────────────────────────────────────────────

        private void BrowseFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Select Excel Biodata File",
                Filter = "Excel Files|*.xlsx;*.xlsm;*.xls",
                Multiselect = false
            };
            if (dlg.ShowDialog() != true) return;

            FilePath = dlg.FileName;
            PreviewRows.Clear();
            StatusMessage = $"File selected: {Path.GetFileName(FilePath)}";
            ImportedCount = SkippedCount = ErrorCount = 0;
        }

        // ── Parse Preview ───────────────────────────────────────────────

        private async Task ParsePreviewAsync()
        {
            if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath)) return;

            IsLoading = true;
            StatusMessage = "Parsing Excel file…";
            PreviewRows.Clear();
            ImportedCount = SkippedCount = ErrorCount = 0;

            try
            {
                var rows = await Task.Run(() => ParseExcelFile(FilePath));
                foreach (var r in rows)
                    PreviewRows.Add(r);

                OnPropertyChanged(nameof(TotalRows));
                StatusMessage = $"Found {PreviewRows.Count} records across all sheets. Review and click Import.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error reading file: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Core Parsing Logic ──────────────────────────────────────────

        /// <summary>
        /// Parses both MALE and FEMALE sheets from the Excel workbook.
        /// Column mapping is based on the actual Excel format provided.
        /// </summary>
        private static List<ImportPreviewRow> ParseExcelFile(string path)
        {
            var results = new List<ImportPreviewRow>();
            int rowNum = 1;

            using var wb = new XLWorkbook(path);

            foreach (var ws in wb.Worksheets)
            {
                // Detect header row (first row with S.NO. or NAME in col A/B)
                int headerRow = FindHeaderRow(ws);
                if (headerRow < 0) continue;

                // Build column index map from headers
                var colMap = BuildColumnMap(ws, headerRow);

                // Infer gender from sheet name if not in data
                string sheetGender = ws.Name.ToUpper().Contains("FEMALE") ? "FEMALE"
                                   : ws.Name.ToUpper().Contains("MALE")   ? "MALE"
                                   : string.Empty;

                int lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow;
                for (int row = headerRow + 1; row <= lastRow; row++)
                {
                    var nameCell = GetCell(ws, row, colMap, "NAME");
                    if (string.IsNullOrWhiteSpace(nameCell)) continue;
                    if (nameCell.Equals("NAME", StringComparison.OrdinalIgnoreCase)) continue; // duplicate header

                    try
                    {
                        var preview = new ImportPreviewRow
                        {
                            RowNum      = rowNum++,
                            Name        = Clean(nameCell),
                            Gender      = Clean(GetCell(ws, row, colMap, "GENDER") ?? sheetGender),
                            Caste       = Clean(GetCell(ws, row, colMap, "CASTE")),
                            DateOfBirth = Clean(GetCell(ws, row, colMap, "D.O.B")),
                            Height      = Clean(GetCell(ws, row, colMap, "HEIGHT")),
                            Qualification = Clean(GetCell(ws, row, colMap, "QUALIFICATION")),
                            Designation = Clean(GetCell(ws, row, colMap, "DESIGNATION")),
                            District    = Clean(GetCell(ws, row, colMap, "DISTRICT")),
                            Phone1      = Clean(GetCell(ws, row, colMap, "PHONE1")),
                            BirthStar   = Clean(GetCell(ws, row, colMap, "BIRTH STAR")),
                            Raasi       = Clean(GetCell(ws, row, colMap, "RAASI")),
                        };

                        // Build full Biodata record
                        preview.ParsedBiodata = BuildBiodata(ws, row, colMap, sheetGender);
                        preview.Status = "Pending";
                        results.Add(preview);
                    }
                    catch (Exception ex)
                    {
                        results.Add(new ImportPreviewRow
                        {
                            RowNum = rowNum++,
                            Name   = nameCell,
                            Status = "Error",
                            ErrorMessage = ex.Message
                        });
                    }
                }
            }

            return results;
        }

        private static int FindHeaderRow(IXLWorksheet ws)
        {
            for (int r = 1; r <= Math.Min(10, ws.LastRowUsed()?.RowNumber() ?? 1); r++)
            {
                for (int c = 1; c <= Math.Min(5, ws.LastColumnUsed()?.ColumnNumber() ?? 1); c++)
                {
                    var val = ws.Cell(r, c).GetString().ToUpper().Trim();
                    if (val == "NAME" || val == "S.NO.") return r;
                }
            }
            return -1;
        }

        private static Dictionary<string, int> BuildColumnMap(IXLWorksheet ws, int headerRow)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 50;

            for (int c = 1; c <= lastCol; c++)
            {
                var header = ws.Cell(headerRow, c).GetString().ToUpper().Trim();
                if (string.IsNullOrWhiteSpace(header)) continue;

                // Normalise common variants
                var key = header switch
                {
                    "PH NO.1" or "PHONE NUMBER 1" or "PH NO.1" => "PHONE1",
                    "PH NO.2" or "PHONE NUMBER 2" or "PH NO.2" => "PHONE2",
                    "NAME OF THE COMPANY & ADRESS" or "COMPANY NAME & ADDRESS" => "COMPANY",
                    "COMPLECTION" or "COMPLEXION" => "COMPLEXION",
                    "BIRTH STAR" or "NAKSHATRA" => "BIRTH STAR",
                    "PATERNAL GOTRAM" => "PATERNAL GOTRAM",
                    "MATERNAL GOTRAM" => "MATERNAL GOTRAM",
                    "D.NO." or "DOOR NO" or "D.NO" => "D.NO.",
                    "TOWN/VILLAGE" or "TOWN / VILLAGE" => "TOWN",
                    "OCCUPATION /EDUCATION" or "OCCUPATION" or "OCCUPATION/EDUCATION" => "OCCUPATION",
                    "EXPECTATIONS FROM PARTNER" => "EXPECTATIONS",
                    "GRAND FATHER NAME" => "GRANDFATHER",
                    "UNCLE" => "UNCLE",
                    "NO.OF SIBLINGS" => "SIBLINGS",
                    "REFERENCE NAME" or "REFERENCES" => "REFERENCES",
                    "REFERENCE PNONE" or "REFERENCE PHONE" => "REF PHONE",
                    _ => header
                };

                if (!map.ContainsKey(key))
                    map[key] = c;
            }
            return map;
        }

        private static string? GetCell(IXLWorksheet ws, int row, Dictionary<string, int> map, string key)
        {
            if (!map.TryGetValue(key, out int col)) return null;
            var cell = ws.Cell(row, col);
            if (cell.IsEmpty()) return null;

            // Handle TimeSpan cells (time of birth)
            if (cell.DataType == XLDataType.DateTime)
            {
                try { return cell.GetDateTime().ToString("HH:mm"); } catch { }
            }
            return cell.GetString().Trim();
        }

        private static string Clean(string? s) => string.IsNullOrWhiteSpace(s) ? string.Empty : s.Trim();

        private static Biodata BuildBiodata(IXLWorksheet ws, int row,
                                            Dictionary<string, int> colMap, string sheetGender)
        {
            string G(string key) => Clean(GetCell(ws, row, colMap, key));

            // Time of birth – handle cell stored as DateTime/TimeSpan
            string timeOfBirth = G("TIME OF BIRTH");
            if (string.IsNullOrWhiteSpace(timeOfBirth) && colMap.TryGetValue("TIME OF BIRTH", out int tc))
            {
                var cell = ws.Cell(row, tc);
                if (!cell.IsEmpty())
                {
                    try
                    {
                        if (cell.DataType == XLDataType.DateTime)
                            timeOfBirth = cell.GetDateTime().ToString("HH:mm");
                        else if (cell.DataType == XLDataType.Number)
                        {
                            // Decimal fraction of a day
                            var ts = TimeSpan.FromDays(cell.GetDouble());
                            timeOfBirth = ts.ToString(@"hh\:mm");
                        }
                        else
                            timeOfBirth = cell.GetString();
                    }
                    catch { timeOfBirth = cell.GetString(); }
                }
            }

            string gender = G("GENDER");
            if (string.IsNullOrWhiteSpace(gender)) gender = sheetGender;
            if (string.IsNullOrWhiteSpace(gender)) gender = "MALE";

            return new Biodata
            {
                Name                    = G("NAME"),
                Caste                   = G("CASTE"),
                Gender                  = gender.ToUpper(),
                DateOfBirth             = G("D.O.B"),
                TimeOfBirth             = timeOfBirth,
                AmPm                    = G("AM/PM"),
                PlaceOfBirth            = G("PLACE OF BIRTH"),
                Height                  = G("HEIGHT"),
                Complexion              = G("COMPLEXION"),
                BirthStar               = G("BIRTH STAR"),
                Padam                   = G("PADAM"),
                Raasi                   = G("RAASI"),
                Religion                = G("RELIGION"),
                PaternalGotram          = G("PATERNAL GOTRAM"),
                MaternalGotram          = G("MATERNAL GOTRAM"),
                Qualification           = G("QUALIFICATION"),
                Designation             = G("DESIGNATION"),
                CompanyAddress          = G("COMPANY"),
                FatherName              = G("FATHER NAME"),
                FatherOccupation        = G("FATHER OCCUPATION"),
                MotherName              = G("MOTHER NAME"),
                MotherOccupation        = G("MOTHER OCCUPATION"),
                NoOfSiblings            = G("SIBLINGS"),
                BrotherCount            = G("BROTHER"),
                BrotherOccupation       = G("OCCUPATION"),
                SisterCount             = G("SISTER"),
                SisterOccupation        = G("OCCUPATION"),  // same col reused
                BrotherInLaw            = G("BROTHER IN LAW"),
                GrandFatherName         = G("GRANDFATHER"),
                ElderFather             = G("UNCLE"),
                ElderFatherPhone        = G("PHONE"),
                DoorNumber              = G("D.NO."),
                AddressLine             = G("ADRESS"),
                TownVillage             = G("TOWN"),
                District                = G("DISTRICT"),
                State                   = G("STATE"),
                Country                 = G("COUNTRY"),
                PinCode                 = G("PIN CODE"),
                LivingIn                = G("LIVING IN"),
                Phone1                  = G("PHONE1"),
                Phone2                  = G("PHONE2"),
                References              = G("REFERENCES"),
                ExpectationsFromPartner = G("EXPECTATIONS"),
                CreatedAt               = DateTime.Now,
                UpdatedAt               = DateTime.Now,
            };
        }

        // ── Import ──────────────────────────────────────────────────────

        private async Task ImportAsync()
        {
            if (PreviewRows.Count == 0) return;

            IsImporting = true;
            ImportedCount = SkippedCount = ErrorCount = 0;
            StatusMessage = "Importing…";

            try
            {
                // Load existing names for duplicate detection
                HashSet<string> existingNames;
                using (var ctx = new AppDbContext())
                {
                    var names = await ctx.Biodatas.Select(b => b.Name.ToLower()).ToListAsync();
                    existingNames = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
                }

                foreach (var row in PreviewRows)
                {
                    if (row.ParsedBiodata == null)
                    {
                        row.Status = "Error";
                        row.ErrorMessage = "No parsed data available.";
                        ErrorCount++;
                        continue;
                    }

                    // Check for duplicate
                    if (SkipDuplicates && existingNames.Contains(row.ParsedBiodata.Name))
                    {
                        row.Status = "Skipped (duplicate)";
                        SkippedCount++;
                        continue;
                    }

                    try
                    {
                        using var ctx = new AppDbContext();
                        // Assign ProfileId if not already set
                        if (string.IsNullOrWhiteSpace(row.ParsedBiodata.ProfileId))
                            row.ParsedBiodata.ProfileId = AppDbContext.GenerateNextProfileId(ctx);
                        ctx.Biodatas.Add(row.ParsedBiodata);
                        await ctx.SaveChangesAsync();

                        existingNames.Add(row.ParsedBiodata.Name.ToLower());
                        row.Status = "Imported";
                        ImportedCount++;
                    }
                    catch (Exception ex)
                    {
                        row.Status = "Error";
                        row.ErrorMessage = ex.Message;
                        ErrorCount++;
                    }
                }

                StatusMessage = $"Done! Imported: {ImportedCount}  |  Skipped: {SkippedCount}  |  Errors: {ErrorCount}";
                OnPropertyChanged(nameof(PreviewRows));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Import failed: {ex.Message}";
            }
            finally
            {
                IsImporting = false;
            }
        }

        // ── Clear ───────────────────────────────────────────────────────

        private void Clear()
        {
            FilePath = string.Empty;
            PreviewRows.Clear();
            StatusMessage = string.Empty;
            ImportedCount = SkippedCount = ErrorCount = 0;
            OnPropertyChanged(nameof(TotalRows));
        }
    }
}
