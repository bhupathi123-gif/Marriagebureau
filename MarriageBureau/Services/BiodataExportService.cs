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
    /// Generates a professional marriage biodata card as PDF and/or PNG image.
    /// Layout:
    ///   Page 1 – header with name, then all detail sections in single-column
    ///            (auto-splits to 2-column if content would overflow one page).
    ///   Remaining pages – one photo per page, centred, scaled to fit if needed.
    /// A diagonal watermark with the business name is drawn on every page.
    /// </summary>
    public static class BiodataExportService
    {
        static BiodataExportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // ── Data model for a single field row ────────────────────────────────

        private record FieldEntry(string Label, string Value);
        private record Section(string Title, List<FieldEntry> Fields);

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Generates and saves a multi-page PDF biodata card.</summary>
        public static void ExportToPdf(Biodata profile, List<string> photoPaths, string outputPath)
        {
            var businessName = LicenceService.BusinessName;
            var sections = BuildSections(profile);

            var doc = Document.Create(container =>
            {
                // ── Biodata page ─────────────────────────────────────────
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                    page.Background().Element(bg => DrawBackground(bg));

                    page.Content()
                        .Padding(24, Unit.Point)
                        .Element(c => ComposeBiodataContent(c, profile, sections, businessName));

                    page.Foreground().Element(fg => DrawWatermark(fg, businessName));
                });

                // ── One page per photo ────────────────────────────────────
                foreach (var photoPath in photoPaths)
                {
                    if (string.IsNullOrWhiteSpace(photoPath) || !File.Exists(photoPath))
                        continue;

                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(0);
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                        page.Background().Element(bg => DrawBackground(bg));

                        page.Content().Element(c => ComposePhotoPage(c, photoPath));

                        page.Foreground().Element(fg => DrawWatermark(fg, businessName));
                    });
                }
            });

            doc.GeneratePdf(outputPath);
        }

        /// <summary>
        /// Exports the biodata as images:
        ///   - [baseName].jpg  → biodata page
        ///   - [baseName]_photo_1.jpg, [baseName]_photo_2.jpg, … → one image per photo
        /// Returns the list of all generated file paths.
        /// </summary>
        public static List<string> ExportToImages(Biodata profile, List<string> photoPaths, string baseOutputPath)
        {
            var businessName = LicenceService.BusinessName;
            var sections = BuildSections(profile);
            var generatedFiles = new List<string>();

            // ── Biodata image ────────────────────────────────────────────────
            var biodataDoc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                    page.Background().Element(bg => DrawBackground(bg));

                    page.Content()
                        .Padding(24, Unit.Point)
                        .Element(c => ComposeBiodataContent(c, profile, sections, businessName));

                    page.Foreground().Element(fg => DrawWatermark(fg, businessName));
                });
            });

            var biodataImages = biodataDoc.GenerateImages(new ImageGenerationSettings
            {
                RasterDpi = 150,
                ImageFormat = ImageFormat.Jpeg,
                ImageCompressionQuality = ImageCompressionQuality.High
            });

            if (biodataImages.Any())
            {
                File.WriteAllBytes(baseOutputPath, biodataImages.First());
                generatedFiles.Add(baseOutputPath);
            }

            // ── One image per photo ──────────────────────────────────────────
            var dir = Path.GetDirectoryName(baseOutputPath) ?? string.Empty;
            var nameWithoutExt = Path.GetFileNameWithoutExtension(baseOutputPath);
            var ext = Path.GetExtension(baseOutputPath);

            int photoIndex = 1;
            foreach (var photoPath in photoPaths)
            {
                if (string.IsNullOrWhiteSpace(photoPath) || !File.Exists(photoPath))
                    continue;

                var photoDoc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(0);
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                        page.Background().Element(bg => DrawBackground(bg));
                        page.Content().Element(c => ComposePhotoPage(c, photoPath));
                        page.Foreground().Element(fg => DrawWatermark(fg, businessName));
                    });
                });

                var photoImages = photoDoc.GenerateImages(new ImageGenerationSettings
                {
                    RasterDpi = 150,
                    ImageFormat = ImageFormat.Jpeg,
                    ImageCompressionQuality = ImageCompressionQuality.High
                });

                if (photoImages.Any())
                {
                    var photoOutputPath = Path.Combine(dir, $"{nameWithoutExt}_photo_{photoIndex}{ext}");
                    File.WriteAllBytes(photoOutputPath, photoImages.First());
                    generatedFiles.Add(photoOutputPath);
                }

                photoIndex++;
            }

            return generatedFiles;
        }

        // kept for backward compatibility – single-image export (biodata page only)
        public static void ExportToImage(Biodata profile, List<string> photoPaths, string outputPath)
        {
            ExportToImages(profile, photoPaths, outputPath);
        }

        // ── Background / Watermark ───────────────────────────────────────────

        private static void DrawBackground(IContainer c)
        {
            var bgPath = ConfigurationManager.AppSettings["BgImagePath"];

            if (!string.IsNullOrWhiteSpace(bgPath) && File.Exists(bgPath))
            {
                var img = QuestPDF.Infrastructure.Image.FromFile(bgPath);
                c.Extend().Image(img).FitUnproportionally();
                return;
            }
        }

        private static void DrawWatermark(IContainer fg, string? businessName)
        {
            if (string.IsNullOrWhiteSpace(businessName)) return;
            var svg = BuildWatermarkSvg(PageSizes.A4.Width, PageSizes.A4.Height, businessName);
            fg.Extend().Svg(svg);
        }

        private static string BuildWatermarkSvg(float width, float height, string text)
        {
            text = EscapeXml(text);

            var alpha = (35f / 255f).ToString(CultureInfo.InvariantCulture);
            var cx = (width / 2f).ToString(CultureInfo.InvariantCulture);
            var cy = (height / 2f).ToString(CultureInfo.InvariantCulture);

            string y(float v) => v.ToString(CultureInfo.InvariantCulture);

            var y1 = y(-height * 0.25f);
            var y2 = y(0);
            var y3 = y(height * 0.25f);

            var w = width.ToString(CultureInfo.InvariantCulture);
            var h = height.ToString(CultureInfo.InvariantCulture);

            return $@"
<svg xmlns='http://www.w3.org/2000/svg' width='{w}' height='{h}' viewBox='0 0 {w} {h}'>
  <g transform='translate({cx} {cy}) rotate(-35)'>
    <text x='0' y='{y1}' text-anchor='middle'
          font-family='Arial' font-size='48' font-weight='700'
          fill='rgb(160,120,200)' fill-opacity='{alpha}'>{text}</text>
    <text x='0' y='{y2}' text-anchor='middle'
          font-family='Arial' font-size='48' font-weight='700'
          fill='rgb(160,120,200)' fill-opacity='{alpha}'>{text}</text>
    <text x='0' y='{y3}' text-anchor='middle'
          font-family='Arial' font-size='48' font-weight='700'
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

        // ── Photo page ───────────────────────────────────────────────────────

        /// <summary>
        /// Renders a single photo centred on the page.
        /// If the photo is smaller than the page area it is shown at natural size;
        /// if it is larger it is scaled down to fit while preserving aspect ratio.
        /// </summary>
        private static void ComposePhotoPage(IContainer root, string photoPath)
        {
            root.Extend()          // fill the whole page area
                .AlignCenter()
                .AlignMiddle()
                .Element(inner =>
                {
                    try
                    {
                        inner.Image(photoPath).FitArea();
                    }
                    catch
                    {
                        inner.Text("[ Photo unavailable ]")
                             .FontSize(14)
                             .FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                    }
                });
        }

        // ── Biodata sections data builder ────────────────────────────────────

        private static List<Section> BuildSections(Biodata p)
        {
            var addr = string.Join(", ",
                new[] { p.DoorNumber, p.AddressLine, p.TownVillage, p.District, p.State, p.PinCode }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            return new List<Section>
            {
                new Section("Personal Information", new List<FieldEntry>
                {
                    new("Date of Birth",   p.DateOfBirth ?? ""),
                    new("Time of Birth",   $"{p.TimeOfBirth} {p.AmPm}".Trim()),
                    new("Place of Birth",  p.PlaceOfBirth ?? ""),
                    new("Height",          p.Height ?? ""),
                    new("Complexion",      p.Complexion ?? ""),
                    new("Religion",        p.Religion ?? ""),
                }),
                new Section("Horoscope", new List<FieldEntry>
                {
                    new("Birth Star (Nakshatra)", p.BirthStar ?? ""),
                    new("Padam",                  p.Padam ?? ""),
                    new("Raasi (Rashi)",           p.Raasi ?? ""),
                    new("Paternal Gotram",         p.PaternalGotram ?? ""),
                    new("Maternal Gotram",         p.MaternalGotram ?? ""),
                }),
                new Section("Education & Career", new List<FieldEntry>
                {
                    new("Qualification",     p.Qualification ?? ""),
                    new("Designation",       p.Designation ?? ""),
                    new("Company / Address", p.CompanyAddress ?? ""),
                }),
                new Section("Family Information", new List<FieldEntry>
                {
                    new("Father's Name",       p.FatherName ?? ""),
                    new("Father's Occupation", p.FatherOccupation ?? ""),
                    new("Mother's Name",       p.MotherName ?? ""),
                    new("Mother's Occupation", p.MotherOccupation ?? ""),
                    new("Brothers",            p.BrotherCount ?? ""),
                    new("Brother's Name",      p.BrotherOccupation ?? ""),
                    new("Sisters",             p.SisterCount ?? ""),
                    new("Sister's Name",       p.SisterOccupation ?? ""),
                    new("Grand Father",        p.GrandFatherName ?? ""),
                    new("Elder Father",        p.ElderFather ?? ""),
                }),
                new Section("Address & Contact", new List<FieldEntry>
                {
                    new("Address",    addr),
                    new("Living In",  p.LivingIn ?? ""),
                    new("Phone 1",    p.Phone1 ?? ""),
                    new("Phone 2",    p.Phone2 ?? ""),
                    new("References", p.References ?? ""),
                }),
            };
        }

        // ── Biodata page composer ────────────────────────────────────────────

        /// <summary>
        /// Tries single-column first.  QuestPDF will automatically paginate if
        /// content overflows; we compose with 2-column layout to keep everything
        /// on one page whenever possible.
        ///
        /// Strategy: estimate total field count; if > threshold use 2-col.
        /// QuestPDF itself handles overflow pagination, so single-col is safe
        /// as well — the "two column" path is used when the profile is dense.
        /// </summary>
        private static void ComposeBiodataContent(
            IContainer root, Biodata p, List<Section> sections, string businessName)
        {
            const string accentColor = "#800000";

            // Count non-empty fields to decide layout
            int totalFields = sections.Sum(s => s.Fields.Count(f => !string.IsNullOrWhiteSpace(f.Value)));

            // Threshold: > 22 non-empty fields → use two-column layout
            bool useTwoColumn = totalFields > 22;

            root.Column(col =>
            {
                // ── Name header ──────────────────────────────────────────
                col.Item().PaddingVertical(50).AlignCenter()
                   .Text(p.Name.ToUpper())
                   .FontSize(28)
                   .FontColor(accentColor)
                   .Bold();

                col.Item().PaddingVertical(4);

                // ── Detail sections ──────────────────────────────────────
                if (useTwoColumn)
                {
                    RenderTwoColumns(col, sections, accentColor);
                }
                else
                {
                    RenderSingleColumn(col, sections, accentColor);
                }
            });
        }

        // ── Single-column layout ─────────────────────────────────────────────

        private static void RenderSingleColumn(
            ColumnDescriptor col, List<Section> sections, string accentColor)
        {
            foreach (var section in sections)
            {
                var fields = section.Fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToList();
                if (fields.Count == 0) continue;

                col.Item().PaddingTop(6);
                // Category heading – centred
                col.Item()
                   .Background(accentColor)
                   .PaddingHorizontal(4)
                   .PaddingVertical(3)
                   .AlignCenter()
                   .Text(section.Title)
                   .FontSize(10)
                   .Bold()
                   .FontColor(QuestPDF.Helpers.Colors.White);

                col.Item().PaddingTop(3);

                foreach (var field in fields)
                    RenderFieldRow(col, field.Label, field.Value, accentColor);
            }
        }

        // ── Two-column layout ────────────────────────────────────────────────

        private static void RenderTwoColumns(
            ColumnDescriptor col, List<Section> sections, string accentColor)
        {
            // Split sections roughly in half by field count
            int totalFields = sections.Sum(s => s.Fields.Count(f => !string.IsNullOrWhiteSpace(f.Value)));
            int half = (totalFields + 1) / 2;

            var leftSections = new List<Section>();
            var rightSections = new List<Section>();
            int accumulated = 0;
            bool leftFull = false;

            foreach (var section in sections)
            {
                int sectionCount = section.Fields.Count(f => !string.IsNullOrWhiteSpace(f.Value));
                if (sectionCount == 0) continue;

                if (!leftFull)
                {
                    leftSections.Add(section);
                    accumulated += sectionCount;
                    if (accumulated >= half)
                        leftFull = true;
                }
                else
                {
                    rightSections.Add(section);
                }
            }

            col.Item().Row(row =>
            {
                // Left column
                row.RelativeItem().Column(leftCol =>
                {
                    foreach (var section in leftSections)
                    {
                        var fields = section.Fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToList();
                        if (fields.Count == 0) continue;

                        leftCol.Item().PaddingTop(6);
                        leftCol.Item()
                               .Background(accentColor)
                               .PaddingHorizontal(4)
                               .PaddingVertical(3)
                               .AlignCenter()
                               .Text(section.Title)
                               .FontSize(9)
                               .Bold()
                               .FontColor(QuestPDF.Helpers.Colors.White);

                        leftCol.Item().PaddingTop(2);

                        foreach (var field in fields)
                            RenderFieldRowCompact(leftCol, field.Label, field.Value, accentColor);
                    }
                });

                row.ConstantItem(14);

                // Right column
                row.RelativeItem().Column(rightCol =>
                {
                    foreach (var section in rightSections)
                    {
                        var fields = section.Fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToList();
                        if (fields.Count == 0) continue;

                        rightCol.Item().PaddingTop(6);
                        rightCol.Item()
                                .Background(accentColor)
                                .PaddingHorizontal(4)
                                .PaddingVertical(3)
                                .AlignCenter()
                                .Text(section.Title)
                                .FontSize(9)
                                .Bold()
                                .FontColor(QuestPDF.Helpers.Colors.White);

                        rightCol.Item().PaddingTop(2);

                        foreach (var field in fields)
                            RenderFieldRowCompact(rightCol, field.Label, field.Value, accentColor);
                    }
                });
            });
        }

        // ── Field row renderers ──────────────────────────────────────────────

        private static void RenderFieldRow(
            ColumnDescriptor col, string label, string value, string accentColor)
        {
            col.Item().PaddingBottom(2).Row(row =>
            {
                row.ConstantItem(120).Text(label + " :")
                   .FontSize(11).FontColor(accentColor);
                row.RelativeItem().Text(value)
                   .FontColor(accentColor).FontSize(11).Bold();
            });
        }

        private static void RenderFieldRowCompact(
            ColumnDescriptor col, string label, string value, string accentColor)
        {
            col.Item().PaddingBottom(2).Row(row =>
            {
                row.ConstantItem(100).Text(label + " :")
                   .FontSize(9).FontColor(accentColor);
                row.RelativeItem().Text(value)
                   .FontColor(accentColor).FontSize(9).Bold();
            });
        }
    }
}
