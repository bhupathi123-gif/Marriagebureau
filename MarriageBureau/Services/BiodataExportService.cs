using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MarriageBureau.Models;
using MarriageBureau.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;

namespace MarriageBureau.Services
{
    /// <summary>
    /// Generates a professional marriage biodata card as PDF and/or PNG image.
    /// Layout: header with name+gender badge, two photos side-by-side, then all detail sections.
    /// A diagonal watermark with the business name is drawn on the page.
    /// </summary>
    public static class BiodataExportService
    {
        static BiodataExportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // ── Public API ──────────────────────────────────────────────────────

        /// <summary>Generates and saves a PDF biodata card to <paramref name="outputPath"/>.</summary>
        public static void ExportToPdf(Biodata profile, List<string> photoPaths, string outputPath)
        {
            var businessName = LicenceService.BusinessName;
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(24, Unit.Point);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));
                    page.Content().Element(c => ComposeBiodata(c, profile, photoPaths, businessName));

                    // Watermark on foreground
                    page.Foreground().Element(container =>
                    {
                        if (string.IsNullOrWhiteSpace(businessName))
                            return;

                        var svg = BuildWatermarkSvg(PageSizes.A4.Width, PageSizes.A4.Height, businessName);

                        container
                            .Width(PageSizes.A4.Width)
                            .Height(PageSizes.A4.Height)
                            .Svg(svg);
                    });

                });
            });
            doc.GeneratePdf(outputPath);
        }

        /// <summary>Renders the first page of the PDF as a PNG/JPEG image.</summary>
        public static void ExportToImage(Biodata profile, List<string> photoPaths, string outputPath)
        {
            var businessName = LicenceService.BusinessName;
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(24, Unit.Point);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));
                    page.Content().Element(c => ComposeBiodata(c, profile, photoPaths, businessName));

                    // Watermark on foreground
                    page.Foreground().Element(container =>
                    {
                        if (string.IsNullOrWhiteSpace(businessName))
                            return;

                        var svg = BuildWatermarkSvg(PageSizes.A4.Width, PageSizes.A4.Height, businessName);

                        container
                            .Width(PageSizes.A4.Width)
                            .Height(PageSizes.A4.Height)
                            .Svg(svg);
                    });
                });
            });

            var images = doc.GenerateImages(new ImageGenerationSettings
            {
                RasterDpi = 150,
                ImageFormat = ImageFormat.Jpeg,
                ImageCompressionQuality = ImageCompressionQuality.High
            });

            if (images.Any())
                File.WriteAllBytes(outputPath, images.First());
        }

        // ── Watermark ───────────────────────────────────────────────────────
     


