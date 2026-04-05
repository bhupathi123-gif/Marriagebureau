using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarriageBureau.Models
{
    /// <summary>Current status of a biodata profile.</summary>
    public enum ProfileStatus
    {
        Active,
        Inactive,
        Married,
        Engaged,
        OnHold,
        Closed
    }

    public class Biodata
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Matrimony Internal ID — user-supplied unique reference (e.g. F1, F2, M1, M2).
        /// Mandatory; must be unique across all profiles.
        /// Maps to the S.NO. column in import Excel.
        /// </summary>
        [Required]
        public string IntId { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable profile identifier, e.g. "TS001".
        /// Generated on first save as: prefix (from App.config "ProfileIdPrefix") + sequential number.
        /// </summary>
        public string? ProfileId { get; set; }

        // ─── Personal Info ────────────────────────────────────────────────
        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Caste { get; set; }

        [Required]
        public string Gender { get; set; } = "Male";   // Male / Female

        public string? DateOfBirth { get; set; }
        public string? TimeOfBirth { get; set; }
        public string? AmPm { get; set; }
        public string? PlaceOfBirth { get; set; }
        public string? Height { get; set; }
        public string? Complexion { get; set; }

        // ─── Horoscope ────────────────────────────────────────────────────
        public string? BirthStar { get; set; }
        public string? Padam { get; set; }
        public string? Raasi { get; set; }
        public string? Religion { get; set; }
        public string? PaternalGotram { get; set; }
        public string? MaternalGotram { get; set; }

        // ─── Education & Career ───────────────────────────────────────────
        public string? Qualification { get; set; }
        public string? Designation { get; set; }
        public string? DesignationDetails { get; set; }   // Designation Details (Name & Place)
        public string? CompanyAddress { get; set; }
        public string? Income { get; set; }               // Annual Income
        public string? AssetValue { get; set; }           // Asset Value
        public string? Gift { get; set; }                 // Gift / Dowry

        // ─── Family ───────────────────────────────────────────────────────
        public string? FatherName { get; set; }
        public string? FatherOccupation { get; set; }
        public string? MotherName { get; set; }
        public string? MotherOccupation { get; set; }
        public string? NoOfSiblings { get; set; }
        public string? BrotherCount { get; set; }
        public string? BrotherOccupation { get; set; }
        public string? BrotherDetails { get; set; }       // Brother Details (names/occupations)
        public string? SisterCount { get; set; }
        public string? SisterOccupation { get; set; }
        public string? SisterDetails { get; set; }        // Sister Details (names/occupations)
        public string? BrotherInLaw { get; set; }
        public string? GrandFatherName { get; set; }
        public string? ElderFather { get; set; }
        public string? ElderFatherPhone { get; set; }

        // ─── Address ──────────────────────────────────────────────────────
        public string? DoorNumber { get; set; }
        public string? AddressLine { get; set; }
        public string? TownVillage { get; set; }
        public string? District { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? PinCode { get; set; }
        public string? LivingIn { get; set; }

        // ─── Contact ──────────────────────────────────────────────────────
        public string? Phone1 { get; set; }
        public string? Phone2 { get; set; }
        public string? References { get; set; }
        public string? ReferencePhone { get; set; }       // Reference Phone number

        // ─── Partner Expectations ─────────────────────────────────────────
        public string? ExpectationsFromPartner { get; set; }
        public string? Preferences { get; set; }          // Candidate Preferences

        // ─── Profile Status ───────────────────────────────────────────────
        /// <summary>Current lifecycle status of the profile (Active / Inactive / Married / Engaged / OnHold / Closed)</summary>
        public ProfileStatus Status { get; set; } = ProfileStatus.Active;

        // ─── Files ────────────────────────────────────────────────────────
        /// <summary>Primary / cover photo path (kept for backward compat and quick access)</summary>
        public string? PhotoPath { get; set; }

        /// <summary>Full path to the biodata PDF on disk</summary>
        public string? PdfPath { get; set; }

        // ─── Multiple Photos (gallery) ────────────────────────────────────
        /// <summary>All photos for this profile, ordered by SortOrder</summary>
        public List<BiodataPhoto> Photos { get; set; } = new();

        // ─── Meta ─────────────────────────────────────────────────────────
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "(No Name)" : Name;

        [NotMapped]
        public string AgeDisplay
        {
            get
            {
                if (string.IsNullOrWhiteSpace(DateOfBirth)) return "N/A";
                try
                {
                    if (DateTime.TryParse(DateOfBirth.Replace(" ", "-"), out var dob))
                    {
                        var age = DateTime.Today.Year - dob.Year;
                        if (dob.Date > DateTime.Today.AddYears(-age)) age--;
                        return $"{age} yrs";
                    }
                }
                catch { }
                return DateOfBirth;
            }
        }

        [NotMapped]
        public bool HasPhoto => (!string.IsNullOrWhiteSpace(PhotoPath) && System.IO.File.Exists(PhotoPath))
                                || (Photos != null && Photos.Count > 0 && Photos[0].Exists);

        [NotMapped]
        public bool HasPdf => !string.IsNullOrWhiteSpace(PdfPath) && System.IO.File.Exists(PdfPath);

        /// <summary>Returns the best available primary photo path (gallery first, then legacy field)</summary>
        [NotMapped]
        public string? PrimaryPhotoPath
        {
            get
            {
                if (Photos != null && Photos.Count > 0)
                {
                    var first = Photos.Find(p => p.Exists);
                    if (first != null) return first.FilePath;
                }
                if (!string.IsNullOrWhiteSpace(PhotoPath) && System.IO.File.Exists(PhotoPath))
                    return PhotoPath;
                return null;
            }
        }

        /// <summary>Status display label with colour hint</summary>
        [NotMapped]
        public string StatusDisplay => Status.ToString();

        /// <summary>Badge colour for the status pill</summary>
        [NotMapped]
        public string StatusColor => Status switch
        {
            ProfileStatus.Active   => "#2E7D32",   // green
            ProfileStatus.Inactive => "#757575",   // grey
            ProfileStatus.Married  => "#1565C0",   // blue
            ProfileStatus.Engaged  => "#6A1B9A",   // purple
            ProfileStatus.OnHold   => "#E65100",   // orange
            ProfileStatus.Closed   => "#B71C1C",   // red
            _                      => "#757575"
        };
    }
}
