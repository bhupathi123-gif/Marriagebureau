using System.Collections.ObjectModel;
using System.Globalization;
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
        public string IntId { get; set; } = string.Empty;
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
        public ICommand ExportErrorsCommand { get; }

        // ── Constructor ─────────────────────────────────────────────────

        public ExcelImportViewModel(MainViewModel mainVm)
        {
            _mainVm = mainVm;

            BrowseFileCommand   = new RelayCommand(BrowseFile);
            ParsePreviewCommand = new RelayCommand(async () => await ParsePreviewAsync(), () => FileSelected && !IsLoading);
            ImportCommand       = new RelayCommand(async () => await ImportAsync(),
                                                    () => PreviewRows.Count > 0 && !IsImporting && !IsLoading);
            CancelCommand       = new RelayCommand(() => _mainVm.Navigate(AppPage.Browse));
            ClearCommand        = new RelayCommand(Clear);
            ExportErrorsCommand = new RelayCommand(async () => await ExportErrorsAsync(),
                                                    () => PreviewRows.Any(r => r.Status == "Error" || r.Status.StartsWith("Skipped")));
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

                int errCount = rows.Count(r => r.Status == "Error");
                StatusMessage = $"Found {PreviewRows.Count} records. " +
                                (errCount > 0 ? $"{errCount} rows have errors. " : "") +
                                "Review and click Import.";
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

        private static List<ImportPreviewRow> ParseExcelFile(string path)
        {
            var results = new List<ImportPreviewRow>();
            int rowNum = 1;

            // Track within-file duplicates (intId, fullName+fatherName+gender, phone)
            var seenIntIds    = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var seenIdentKeys = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var seenPhones    = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            using var wb = new XLWorkbook(path);

            foreach (var ws in wb.Worksheets)
            {
                int headerRow = FindHeaderRow(ws);
                if (headerRow < 0) continue;

                var colMap = BuildColumnMap(ws, headerRow);

                string sheetGender = ws.Name.ToUpper().Contains("FEMALE") ? "FEMALE"
                                   : ws.Name.ToUpper().Contains("MALE")   ? "MALE"
                                   : string.Empty;

                int lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow;
                for (int row = headerRow + 1; row <= lastRow; row++)
                {
                    var nameCell = GetCell(ws, row, colMap, "NAME");
                    if (string.IsNullOrWhiteSpace(nameCell)) continue;
                    if (nameCell.Equals("NAME", StringComparison.OrdinalIgnoreCase)) continue;

                    var errors = new List<string>();

                    try
                    {
                        // ── IntId from S.NO. column ──────────────────────
                        string intId = Clean(GetCell(ws, row, colMap, "S.NO."));
                        // Also accept INT ID / INTID as direct column
                        if (string.IsNullOrWhiteSpace(intId))
                            intId = Clean(GetCell(ws, row, colMap, "INTID"));
                        if (string.IsNullOrWhiteSpace(intId))
                            intId = Clean(GetCell(ws, row, colMap, "INT ID"));

                        if (string.IsNullOrWhiteSpace(intId))
                            errors.Add("IntId (S.NO.) is empty");

                        // ── DOB – multiple formats ────────────────────────
                        //string rawDob = Clean(GetCell(ws, row, colMap, "D.O.B"));
                        //string parsedDob = NormalizeDob(rawDob, ws, row, colMap);

                        string parsedDob = ReadDob(ws,row,colMap);
                        // ── Duplicate IntId within file ───────────────────
                        if (!string.IsNullOrWhiteSpace(intId))
                        {
                            if (seenIntIds.TryGetValue(intId, out int prevRow))
                                errors.Add($"Duplicate IntId '{intId}' (also row {prevRow})");
                            else
                                seenIntIds[intId] = rowNum;
                        }

                        // ── Duplicate Name+FatherName+Gender within file ──
                        string fatherName = Clean(GetCell(ws, row, colMap, "FATHER NAME"));
                        string gender = Clean(GetCell(ws, row, colMap, "GENDER") ?? sheetGender);
                        string identKey = $"{nameCell.ToLower()}|{fatherName.ToLower()}|{gender.ToLower()}";
                        if (seenIdentKeys.TryGetValue(identKey, out int prevIdentRow))
                            errors.Add($"Duplicate Name+FatherName+Gender (also row {prevIdentRow})");
                        else
                            seenIdentKeys[identKey] = rowNum;

                        // ── Duplicate Phone within file ───────────────────
                        string phone = Clean(GetCell(ws, row, colMap, "PHONE1"));
                        if (!string.IsNullOrWhiteSpace(phone))
                        {
                            if (seenPhones.TryGetValue(phone, out int prevPhoneRow))
                                errors.Add($"Duplicate Phone '{phone}' (also row {prevPhoneRow})");
                            else
                                seenPhones[phone] = rowNum;
                        }

                        var preview = new ImportPreviewRow
                        {
                            RowNum      = rowNum++,
                            IntId       = intId,
                            Name        = Clean(nameCell),
                            Gender      = Clean(GetCell(ws, row, colMap, "GENDER") ?? sheetGender),
                            Caste       = Clean(GetCell(ws, row, colMap, "CASTE")),
                            DateOfBirth = parsedDob,
                            Height      = Clean(GetCell(ws, row, colMap, "HEIGHT")),
                            Qualification = Clean(GetCell(ws, row, colMap, "QUALIFICATION")),
                            Designation = Clean(GetCell(ws, row, colMap, "DESIGNATION")),
                            District    = Clean(GetCell(ws, row, colMap, "DISTRICT")),
                            Phone1      = phone,
                            BirthStar   = Clean(GetCell(ws, row, colMap, "BIRTH STAR")),
                            Raasi       = Clean(GetCell(ws, row, colMap, "RAASI")),
                        };

                        if (errors.Count > 0)
                        {
                            preview.Status       = "Error";
                            preview.ErrorMessage = string.Join("; ", errors);
                        }
                        else
                        {
                            preview.ParsedBiodata = BuildBiodata(ws, row, colMap, sheetGender, intId, parsedDob);
                            preview.Status = "Pending";
                        }

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

        // ── DOB normalisation ────────────────────────────────────────────
        // Accepts: 01 March 2001, 01-Mar-2001, 01-03-2001, 01/Mar/2001, 01/03/2001
        private static string NormalizeDob(string raw, IXLWorksheet? ws = null,
                                            int row = 0, Dictionary<string, int>? colMap = null)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                // Try reading as Excel DateTime cell
                if (ws != null && colMap != null && colMap.TryGetValue("D.O.B", out int dc))
                {
                    var cell = ws.Cell(row, dc);
                    if (!cell.IsEmpty() && cell.DataType == XLDataType.DateTime)
                    {
                        try { return cell.GetDateTime().ToString("dd-MMM-yyyy"); } catch { }
                    }
                }
                return raw;
            }

            // Normalize separators: '/', ' ', '-'  →  '-'
            var normalized = raw.Trim()
                                .Replace("/", "-")
                                .Replace(" ", "-");

            // Try known formats
            string[] formats =
            {
                "dd MMMM yyyy",
                "dd-MMMM-yyyy",   // 01-March-2001
                "dd-MMM-yyyy",    // 01-Mar-2001
                "dd-MM-yyyy",     // 01-03-2001
                "d-MMMM-yyyy",
                "d-MMM-yyyy",
                "d-MM-yyyy",
                "dd-MM-yy",
                "d-MM-yy",
            };

            if (DateTime.TryParseExact(normalized, formats,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt.ToString("dd-MMM-yyyy");

            // Last resort: try general parse
            if (DateTime.TryParse(raw, out var dt2))
                return dt2.ToString("dd-MMM-yyyy");

            return raw; // return as-is if cannot parse
        }


        private static string ReadDob(IXLWorksheet ws, int row, Dictionary<string, int> colMap)
        {
            if (!colMap.TryGetValue("D.O.B", out var col)) return "";

            var cell = ws.Cell(row, col);
            if (cell.IsEmpty()) return "";

            // Best case: ClosedXML can give DateTime directly
            if (cell.TryGetValue<DateTime>(out var dt))
                return dt.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture);

            // If Excel stores as serial number
            if (cell.TryGetValue<double>(out var oa) && oa > 1 && oa < 60000)
            {
                try
                {
                    var dt2 = DateTime.FromOADate(oa);
                    return dt2.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture);
                }
                catch { }
            }

            // Fallback: use formatted text (what you see in Excel)
            var s = (cell.GetFormattedString() ?? "").Trim();
            return NormalizeDob(s);
        }

        // ── Header row finder ────────────────────────────────────────────

        private static int FindHeaderRow(IXLWorksheet ws)
        {
            for (int r = 1; r <= Math.Min(10, ws.LastRowUsed()?.RowNumber() ?? 1); r++)
            {
                for (int c = 1; c <= Math.Min(5, ws.LastColumnUsed()?.ColumnNumber() ?? 1); c++)
                {
                    var val = ws.Cell(r, c).GetString().ToUpper().Trim();
                    if (val == "NAME" || val == "S.NO." || val == "S.NO" || val == "S NO") return r;
                }
            }
            return -1;
        }

        // ── Column map builder ───────────────────────────────────────────

        private static Dictionary<string, int> BuildColumnMap(IXLWorksheet ws, int headerRow)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 50;

            for (int c = 1; c <= lastCol; c++)
            {
                var header = ws.Cell(headerRow, c).GetString().ToUpper().Trim();
                if (string.IsNullOrWhiteSpace(header)) continue;

                var key = header switch
                {
                    "PH NO.1" or "PHONE NUMBER 1" or "PH NO.1" or "PHONE 1" => "PHONE1",
                    "PH NO.2" or "PHONE NUMBER 2" or "PH NO.2" or "PHONE 2" => "PHONE2",
                    "NAME OF THE COMPANY & ADRESS" or "COMPANY NAME & ADDRESS" or "COMPANY" => "COMPANY",
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
                    "S.NO." or "S.NO" or "S NO" or "SNO" => "S.NO.",
                    "INT ID" or "INTID" or "INT_ID" => "INTID",
                    "PROFILE ID" or "PROFILEID" or "TMID" => "PROFILE ID",
                    // New columns from Excel template
                    "INCOME" => "INCOME",
                    "ASSET VALUE" => "ASSET VALUE",
                    "GIFT" => "GIFT",
                    "DESIGNATION DETAILS (NAME & PLACE)" or "DESIGNATION DETAILS" => "DESIGNATION DETAILS",
                    "BROTHER DETAILS" => "BROTHER DETAILS",
                    "SISTER DETAILS" => "SISTER DETAILS",
                    "PREFERENCES" => "PREFERENCES",
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

            if (cell.DataType == XLDataType.DateTime)
            {
                try { return cell.GetDateTime().ToString("HH:mm"); } catch { }
            }
            return cell.GetString().Trim();
        }

        private static string Clean(string? s) => string.IsNullOrWhiteSpace(s) ? string.Empty : s.Trim();

        private static Biodata BuildBiodata(IXLWorksheet ws, int row,
                                            Dictionary<string, int> colMap, string sheetGender,
                                            string intId, string parsedDob)
        {
            string G(string key) => Clean(GetCell(ws, row, colMap, key));

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

            // Prefer explicitly named PROFILE ID column, else leave empty
            string profileId = G("PROFILE ID");

            return new Biodata
            {
                IntId                   = intId,
                ProfileId               = string.IsNullOrWhiteSpace(profileId) ? null : profileId,
                Name                    = G("NAME"),
                Caste                   = G("CASTE"),
                Gender                  = gender.ToUpper(),
                DateOfBirth             = parsedDob,
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
                DesignationDetails      = G("DESIGNATION DETAILS"),
                CompanyAddress          = G("COMPANY"),
                Income                  = G("INCOME"),
                AssetValue              = G("ASSET VALUE"),
                Gift                    = G("GIFT"),
                FatherName              = G("FATHER NAME"),
                FatherOccupation        = G("FATHER OCCUPATION"),
                MotherName              = G("MOTHER NAME"),
                MotherOccupation        = G("MOTHER OCCUPATION"),
                NoOfSiblings            = G("SIBLINGS"),
                BrotherCount            = G("BROTHER"),
                BrotherOccupation       = G("OCCUPATION"),
                BrotherDetails          = G("BROTHER DETAILS"),
                SisterCount             = G("SISTER"),
                SisterOccupation        = G("OCCUPATION"),
                SisterDetails           = G("SISTER DETAILS"),
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
                ReferencePhone          = G("REF PHONE"),
                ExpectationsFromPartner = G("EXPECTATIONS"),
                Preferences             = G("PREFERENCES"),
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
                // Load existing records for duplicate detection
                HashSet<string> existingIntIds;
                HashSet<string> existingNames;
                HashSet<string> existingPhones;
                HashSet<string> existingIdentKeys;

                using (var ctx = new AppDbContext())
                {
                    existingIntIds    = new HashSet<string>(
                        ctx.Biodatas.Select(b => b.IntId).Where(x => x != null && x != "").ToList(),
                        StringComparer.OrdinalIgnoreCase);
                    existingNames     = new HashSet<string>(
                        ctx.Biodatas.Select(b => b.Name.ToLower()).ToList(),
                        StringComparer.OrdinalIgnoreCase);
                    existingPhones    = new HashSet<string>(
                        ctx.Biodatas.Where(b => b.Phone1 != null && b.Phone1 != "")
                                    .Select(b => b.Phone1!).ToList(),
                        StringComparer.OrdinalIgnoreCase);
                    existingIdentKeys = new HashSet<string>(
                        ctx.Biodatas.ToList()
                           .Select(b => $"{b.Name.ToLower()}|{(b.FatherName ?? "").ToLower()}|{(b.Gender ?? "").ToLower()}"),
                        StringComparer.OrdinalIgnoreCase);
                }

                foreach (var row in PreviewRows)
                {
                    // Skip rows that already have parse errors
                    if (row.Status == "Error")
                    {
                        ErrorCount++;
                        continue;
                    }

                    if (row.ParsedBiodata == null)
                    {
                        row.Status       = "Error";
                        row.ErrorMessage = "No parsed data available.";
                        ErrorCount++;
                        continue;
                    }

                    var dupErrors = new List<string>();

                    // IntId duplicate check (DB)
                    string intId = row.ParsedBiodata.IntId ?? "";
                    if (!string.IsNullOrWhiteSpace(intId) && existingIntIds.Contains(intId))
                        dupErrors.Add($"IntId '{intId}' already exists in database");

                    if (string.IsNullOrWhiteSpace(intId))
                        dupErrors.Add("IntId is empty");

                    // Name+FatherName+Gender duplicate check (DB)
                    string identKey = $"{row.ParsedBiodata.Name.ToLower()}|{(row.ParsedBiodata.FatherName ?? "").ToLower()}|{(row.ParsedBiodata.Gender ?? "").ToLower()}";
                    if (SkipDuplicates && existingIdentKeys.Contains(identKey))
                        dupErrors.Add("Duplicate Name+FatherName+Gender in database");

                    // Phone duplicate check (DB)
                    if (!string.IsNullOrWhiteSpace(row.ParsedBiodata.Phone1)
                        && existingPhones.Contains(row.ParsedBiodata.Phone1))
                        dupErrors.Add($"Phone '{row.ParsedBiodata.Phone1}' already exists in database");

                    if (dupErrors.Count > 0)
                    {
                        row.Status       = "Skipped — " + string.Join("; ", dupErrors);
                        row.ErrorMessage = string.Join("; ", dupErrors);
                        SkippedCount++;
                        continue;
                    }

                    try
                    {
                        using var ctx = new AppDbContext();

                        // Assign ProfileId if not provided
                        if (string.IsNullOrWhiteSpace(row.ParsedBiodata.ProfileId))
                            row.ParsedBiodata.ProfileId = AppDbContext.GenerateNextProfileId(ctx);

                        ctx.Biodatas.Add(row.ParsedBiodata);
                        await ctx.SaveChangesAsync();

                        existingIntIds.Add(intId);
                        existingNames.Add(row.ParsedBiodata.Name.ToLower());
                        if (!string.IsNullOrWhiteSpace(row.ParsedBiodata.Phone1))
                            existingPhones.Add(row.ParsedBiodata.Phone1);
                        existingIdentKeys.Add(identKey);

                        row.Status = "Imported";
                        ImportedCount++;
                    }
                    catch (Exception ex)
                    {
                        row.Status       = "Error";
                        row.ErrorMessage = ex.Message;
                        ErrorCount++;
                    }
                }

                StatusMessage = $"Done! Imported: {ImportedCount}  |  Skipped: {SkippedCount}  |  Errors: {ErrorCount}";

                // Show details of failed rows
                //if (SkippedCount + ErrorCount > 0)
                //{
                //    var failedRows = PreviewRows
                //        .Where(r => r.Status != "Imported" && r.Status != "Pending")
                //        .Select(r => $"Row {r.RowNum} [{r.IntId}] {r.Name}: {r.ErrorMessage}")
                //        .ToList();

                //    StatusMessage += $"\n\nNot imported records:\n" + string.Join("\n", failedRows);
                //}

                OnPropertyChanged(nameof(PreviewRows));
                CommandManager.InvalidateRequerySuggested();
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

        // ── Export Errors to Excel ──────────────────────────────────────

        private async Task ExportErrorsAsync()
        {
            var errorRows = PreviewRows
                .Where(r => r.Status != "Imported" && r.Status != "Pending")
                .ToList();

            if (errorRows.Count == 0)
            {
                System.Windows.MessageBox.Show("No error/skipped rows to export.",
                    "Export Errors", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title      = "Export Not-Imported Records",
                Filter     = "Excel Workbook|*.xlsx",
                FileName   = $"Import_Errors_{DateTime.Now:yyyyMMdd_HHmm}",
                DefaultExt = ".xlsx"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                await Task.Run(() =>
                {
                    using var wb = new XLWorkbook();
                    var ws = wb.Worksheets.Add("Not Imported");

                    // Header
                    var headers = new[] { "#", "IntId", "Name", "Gender", "D.O.B", "Caste",
                                          "Height", "Qualification", "District", "Phone", "Status", "Reason" };
                    for (int c = 1; c <= headers.Length; c++)
                    {
                        var cell = ws.Cell(1, c);
                        cell.Value = headers[c - 1];
                        cell.Style.Font.Bold         = true;
                        cell.Style.Font.FontColor    = XLColor.White;
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#C62828");
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }

                    for (int r = 0; r < errorRows.Count; r++)
                    {
                        var row = errorRows[r];
                        int er  = r + 2;

                        ws.Cell(er, 1).Value  = row.RowNum;
                        ws.Cell(er, 2).Value  = row.IntId;
                        ws.Cell(er, 3).Value  = row.Name;
                        ws.Cell(er, 4).Value  = row.Gender;
                        ws.Cell(er, 5).Value  = row.DateOfBirth;
                        ws.Cell(er, 6).Value  = row.Caste;
                        ws.Cell(er, 7).Value  = row.Height;
                        ws.Cell(er, 8).Value  = row.Qualification;
                        ws.Cell(er, 9).Value  = row.District;
                        ws.Cell(er, 10).Value = row.Phone1;
                        ws.Cell(er, 11).Value = row.Status;
                        ws.Cell(er, 12).Value = row.ErrorMessage ?? "";

                        // Highlight error rows red, skipped rows orange
                        var rowColor = row.Status.StartsWith("Skipped")
                            ? XLColor.FromHtml("#FFF3E0")
                            : XLColor.FromHtml("#FFEBEE");
                        ws.Row(er).Style.Fill.BackgroundColor = rowColor;

                        // Mark the reason cell bold-red
                        ws.Cell(er, 12).Style.Font.Bold      = true;
                        ws.Cell(er, 12).Style.Font.FontColor = XLColor.FromHtml("#C62828");

                        for (int c = 1; c <= headers.Length; c++)
                            ws.Cell(er, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }

                    ws.Columns().AdjustToContents();
                    foreach (var c in ws.ColumnsUsed())
                    {
                        if (c.Width > 50) c.Width = 50;
                        if (c.Width < 8)  c.Width = 8;
                    }

                    if (errorRows.Count > 0)
                        ws.RangeUsed()?.SetAutoFilter();

                    wb.SaveAs(dlg.FileName);
                });

                var open = System.Windows.MessageBox.Show(
                    $"Exported {errorRows.Count} not-imported record(s) to:\n{dlg.FileName}\n\nOpen the file now?",
                    "Export Successful",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information);

                if (open == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(
                            new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Export failed:\n{ex.Message}",
                    "Export Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
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