private static string BuildWatermarkSvg(float width, float height, string text)
    {
        text = EscapeXml(text);

        var alpha = (35f / 255f).ToString(CultureInfo.InvariantCulture); // matches your SKColor alpha=35
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

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private static void DrawWatermark(
            object canvasObject,
            QuestPDF.Infrastructure.Size size,
            string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var canvas = (SKCanvas)canvasObject;

            using var paint = new SKPaint
            {
                Color = new SKColor(160, 120, 200, 35),   // semi-transparent purple
                TextSize = 48,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName(
                    "Arial",
                    SKFontStyle.Bold),
                TextAlign = SKTextAlign.Center
            };

            float cx = size.Width / 2f;
            float cy = size.Height / 2f;

            canvas.Save();
            canvas.Translate(cx, cy);
            canvas.RotateDegrees(-35);

            // Draw the text three times (top, middle, bottom) for a tiled feel
            canvas.DrawText(text, 0, -size.Height * 0.25f, paint);
            canvas.DrawText(text, 0, 0, paint);
            canvas.DrawText(text, 0, size.Height * 0.25f, paint);

            canvas.Restore();
        }

        // ── Layout Builder ──────────────────────────────────────────────────

        private static void ComposeBiodata(
            IContainer root, Biodata p, List<string> photos, string businessName)
        {
            bool isFemale = p.Gender?.ToUpper() == "FEMALE";
            var accentColor = isFemale
                ? QuestPDF.Helpers.Colors.Pink.Darken3
                : QuestPDF.Helpers.Colors.Blue.Darken3;
            var lightAccent = isFemale
                ? QuestPDF.Helpers.Colors.Pink.Lighten4
                : QuestPDF.Helpers.Colors.Blue.Lighten4;

            root.Column(col =>
            {
                // ── Header banner ─────────────────────────────────────────
                col.Item().Background(accentColor).Padding(10).Row(row =>
                {
                    row.RelativeItem().Column(hCol =>
                    {
                        // Business name tag
                        if (!string.IsNullOrWhiteSpace(businessName))
                        {
                            hCol.Item()
                                .Text(businessName.ToUpper())
                                .FontSize(8)
                                .FontColor(QuestPDF.Helpers.Colors.White)
                                .Italic();
                        }

                        hCol.Item().Text("✦  Marriage Biodata  ✦")
                            .FontSize(7).FontColor(QuestPDF.Helpers.Colors.White).Italic();
                        hCol.Item().Text(p.Name.ToUpper())
                            .FontSize(18).FontColor(QuestPDF.Helpers.Colors.White).Bold();
                        hCol.Item().PaddingTop(3).Row(r2 =>
                        {
                            r2.AutoItem().Background(lightAccent).PaddingVertical(1)
                                .PaddingHorizontal(3)
                              .Text(p.Gender?.ToUpper() ?? "")
                              .FontSize(8).FontColor(accentColor).Bold();

                            if (!string.IsNullOrWhiteSpace(p.AgeDisplay))
                                r2.AutoItem().PaddingLeft(8)
                                  .Text($"Age: {p.AgeDisplay}")
                                  .FontSize(8).FontColor(QuestPDF.Helpers.Colors.White);

                            if (!string.IsNullOrWhiteSpace(p.Caste))
                                r2.AutoItem().PaddingLeft(8)
                                  .Text($"Caste: {p.Caste}")
                                  .FontSize(8).FontColor(QuestPDF.Helpers.Colors.White);

                            // Status badge
                            r2.AutoItem().PaddingLeft(8)
                              .Text($"Status: {p.Status}")
                              .FontSize(8).FontColor(QuestPDF.Helpers.Colors.White);
                        });
                    });

                    // OM symbol
                    row.AutoItem().AlignMiddle().AlignRight()
                       .Text("ॐ").FontSize(32)
                       .FontColor(QuestPDF.Helpers.Colors.White).Bold();
                });

                col.Item().PaddingVertical(6);

                // ── Two Photos Side by Side ───────────────────────────────
                var photo1 = photos.Count > 0 ? photos[0] : null;
                var photo2 = photos.Count > 1 ? photos[1] : null;

                if (photo1 != null || photo2 != null)
                {
                    col.Item().Row(photoRow =>
                    {
                        photoRow.RelativeItem(1);
                        AddPhotoBox(photoRow.ConstantItem(130), photo1, "Photo 1", accentColor);
                        photoRow.ConstantItem(16);
                        AddPhotoBox(photoRow.ConstantItem(130), photo2, "Photo 2", accentColor);
                        photoRow.RelativeItem(1);
                    });
                    col.Item().PaddingVertical(8);
                }

                // ── Detail Sections ──────────────────────────────────────
                col.Item().Row(mainRow =>
                {
                    // Left column
                    mainRow.RelativeItem().Column(left =>
                    {
                        SectionHeader(left, "Personal Information", accentColor);
                        DetailRow(left, "Date of Birth", p.DateOfBirth);
                        DetailRow(left, "Time of Birth", $"{p.TimeOfBirth} {p.AmPm}".Trim());
                        DetailRow(left, "Place of Birth", p.PlaceOfBirth);
                        DetailRow(left, "Height", p.Height);
                        DetailRow(left, "Complexion", p.Complexion);
                        DetailRow(left, "Religion", p.Religion);

                        left.Item().PaddingTop(8);
                        SectionHeader(left, "Horoscope", accentColor);
                        DetailRow(left, "Birth Star (Nakshatra)", p.BirthStar);
                        DetailRow(left, "Padam", p.Padam);
                        DetailRow(left, "Raasi (Rashi)", p.Raasi);
                        DetailRow(left, "Paternal Gotram", p.PaternalGotram);
                        DetailRow(left, "Maternal Gotram", p.MaternalGotram);

                        left.Item().PaddingTop(8);
                        SectionHeader(left, "Education & Career", accentColor);
                        DetailRow(left, "Qualification", p.Qualification);
                        DetailRow(left, "Designation", p.Designation);
                        DetailRow(left, "Company / Address", p.CompanyAddress);
                    });

                    mainRow.ConstantItem(14);

                    // Right column
                    mainRow.RelativeItem().Column(right =>
                    {
                        SectionHeader(right, "Family Information", accentColor);
                        DetailRow(right, "Father's Name", p.FatherName);
                        DetailRow(right, "Father's Occupation", p.FatherOccupation);
                        DetailRow(right, "Mother's Name", p.MotherName);
                        DetailRow(right, "Mother's Occupation", p.MotherOccupation);
                        DetailRow(right, "Brothers", p.BrotherCount);
                        DetailRow(right, "Brother Occupation", p.BrotherOccupation);
                        DetailRow(right, "Sisters", p.SisterCount);
                        DetailRow(right, "Sister Occupation", p.SisterOccupation);
                        DetailRow(right, "Grand Father", p.GrandFatherName);
                        DetailRow(right, "Elder Father", p.ElderFather);

                        right.Item().PaddingTop(8);
                        SectionHeader(right, "Address & Contact", accentColor);
                        var addr = string.Join(", ",
                            new[] { p.DoorNumber, p.AddressLine, p.TownVillage, p.District, p.State, p.PinCode }
                            .Where(s => !string.IsNullOrWhiteSpace(s)));
                        DetailRow(right, "Address", addr);
                        DetailRow(right, "Living In", p.LivingIn);
                        DetailRow(right, "Phone 1", p.Phone1);
                        DetailRow(right, "Phone 2", p.Phone2);
                        DetailRow(right, "References", p.References);
                    });
                });

                // ── Expectations ─────────────────────────────────────────
                if (!string.IsNullOrWhiteSpace(p.ExpectationsFromPartner))
                {
                    col.Item().PaddingTop(8);
                    col.Item().Background(lightAccent).Padding(6).Column(exp =>
                    {
                        exp.Item().Text("Partner Expectations").Bold()
                           .FontSize(9).FontColor(accentColor);
                        exp.Item().PaddingTop(2).Text(p.ExpectationsFromPartner).FontSize(8);
                    });
                }

                // ── Footer ────────────────────────────────────────────────
                col.Item().PaddingTop(10);
                col.Item().BorderTop(1).BorderColor(accentColor)
                   .PaddingTop(4)
                   .Row(footer =>
                   {
                       footer.RelativeItem()
                             .Text($"Generated on {DateTime.Now:dd-MMM-yyyy}")
                             .FontSize(7).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                       footer.RelativeItem().AlignRight()
                             .Text(string.IsNullOrWhiteSpace(businessName)
                                 ? "Marriage Bureau Management System"
                                 : businessName)
                             .FontSize(7).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                   });
            });
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static void AddPhotoBox(IContainer container, string? photoPath, string label, string borderColor)
        {
            container.Border(1).BorderColor(borderColor).Column(c =>
            {
                c.Item().Height(155).Element(img =>
                {
                    if (!string.IsNullOrWhiteSpace(photoPath) && File.Exists(photoPath))
                    {
                        try { img.Image(photoPath).FitArea(); return; }
                        catch { }
                    }
                    img.AlignCenter().AlignMiddle()
                       .Text("[ No Photo ]").FontSize(8)
                       .FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                });
                c.Item().Background(borderColor).Padding(2)
                 .AlignCenter().Text(label).FontSize(7)
                 .FontColor(QuestPDF.Helpers.Colors.White);
            });
        }

        private static void SectionHeader(ColumnDescriptor col, string title, string color)
        {
            col.Item().Background(color).PaddingHorizontal(4)
                 .PaddingVertical(2)
               .Text(title).FontSize(9).Bold()
               .FontColor(QuestPDF.Helpers.Colors.White);
            col.Item().PaddingTop(3);
        }

        private static void DetailRow(ColumnDescriptor col, string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            col.Item().PaddingBottom(2).Row(row =>
            {
                row.ConstantItem(110).Text(label + " :").FontSize(8)
                   .FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
                row.RelativeItem().Text(value).FontSize(8).Bold();
            });
        }
    }
}
