using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarriageBureau.Models
{
    public class Biodata
    {
        [Key]
        public int Id { get; set; }

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
        public string? CompanyAddress { get; set; }

        // ─── Family ───────────────────────────────────────────────────────
        public string? FatherName { get; set; }
        public string? FatherOccupation { get; set; }
        public string? MotherName { get; set; }
        public string? MotherOccupation { get; set; }
        public string? NoOfSiblings { get; set; }
        public string? BrotherCount { get; set; }
        public string? BrotherOccupation { get; set; }
        public string? SisterCount { get; set; }
        public string? SisterOccupation { get; set; }
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

        // ─── Partner Expectations ─────────────────────────────────────────
        public string? ExpectationsFromPartner { get; set; }

        // ─── Files ────────────────────────────────────────────────────────
        /// <summary>Full path to the profile photo on disk</summary>
        public string? PhotoPath { get; set; }

        /// <summary>Full path to the biodata PDF on disk</summary>
        public string? PdfPath { get; set; }

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
                    // Try to parse various date formats
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
        public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoPath) && System.IO.File.Exists(PhotoPath);

        [NotMapped]
        public bool HasPdf => !string.IsNullOrWhiteSpace(PdfPath) && System.IO.File.Exists(PdfPath);
    }
}
