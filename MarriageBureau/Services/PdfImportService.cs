using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using MarriageBureau.Models;

namespace MarriageBureau.Services
{
    /// <summary>
    /// Extracts biodata fields from a PDF file using pattern-matching on the raw text.
    /// Works best with typed/digital PDFs (not scanned images).
    /// </summary>
    public static class PdfImportService
    {
        /// <summary>
        /// Reads the PDF, extracts all text, and attempts to populate a Biodata object
        /// by matching common label patterns found in marriage biodata PDFs.
        /// Returns a partially-filled Biodata that the user can review and complete.
        /// </summary>
        public static (Biodata Biodata, string RawText) ExtractFromPdf(string pdfPath)
        {
            string rawText = ExtractText(pdfPath);
            var biodata    = ParseText(rawText);
            return (biodata, rawText);
        }

        // ── Text Extraction ─────────────────────────────────────────────────

        private static string ExtractText(string path)
        {
            var sb = new StringBuilder();
            using var reader = new PdfReader(path);
            using var doc    = new PdfDocument(reader);

            for (int i = 1; i <= doc.GetNumberOfPages(); i++)
            {
                var strategy = new LocationTextExtractionStrategy();
                string pageText = PdfTextExtractor.GetTextFromPage(doc.GetPage(i), strategy);
                sb.AppendLine(pageText);
            }
            return sb.ToString();
        }

        // ── Field Parsing ───────────────────────────────────────────────────

        private static Biodata ParseText(string text)
        {
            var bio = new Biodata();

            // Normalise: replace multiple spaces/tabs with single space
            text = Regex.Replace(text, @"[ \t]+", " ");

            // ── Helper: grab value after label, up to next newline or known label ──
            string? Field(string label, string? fallbackLabel = null)
            {
                var patterns = new List<string> { Regex.Escape(label) };
                if (fallbackLabel != null) patterns.Add(Regex.Escape(fallbackLabel));

                foreach (var pat in patterns)
                {
                    var m = Regex.Match(text,
                        pat + @"\s*[:\-–]?\s*([^\n\r:]{1,120})",
                        RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        var val = m.Groups[1].Value.Trim().TrimEnd(',', '.', ';');
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
                return null;
            }

            // ── Name — try to grab the most prominent line near "NAME" ──
            bio.Name       = Field("Name", "Full Name") ?? ExtractLikelyName(text);
            bio.Gender     = NormaliseGender(Field("Gender", "Sex"));
            bio.Caste      = Field("Caste", "Community");
            bio.DateOfBirth = Field("Date of Birth", "D.O.B") ?? Field("DOB");
            bio.TimeOfBirth = Field("Time of Birth", "Birth Time");
            bio.AmPm       = Field("AM/PM", "AM / PM");
            bio.PlaceOfBirth = Field("Place of Birth", "Birth Place");
            bio.Height     = Field("Height");
            bio.Complexion = Field("Complexion", "Colour") ?? Field("Skin");
            bio.BirthStar  = Field("Birth Star", "Nakshatra") ?? Field("Star");
            bio.Padam      = Field("Padam", "Pada");
            bio.Raasi      = Field("Raasi", "Rashi") ?? Field("Zodiac");
            bio.Religion   = Field("Religion");
            bio.PaternalGotram = Field("Paternal Gotram", "Father Gotram") ?? Field("Gotram");
            bio.MaternalGotram = Field("Maternal Gotram", "Mother Gotram");
            bio.Qualification  = Field("Qualification", "Education") ?? Field("Degree");
            bio.Designation    = Field("Designation", "Job Title") ?? Field("Occupation", "Profession");
            bio.CompanyAddress = Field("Company", "Name of Company") ?? Field("Employer");
            bio.FatherName     = Field("Father Name", "Father's Name");
            bio.FatherOccupation = Field("Father Occupation", "Father's Occupation");
            bio.MotherName     = Field("Mother Name", "Mother's Name");
            bio.MotherOccupation = Field("Mother Occupation", "Mother's Occupation");
            bio.BrotherCount   = Field("Brother", "No. of Brothers");
            bio.SisterCount    = Field("Sister", "No. of Sisters");
            bio.GrandFatherName = Field("Grand Father", "Grandfather");
            bio.DoorNumber     = Field("D.No", "Door No") ?? Field("Flat No");
            bio.AddressLine    = Field("Address", "Street");
            bio.TownVillage    = Field("Town", "Village");
            bio.District       = Field("District");
            bio.State          = Field("State");
            bio.Country        = Field("Country");
            bio.PinCode        = Field("Pin Code", "PIN") ?? Field("Zip");
            bio.Phone1         = Field("Phone 1", "Ph No.1") ?? Field("Mobile", "Contact No");
            bio.Phone2         = Field("Phone 2", "Ph No.2") ?? Field("Alternate");
            bio.References     = Field("Reference", "References");
            bio.ExpectationsFromPartner = Field("Expectations", "Partner Expectations")
                                       ?? Field("Looking for");

            bio.CreatedAt = DateTime.Now;
            bio.UpdatedAt = DateTime.Now;
            return bio;
        }

        // ── Try to find the main name from the first lines of the document ──
        private static string? ExtractLikelyName(string text)
        {
            // Look for the first all-caps line with at least 2 words (likely a name)
            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length < 4 || trimmed.Length > 80) continue;
                if (Regex.IsMatch(trimmed, @"^[A-Z][A-Z\s]+$") && trimmed.Contains(' '))
                    return ToTitleCase(trimmed);
            }
            return null;
        }

        private static string? NormaliseGender(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var upper = raw.ToUpper();
            if (upper.Contains("FEMALE") || upper.Contains("GIRL") || upper.Contains("WOMAN"))
                return "FEMALE";
            if (upper.Contains("MALE") || upper.Contains("BOY") || upper.Contains("MAN"))
                return "MALE";
            return raw.Trim();
        }

        private static string ToTitleCase(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLower());
        }
    }
}
