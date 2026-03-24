using System.IO;
using ClosedXML.Excel;
using MarriageBureau.Models;

namespace MarriageBureau.Services
{
    /// <summary>
    /// Exports a list of Biodata records to an Excel workbook that matches
    /// the import format expected by ExcelImportViewModel.
    /// Records are split into MALE and FEMALE sheets, mirroring the import layout.
    /// </summary>
    public static class ExcelExportService
    {
        // Column headers in the exact order / names used by the importer
        private static readonly (string Header, Func<Biodata, string?> Getter)[] Columns =
        {
            ("S.NO.",                 null!),   // filled in by loop
            ("NAME",                  b => b.Name),
            ("GENDER",                b => b.Gender),
            ("CASTE",                 b => b.Caste),
            ("D.O.B",                 b => b.DateOfBirth),
            ("TIME OF BIRTH",         b => b.TimeOfBirth),
            ("AM/PM",                 b => b.AmPm),
            ("PLACE OF BIRTH",        b => b.PlaceOfBirth),
            ("HEIGHT",                b => b.Height),
            ("COMPLEXION",            b => b.Complexion),
            ("BIRTH STAR",            b => b.BirthStar),
            ("PADAM",                 b => b.Padam),
            ("RAASI",                 b => b.Raasi),
            ("RELIGION",              b => b.Religion),
            ("PATERNAL GOTRAM",       b => b.PaternalGotram),
            ("MATERNAL GOTRAM",       b => b.MaternalGotram),
            ("QUALIFICATION",         b => b.Qualification),
            ("DESIGNATION",           b => b.Designation),
            ("COMPANY",               b => b.CompanyAddress),
            ("FATHER NAME",           b => b.FatherName),
            ("FATHER OCCUPATION",     b => b.FatherOccupation),
            ("MOTHER NAME",           b => b.MotherName),
            ("MOTHER OCCUPATION",     b => b.MotherOccupation),
            ("NO.OF SIBLINGS",        b => b.NoOfSiblings),
            ("BROTHER",               b => b.BrotherCount),
            ("OCCUPATION",            b => b.BrotherOccupation),
            ("SISTER",                b => b.SisterCount),
            ("BROTHER IN LAW",        b => b.BrotherInLaw),
            ("GRAND FATHER NAME",     b => b.GrandFatherName),
            ("ELDER FATHER",          b => b.ElderFather),
            ("PHONE",                 b => b.ElderFatherPhone),
            ("D.NO.",                 b => b.DoorNumber),
            ("ADRESS",                b => b.AddressLine),
            ("TOWN/VILLAGE",          b => b.TownVillage),
            ("DISTRICT",              b => b.District),
            ("STATE",                 b => b.State),
            ("COUNTRY",               b => b.Country),
            ("PIN CODE",              b => b.PinCode),
            ("LIVING IN",             b => b.LivingIn),
            ("PH NO.1",               b => b.Phone1),
            ("PH NO.2",               b => b.Phone2),
            ("REFERENCES",            b => b.References),
            ("EXPECTATIONS FROM PARTNER", b => b.ExpectationsFromPartner),
        };

        /// <summary>
        /// Writes <paramref name="profiles"/> to <paramref name="outputPath"/> as an .xlsx file.
        /// Two worksheets are created: "MALE" and "FEMALE".
        /// </summary>
        public static void Export(IEnumerable<Biodata> profiles, string outputPath)
        {
            var male   = profiles.Where(p => p.Gender?.ToUpper() == "MALE").OrderBy(p => p.Name).ToList();
            var female = profiles.Where(p => p.Gender?.ToUpper() == "FEMALE").OrderBy(p => p.Name).ToList();
            // Any profile that is neither MALE nor FEMALE goes into a catch-all "OTHER" sheet
            var other  = profiles.Where(p => p.Gender?.ToUpper() != "MALE" && p.Gender?.ToUpper() != "FEMALE")
                                  .OrderBy(p => p.Name).ToList();

            using var wb = new XLWorkbook();

            WriteSheet(wb, "MALE",   male);
            WriteSheet(wb, "FEMALE", female);
            if (other.Count > 0)
                WriteSheet(wb, "OTHER", other);

            wb.SaveAs(outputPath);
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private static void WriteSheet(XLWorkbook wb, string sheetName, List<Biodata> rows)
        {
            var ws = wb.Worksheets.Add(sheetName);

            // ── Header row styling ────────────────────────────────────────
            var headerFill   = XLColor.FromHtml("#4A148C");
            var headerFont   = XLColor.White;

            for (int col = 1; col <= Columns.Length; col++)
            {
                var cell = ws.Cell(1, col);
                cell.Value = Columns[col - 1].Header;
                cell.Style.Font.Bold         = true;
                cell.Style.Font.FontColor    = headerFont;
                cell.Style.Fill.BackgroundColor = headerFill;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // Freeze the header row
            ws.SheetView.FreezeRows(1);

            // ── Data rows ─────────────────────────────────────────────────
            for (int r = 0; r < rows.Count; r++)
            {
                var biodata    = rows[r];
                int excelRow   = r + 2;  // 1-based, row 1 is header
                bool isEvenRow = r % 2 == 1;

                for (int col = 1; col <= Columns.Length; col++)
                {
                    var (header, getter) = Columns[col - 1];
                    var cell = ws.Cell(excelRow, col);

                    if (header == "S.NO.")
                    {
                        cell.Value = r + 1;
                    }
                    else
                    {
                        var value = getter?.Invoke(biodata) ?? string.Empty;
                        cell.Value = value ?? string.Empty;
                    }

                    // Alternate row shading
                    if (isEvenRow)
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F3E5F5");

                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CE93D8");
                    cell.Style.Alignment.WrapText   = false;
                }
            }

            // ── Auto-fit columns (cap at 40 chars wide) ───────────────────
            ws.Columns().AdjustToContents();
            foreach (var c in ws.ColumnsUsed())
            {
                if (c.Width > 40) c.Width = 40;
                if (c.Width < 8)  c.Width = 8;
            }

            // ── Auto-filter on header row ─────────────────────────────────
            if (rows.Count > 0)
                ws.RangeUsed()?.SetAutoFilter();
        }
    }
}
