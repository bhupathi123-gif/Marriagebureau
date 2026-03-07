using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarriageBureau.Models
{
    /// <summary>
    /// Represents one photo associated with a Biodata profile.
    /// A single profile can have multiple photos (gallery / slideshow).
    /// </summary>
    public class BiodataPhoto
    {
        [Key]
        public int Id { get; set; }

        /// <summary>FK to the parent Biodata record</summary>
        public int BiodataId { get; set; }

        /// <summary>Full path on disk where the image file is stored</summary>
        [Required]
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Display order within this profile's gallery (0 = primary / cover photo)</summary>
        public int SortOrder { get; set; } = 0;

        /// <summary>Optional caption for the photo</summary>
        public string? Caption { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.Now;

        // ── Navigation ──────────────────────────────────────────────────
        [ForeignKey(nameof(BiodataId))]
        public Biodata? Biodata { get; set; }

        // ── Computed (not mapped) ────────────────────────────────────────
        [NotMapped]
        public bool Exists => !string.IsNullOrWhiteSpace(FilePath) && System.IO.File.Exists(FilePath);

        [NotMapped]
        public string FileName => System.IO.Path.GetFileName(FilePath);
    }
}
