using System.Configuration;
using System.Globalization;
using System.IO;
using MarriageBureau.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MarriageBureau.Services
{
    /// <summary>
    /// Generates a professional marriage biodata card as PDF and/or JPEG images.
    ///
    /// Layout rules (applies to BOTH PDF pages and rendered images):
    ///
    ///   Every page:
    ///     - A fixed-height transparent region at the TOP  (HeaderReservedPt)
    ///       and at the BOTTOM (FooterReservedPt) is left empty so that the
    ///       background image's printed header and footer are never obscured.
    ///     - The background image covers the entire A4 sheet.
    ///     - A diagonal watermark is drawn over the entire page.
    ///
    ///   Biodata page  (page 1):
    ///     - Profile Name (large) + ProfileId centred below the header space.
    ///     - Then all detail sections in single-column layout.
    ///     - Auto-switches to 2-column when non-empty field count > 22.
    ///
    ///   Photo pages (one per photo, appended after the biodata page):
    ///     - Profile Name + ProfileId centred below the header space (same as page 1).
    ///     - Photo centred in the remaining area (between name and footer reserve),
    ///       scaled down if it would overflow, kept at natural size otherwise.
    ///
    ///   Image export:
    ///     - Generates separate JPEG files:  baseName.jpg, baseName_photo_1.jpg, …
    ///       Each image has exactly the same layout as its PDF-page counterpart.
    /// </summary>
    public static class BiodataExportService
    {
        // ── Page geometry constants (in points, 1 pt = 1/72 inch) ────────────

        /// <summary>
        /// Points to reserve at the top of every page for the background
        /// template's printed header band.  Adjust to match your template.
        /// </summary>
        private const float HeaderReservedPt = 70f;

        /// <summary>
        /// Points to reserve at the bottom of every page for the background
        /// template's printed footer band.  Adjust to match your template.
        /// </summary>
        private const float FooterReservedPt = 40f;

        // ── Accent colours ───────────────────────────────────────────────────
        private const string AccentColor = "#800000";

        // ── Internal data types ──────────────────────────────────────────────
        private record FieldEntry(string Label, string Value);
        private record Section(string Title, List<FieldEntry> Fields);

        // ── Static ctor ──────────────────────────────────────────────────────
        static BiodataExportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // ════════════════════════════════════════════════════════════════════
        // Public API
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Generates and saves a multi-page PDF biodata document.</summary>
        public static void ExportToPdf(Biodata profile, List<string> photoPaths, string outputPath)
        {
            var businessName = LicenceService.BusinessName;
            var sections     = BuildSections(profile);

            var doc = Document.Create(container =>
            {
                // ── Page 1: biodata ──────────────────────────────────────
                container.Page(page => BuildPage(page, businessName,
                    content => ComposeBiodataContent(content, profile, sections)));

                // ── Pages 2+: one photo per page ─────────────────────────
                foreach (var photoPath in photoPaths)
                {
                    if (string.IsNullOrWhiteSpace(photoPath) || !File.Exists(photoPath))
                        continue;

                    // capture loop variable for lambda closure
                    var path = photoPath;
                    container.Page(page => BuildPage(page, businessName,
                        content => ComposePhotoContent(content, profile, path)));
                }
            });

            doc.GeneratePdf(outputPath);
        }

        /// <summary>
        /// Exports the biodata as multiple JPEG images (one per logical page).
        /// Returns all generated file paths.
        /// </summary>
        public static List<string> ExportToImages(Biodata profile, List<string> photoPaths, string baseOutputPath)
        {
            var businessName   = LicenceService.BusinessName;
            var sections       = BuildSections(profile);
            var generatedFiles = new List<string>();

            var dir            = Path.GetDirectoryName(baseOutputPath) ?? string.Empty;
            var nameWithoutExt = Path.GetFileNameWithoutExtension(baseOutputPath);
            var ext            = Path.GetExtension(baseOutputPath);

            // ── Biodata image ────────────────────────────────────────────
            var biodataDoc = Document.Create(container =>
                container.Page(page => BuildPage(page, businessName,
                    content => ComposeBiodataContent(content, profile, sections))));

            var biodataBytes = biodataDoc.GenerateImages(ImageSettings()).FirstOrDefault();
            if (biodataBytes != null)
            {
                File.WriteAllBytes(baseOutputPath, biodataBytes);
                generatedFiles.Add(baseOutputPath);
            }

            // ── One image per photo ──────────────────────────────────────
            int idx = 1;
            foreach (var photoPath in photoPaths)
            {
                if (string.IsNullOrWhiteSpace(photoPath) || !File.Exists(photoPath))
                    continue;

                var path = photoPath;
                var photoDoc = Document.Create(container =>
                    container.Page(page => BuildPage(page, businessName,
                        content => ComposePhotoContent(content, profile, path))));

                var photoBytes = photoDoc.GenerateImages(ImageSettings()).FirstOrDefault();
                if (photoBytes != null)
                {
                    var outPath = Path.Combine(dir, $"{nameWithoutExt}_photo_{idx}{ext}");
                    File.WriteAllBytes(outPath, photoBytes);
                    generatedFiles.Add(outPath);
                }

                idx++;
            }

            return generatedFiles;
        }

        /// <summary>Backward-compatible single-call image export (calls ExportToImages internally).</summary>
        public static void ExportToImage(Biodata profile, List<string> photoPaths, string outputPath)
            => ExportToImages(profile, photoPaths, outputPath);

        // ════════════════════════════════════════════════════════════════════
        // Page builder – shared skeleton for every page
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Configures a QuestPDF page with:
        ///   background → full-bleed template image (or nothing if not configured)
        ///   content    → padded inner area respecting header + footer reserves
        ///   foreground → diagonal watermark
        /// The <paramref name="composeContent"/> delegate receives the inner
        /// content container and is responsible for filling it.
        /// </summary>
        private static void BuildPage(
            PageDescriptor page,
            string? businessName,
            Action<IContainer> composeContent)
        {
            page.Size(PageSizes.A4);
            page.Margin(0);
            page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

            // Full-bleed background image
            page.Background().Element(DrawBackground);

            // Content area: outer horizontal padding + top/bottom reserves
            page.Content()
                .PaddingHorizontal(24, Unit.Point)
                .PaddingTop(HeaderReservedPt, Unit.Point)
                .PaddingBottom(FooterReservedPt, Unit.Point)
                .Element(composeContent);

            // Diagonal watermark over everything
            page.Foreground().Element(fg => DrawWatermark(fg, businessName));
        }

        // ════════════════════════════════════════════════════════════════════
        // Biodata page content
        // ════════════════════════════════════════════════════════════════════

        private static void ComposeBiodataContent(IContainer root, Biodata p, List<Section> sections)
        {
            // Count non-empty fields to decide layout
            int totalFields  = sections.Sum(s => s.Fields.Count(f => !string.IsNullOrWhiteSpace(f.Value)));
            bool useTwoCol   = totalFields > 17;

            root.Column(col =>
            {
                // ── Name + ProfileId header (same as photo pages) ────────
                RenderPageNameHeader(col, p);

                col.Item().PaddingVertical(6);

                // ── Detail sections ──────────────────────────────────────
                if (useTwoCol)
                    RenderTwoColumns(col, sections);
                else
                    RenderSingleColumn(col, sections);
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // Photo page content
        // ════════════════════════════════════════════════════════════════════

        private static void ComposePhotoContent(IContainer root, Biodata p, string photoPath)
        {
            root.Column(col =>
            {
                // ── Name + ProfileId header ──────────────────────────────
                RenderPageNameHeader(col, p);

                col.Item().PaddingVertical(4);

                // ── Photo centred in remaining space ─────────────────────
                // We use Extend() so QuestPDF stretches the cell to fill the
                // remaining height, then AlignCenter/AlignMiddle centres the image.
                col.Item().Extend().AlignCenter().AlignMiddle().Element(inner =>
                {
                    try
                    {
                        // FitArea: scales image to fit the available area
                        // while preserving aspect ratio; never upscales beyond
                        // the natural size of the image.
                        inner.Image(photoPath).FitArea();
                    }
                    catch
                    {
                        inner.Text("[ Photo unavailable ]")
                             .FontSize(14)
                             .FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                    }
                });
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // Shared: Name + ProfileId banner (top of every page)
        // ════════════════════════════════════════════════════════════════════

        private static void RenderPageNameHeader(ColumnDescriptor col, Biodata p)
        {
            // Name row
            col.Item()
               .PaddingTop(10)
               .AlignCenter()
               .Text(p.Name.ToUpper())
               .FontSize(24)
               .FontColor(AccentColor)
               .Bold();

            // ProfileId row (only if set)
            if (!string.IsNullOrWhiteSpace(p.ProfileId))
            {
                col.Item()
                   .AlignCenter()
                   .Text($"TMID: {p.ProfileId}")
                   .FontSize(11)
                   .FontColor(AccentColor);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Single-column layout
        // ════════════════════════════════════════════════════════════════════

        private static void RenderSingleColumn(ColumnDescriptor col, List<Section> sections)
        {
            foreach (var section in sections)
            {
                var fields = section.Fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToList();
                if (fields.Count == 0) continue;

                col.Item().PaddingTop(6);

                // Centred section heading
                col.Item()
                   .Background(AccentColor)
                   .PaddingHorizontal(4)
                   .PaddingVertical(3)
                   .AlignCenter()
                   .Text(section.Title)
                   .FontSize(14)
                   .Bold()
                   .FontColor(QuestPDF.Helpers.Colors.White);

                col.Item().PaddingTop(3);

                foreach (var f in fields)
                    RenderFieldRow(col, f.Label, f.Value, 120, 14);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Two-column layout
        // ════════════════════════════════════════════════════════════════════

        private static void RenderTwoColumns(ColumnDescriptor col, List<Section> sections)
        {
            int totalFields = sections.Sum(s => s.Fields.Count(f => !string.IsNullOrWhiteSpace(f.Value)));
            int half        = ((totalFields + 1) / 2) + 2;

            var leftSections  = new List<Section>();
            var rightSections = new List<Section>();
            int accumulated   = 0;
            bool leftFull     = false;

            foreach (var section in sections)
            {
                int count = section.Fields.Count(f => !string.IsNullOrWhiteSpace(f.Value));
                if (count == 0) continue;

                if (!leftFull)
                {
                    leftSections.Add(section);
                    accumulated += count;
                    if (accumulated >= half) leftFull = true;
                }
                else
                {
                    rightSections.Add(section);
                }
            }

            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c => RenderColumnSections(c, leftSections,  labelWidth: 95));
                row.ConstantItem(14);
                row.RelativeItem().Column(c => RenderColumnSections(c, rightSections, labelWidth: 95));
            });
        }

        private static void RenderColumnSections(
            ColumnDescriptor col, List<Section> sections, int labelWidth)
        {
            foreach (var section in sections)
            {
                var fields = section.Fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToList();
                if (fields.Count == 0) continue;

                col.Item().PaddingTop(6);

                col.Item()
                   .Background(AccentColor)
                   .PaddingHorizontal(4)
                   .PaddingVertical(3)
                   .AlignCenter()
                   .Text(section.Title)
                   .FontSize(12)
                   .Bold()
                   .FontColor(QuestPDF.Helpers.Colors.White);

                col.Item().PaddingTop(2);

                foreach (var f in fields)
                    RenderFieldRow(col, f.Label, f.Value, labelWidth, fontSize: 12);
            }
        }

        // ── Generic field row ────────────────────────────────────────────────

        private static void RenderFieldRow(
            ColumnDescriptor col, string label, string value, int labelWidth, int fontSize)
        {
            col.Item().PaddingLeft(2).PaddingBottom(2).Row(row =>
            {
                row.ConstantItem(labelWidth)
                   .Text(label + " :")
                   .FontSize(fontSize)
                   .FontColor(AccentColor);

                row.RelativeItem()
                   .Text(value)
                   .FontSize(fontSize)
                   .FontColor(AccentColor)
                   .Bold();
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // Sections data builder
        // ════════════════════════════════════════════════════════════════════

        private static List<Section> BuildSections(Biodata p)
        {
            var addr = string.Join(", ",
                new[] { p.DoorNumber, p.AddressLine, p.TownVillage, p.District, p.State, p.PinCode }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            return new List<Section>
            {
                new("Personal Information", new List<FieldEntry>
                {
                    new("Date of Birth",   p.DateOfBirth  ?? ""),
                    new("Time of Birth",   $"{p.TimeOfBirth} {p.AmPm}".Trim()),
                    new("Place of Birth",  p.PlaceOfBirth ?? ""),
                    new("Height",          p.Height       ?? ""),
                    new("Complexion",      p.Complexion   ?? ""),
                    new("Religion",        p.Religion     ?? ""),
                }),
                new("Horoscope", new List<FieldEntry>
                {
                    new("Birth Star (Nakshatra)", p.BirthStar     ?? ""),
                    new("Padam",                  p.Padam         ?? ""),
                    new("Raasi (Rashi)",           p.Raasi         ?? ""),
                    new("Paternal Gotram",         p.PaternalGotram ?? ""),
                    new("Maternal Gotram",         p.MaternalGotram ?? ""),
                }),
                new("Education & Career", new List<FieldEntry>
                {
                    new("Qualification",     p.Qualification  ?? ""),
                    new("Designation",       p.Designation    ?? ""),
                    new("Company / Address", p.CompanyAddress ?? ""),
                }),
                new("Family Information", new List<FieldEntry>
                {
                    new("Father's Name",       p.FatherName       ?? ""),
                    new("Father's Occupation", p.FatherOccupation ?? ""),
                    new("Mother's Name",       p.MotherName       ?? ""),
                    new("Mother's Occupation", p.MotherOccupation ?? ""),
                    new("Brothers",            p.BrotherCount     ?? ""),
                    new("Brother's Name",      p.BrotherOccupation ?? ""),
                    new("Sisters",             p.SisterCount      ?? ""),
                    new("Sister's Name",       p.SisterOccupation ?? ""),
                    new("Grand Father",        p.GrandFatherName  ?? ""),
                    new("Uncle",        p.ElderFather      ?? ""),
                }),
                new("Address & Contact", new List<FieldEntry>
                {
                    new("Address",    addr),
                    new("Living In",  p.LivingIn  ?? ""),
                    new("Phone 1",    p.Phone1    ?? ""),
                    new("Phone 2",    p.Phone2    ?? ""),
                    new("References", p.References ?? ""),
                }),
            };
        }

        // ════════════════════════════════════════════════════════════════════
        // Background & watermark
        // ════════════════════════════════════════════════════════════════════

        private static void DrawBackground(IContainer c)
        {
            var bgPath = ConfigurationManager.AppSettings["BgImagePath"];
            if (!string.IsNullOrWhiteSpace(bgPath) && File.Exists(bgPath))
            {
                var img = QuestPDF.Infrastructure.Image.FromFile(bgPath);
                c.Extend().Image(img).FitUnproportionally();
            }
        }

        private static void DrawWatermark(IContainer fg, string? businessName)
        {
            if (string.IsNullOrWhiteSpace(businessName)) return;
            fg.Extend().Svg(BuildWatermarkSvg(PageSizes.A4.Width, PageSizes.A4.Height, businessName));
        }

        private static string BuildWatermarkSvg(float width, float height, string text)
        {
            text = EscapeXml(text);
            var alpha = (35f / 255f).ToString(CultureInfo.InvariantCulture);
            var cx    = (width  / 2f).ToString(CultureInfo.InvariantCulture);
            var cy    = (height / 2f).ToString(CultureInfo.InvariantCulture);

            string Y(float v) => v.ToString(CultureInfo.InvariantCulture);
            var y1 = Y(-height * 0.25f);
            var y2 = Y(0);
            var y3 = Y(height * 0.25f);
            var w  = width .ToString(CultureInfo.InvariantCulture);
            var h  = height.ToString(CultureInfo.InvariantCulture);

            return $@"<svg xmlns='http://www.w3.org/2000/svg' width='{w}' height='{h}' viewBox='0 0 {w} {h}'>
  <g transform='translate({cx} {cy}) rotate(-35)'>
    <text x='0' y='{y1}' text-anchor='middle' font-family='Arial' font-size='48' font-weight='700'
          fill='rgb(160,120,200)' fill-opacity='{alpha}'>{text}</text>
    <text x='0' y='{y2}' text-anchor='middle' font-family='Arial' font-size='48' font-weight='700'
          fill='rgb(160,120,200)' fill-opacity='{alpha}'>{text}</text>
    <text x='0' y='{y3}' text-anchor='middle' font-family='Arial' font-size='48' font-weight='700'
          fill='rgb(160,120,200)' fill-opacity='{alpha}'>{text}</text>
  </g>
</svg>".Trim();
        }

        private static string EscapeXml(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&apos;");
        }

        // ── Image generation settings ────────────────────────────────────────

        private static ImageGenerationSettings ImageSettings() => new()
        {
            RasterDpi              = 150,
            ImageFormat            = ImageFormat.Jpeg,
            ImageCompressionQuality = ImageCompressionQuality.High
        };
    }
}
