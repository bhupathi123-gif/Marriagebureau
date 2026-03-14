using System.Globalization;
using System.IO;
using MarriageBureau.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MarriageBureau.Services
{
    /// <summary>
    /// Generates a marriage biodata PDF and/or image using QuestPDF.
    /// The Trishul golden background image is drawn as the page background;
    /// all biodata text is overlaid in dark-maroon tones that suit the template.
    ///
    /// Background image path:
    ///   Default → Resources\bg_biodata.jpg  (next to the exe)
    ///   Override → set <see cref="BackgroundImagePath"/> before calling export.
    /// </summary>
    public static class BiodataExportService
    {
        static BiodataExportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // ── Background image path (physical path, changeable at runtime) ─────
        /// <summary>
        /// Full path to the background JPEG/PNG used for every exported page.
        /// Defaults to  &lt;exe-dir&gt;\Resources\bg_biodata.jpg
        /// Change this property to swap the template without recompiling.
        /// </summary>
        public static string BackgroundImagePath { get; set; } =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                         "Resources", "bg_biodata.jpg");

        // ── Colour palette – dark maroon family, readable on golden background
        private const string C_HEADING  = "#4A0E05";   // very dark maroon  – candidate name
        private const string C_SECTION  = "#7B1A10";   // deep red-maroon   – section header bar
        private const string C_LABEL    = "#6B3020";   // medium brown      – field labels
        private const string C_VALUE    = "#3A0A00";   // near-black maroon – field values
        private const string C_SUBHEAD  = "#5C1A0E";   // dark maroon       – sub-headings
        private const string C_FOOTER   = "#F5E6C8";   // warm cream        – text on dark footer band

        // ── Layout constants (A4 points) ─────────────────────────────────────
        // The background image has:
        //   • top border   :  ~14 pt
        //   • header band  :  14 – 130 pt  (TRISHUL BIODATA logo lives here)
        //   • content area : 148 – 730 pt  ← we write here
        //   • footer band  : 735 – 820 pt  (bureau address printed on template)
        //   • bottom border: 820 – 842 pt
        private const float CONTENT_TOP    = 148f;
        private const float CONTENT_BOTTOM = 730f;
        private const float L_MARGIN       = 52f;
        private const float R_MARGIN       = 543f;

        // ══════════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Exports a 2-page PDF: page 1 = personal/horoscope/education + photo,
        /// page 2 = family / address / contact / expectations.
        /// Both pages share the same background image.
        /// </summary>
        public static void ExportToPdf(Biodata profile, List<string> photoPaths, string outputPath)
        {
            var doc = BuildDocument(profile, photoPaths, exportImage: false);
            doc.GeneratePdf(outputPath);
        }

        /// <summary>
        /// Exports page 1 of the biodata as a JPEG image at 150 DPI.
        /// </summary>
        public static void ExportToImage(Biodata profile, List<string> photoPaths, string outputPath)
        {
            var doc = BuildDocument(profile, photoPaths, exportImage: true);

            var images = doc.GenerateImages(new ImageGenerationSettings
            {
                RasterDpi                = 150,
                ImageFormat              = ImageFormat.Jpeg,
                ImageCompressionQuality  = ImageCompressionQuality.High
            });

            if (images.Any())
                File.WriteAllBytes(outputPath, images.First());
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Document builder
        // ══════════════════════════════════════════════════════════════════════

        private static IDocument BuildDocument(Biodata p, List<string> photos, bool exportImage)
        {
            var bgPath  = BackgroundImagePath;
            var hasBg   = File.Exists(bgPath);
            var photo1  = photos.Count > 0 && File.Exists(photos[0]) ? photos[0] : null;

            return Document.Create(container =>
            {
                // ── Page 1: Personal · Horoscope · Education · Photo ─────────
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);   // we control all positioning manually

                    // Background image – full page
                    if (hasBg)
                        page.Background().Image(bgPath).FitArea();

                    page.Content().Element(root => ComposePage1(root, p, photo1));
                });

                // ── Page 2: Family · Address · Contact · Expectations ─────────
                // Only include in PDF export (image export = page 1 only)
                if (!exportImage)
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(0);

                        if (hasBg)
                            page.Background().Image(bgPath).FitArea();

                        page.Content().Element(root => ComposePage2(root, p));
                    });
                }
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Page 1 layout
        // ══════════════════════════════════════════════════════════════════════

        private static void ComposePage1(IContainer root, Biodata p, string? photoPath)
        {
            // Outer column with top padding to clear the background header band
            root.PaddingTop(CONTENT_TOP)
                .PaddingBottom(PageSizes.A4.Height - CONTENT_BOTTOM)
                .PaddingLeft(L_MARGIN)
                .PaddingRight(PageSizes.A4.Width - R_MARGIN)
                .Column(col =>
                {
                    col.Spacing(0);

                    // ── Candidate name (displayed in header area via negative margin trick)
                    col.Item().PaddingBottom(6).Column(nameCol =>
                    {
                        nameCol.Item()
                               .Text(p.Name.ToUpper())
                               .FontSize(17).Bold()
                               .FontColor(C_HEADING)
                               .AlignCenter();

                        // Sub-line: Gender | Age | Caste
                        var sub = new List<string>();
                        if (!string.IsNullOrWhiteSpace(p.Gender))   sub.Add(p.Gender);
                        if (!string.IsNullOrWhiteSpace(p.AgeDisplay)) sub.Add(p.AgeDisplay);
                        if (!string.IsNullOrWhiteSpace(p.Caste))    sub.Add(p.Caste);
                        if (sub.Count > 0)
                            nameCol.Item()
                                   .Text(string.Join("  |  ", sub))
                                   .FontSize(8.5f).FontColor(C_SUBHEAD)
                                   .AlignCenter();
                    });

                    // ── Main body: two columns + optional photo ───────────────
                    col.Item().Row(row =>
                    {
                        // Left column (personal + horoscope + education)
                        float leftWeight  = photoPath != null ? 3.2f : 2f;
                        float rightWeight = photoPath != null ? 2.8f : 2f;

                        row.RelativeItem(leftWeight).Column(left =>
                        {
                            left.Spacing(0);

                            SectionBar(left, "PERSONAL INFORMATION");
                            FieldRow(left, "Date of Birth",   p.DateOfBirth);
                            FieldRow(left, "Time of Birth",
                                          Join(p.TimeOfBirth, p.AmPm));
                            FieldRow(left, "Place of Birth",  p.PlaceOfBirth);
                            FieldRow(left, "Height",          p.Height);
                            FieldRow(left, "Complexion",      p.Complexion);
                            FieldRow(left, "Religion",        p.Religion);

                            left.Item().Height(5);
                            SectionBar(left, "HOROSCOPE");
                            FieldRow(left, "Birth Star",      p.BirthStar);
                            FieldRow(left, "Padam",           p.Padam);
                            FieldRow(left, "Raasi",           p.Raasi);
                            FieldRow(left, "Paternal Gotram", p.PaternalGotram);
                            FieldRow(left, "Maternal Gotram", p.MaternalGotram);

                            left.Item().Height(5);
                            SectionBar(left, "EDUCATION & CAREER");
                            FieldRow(left, "Qualification",   p.Qualification);
                            FieldRow(left, "Designation",     p.Designation);
                            FieldRowWrap(left, "Company/Address", p.CompanyAddress);
                        });

                        row.ConstantItem(10); // gutter

                        // Right column: photo (if available) + expectations
                        row.RelativeItem(rightWeight).Column(right =>
                        {
                            right.Spacing(0);

                            if (photoPath != null)
                            {
                                right.Item()
                                     .Border(1.5f).BorderColor(C_SECTION)
                                     .Width(110).Height(138)
                                     .AlignCenter()
                                     .Image(photoPath)
                                     .FitArea();

                                right.Item().Height(8);
                            }

                            if (!string.IsNullOrWhiteSpace(p.ExpectationsFromPartner))
                            {
                                SectionBar(right, "EXPECTATIONS");
                                right.Item()
                                     .PaddingTop(2)
                                     .Text(p.ExpectationsFromPartner)
                                     .FontSize(7.5f).FontColor(C_VALUE);
                            }
                        });
                    });
                });
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Page 2 layout
        // ══════════════════════════════════════════════════════════════════════

        private static void ComposePage2(IContainer root, Biodata p)
        {
            root.PaddingTop(CONTENT_TOP)
                .PaddingBottom(PageSizes.A4.Height - CONTENT_BOTTOM)
                .PaddingLeft(L_MARGIN)
                .PaddingRight(PageSizes.A4.Width - R_MARGIN)
                .Column(col =>
                {
                    col.Spacing(0);

                    // Page heading
                    col.Item().PaddingBottom(6)
                              .Text(p.Name.ToUpper())
                              .FontSize(17).Bold()
                              .FontColor(C_HEADING)
                              .AlignCenter();

                    col.Item().Row(row =>
                    {
                        // Left column: family + address
                        row.RelativeItem().Column(left =>
                        {
                            left.Spacing(0);

                            SectionBar(left, "FAMILY INFORMATION");
                            FieldRow(left, "Father's Name",     p.FatherName);
                            FieldRow(left, "Father Occupation", p.FatherOccupation);
                            FieldRow(left, "Mother's Name",     p.MotherName);
                            FieldRow(left, "Mother Occupation", p.MotherOccupation);
                            FieldRow(left, "Brothers",          p.BrotherCount);
                            FieldRow(left, "Brother Occ.",      p.BrotherOccupation);
                            FieldRow(left, "Sisters",           p.SisterCount);
                            FieldRow(left, "Sister Occ.",       p.SisterOccupation);
                            FieldRow(left, "Grandfather",       p.GrandFatherName);
                            FieldRow(left, "Elder Father",      p.ElderFather);
                            FieldRow(left, "Elder Father Ph.",  p.ElderFatherPhone);
                            FieldRow(left, "Brother-in-Law",    p.BrotherInLaw);

                            left.Item().Height(5);
                            SectionBar(left, "ADDRESS & CONTACT");

                            var addrParts = new[]
                            {
                                p.DoorNumber, p.AddressLine, p.TownVillage,
                                p.District,   p.State,       p.PinCode
                            }.Where(s => !string.IsNullOrWhiteSpace(s));

                            FieldRowWrap(left, "Address", string.Join(", ", addrParts));
                            FieldRow(left, "Living In",  p.LivingIn);
                            FieldRow(left, "Phone 1",    p.Phone1);
                            FieldRow(left, "Phone 2",    p.Phone2);
                            FieldRowWrap(left, "References", p.References);
                        });

                        row.ConstantItem(10);

                        // Right column: expectations overflow
                        row.RelativeItem().Column(right =>
                        {
                            right.Spacing(0);

                            if (!string.IsNullOrWhiteSpace(p.ExpectationsFromPartner))
                            {
                                SectionBar(right, "PARTNER EXPECTATIONS");
                                right.Item()
                                     .PaddingTop(2)
                                     .Text(p.ExpectationsFromPartner)
                                     .FontSize(7.5f).FontColor(C_VALUE);
                            }
                        });
                    });
                });
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Shared UI helpers
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Solid dark-maroon bar with white title text.</summary>
        private static void SectionBar(ColumnDescriptor col, string title)
        {
            col.Item()
               .Background(C_SECTION)
               .PaddingHorizontal(4).PaddingVertical(2)
               .Text(title)
               .FontSize(8f).Bold()
               .FontColor(Colors.White);
            col.Item().Height(2);
        }

        /// <summary>Single label : value row on one line.</summary>
        private static void FieldRow(ColumnDescriptor col, string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            col.Item().PaddingBottom(1.5f).Row(row =>
            {
                row.ConstantItem(105)
                   .Text(label + " :")
                   .FontSize(7.5f).FontColor(C_LABEL);

                row.RelativeItem()
                   .Text(value)
                   .FontSize(7.5f).Bold().FontColor(C_VALUE);
            });
        }

        /// <summary>Label on its own line then value wrapping below (for long text).</summary>
        private static void FieldRowWrap(ColumnDescriptor col, string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            col.Item().PaddingBottom(1.5f).Column(c =>
            {
                c.Item().Text(label + " :")
                        .FontSize(7.5f).FontColor(C_LABEL);
                c.Item().PaddingLeft(4)
                        .Text(value)
                        .FontSize(7.5f).Bold().FontColor(C_VALUE);
            });
        }

        /// <summary>Joins two nullable strings with a space, returning null if both empty.</summary>
        private static string? Join(string? a, string? b)
        {
            var result = $"{a} {b}".Trim();
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
    }
}
