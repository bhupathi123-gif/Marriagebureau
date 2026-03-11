# Marriage Bureau Management System

A comprehensive WPF desktop application for managing marriage biodata profiles, built with .NET 8, WPF, Material Design, and SQLite.

---

## Projects in this Solution

| Project | Description |
|---|---|
| **MarriageBureau** | Main application – profile management, export, slideshow, login |
| **MBKeyGen** | Admin key-generator tool – generate security codes, hash passwords |

---

## Features

### 🔐 Login & Security
- Login screen with username/password (PBKDF2-SHA256 hashed)
- Default admin: `admin` / `Admin@123` (change on first use)
- Role-based access: **Admin** (full access) / **User** (no settings)
- Licence validation at startup – shows expiry warning / blocked screen

### 💍 Profile Management (Add / Edit)
- Full biodata form: personal info, horoscope, education, family, address, contact, partner expectations
- **Profile Status**: Active, Inactive, Married, Engaged, OnHold, Closed
- Multi-photo gallery (drag to reorder, set cover photo)
- PDF upload and view
- Import profile data from a PDF biodata file (iText7)

### 📋 Browse Profiles
- DataGrid with search (name, caste, phone, district, etc.)
- Filter by Gender, Caste, and **Profile Status**
- Status badge column with colour coding
- Export selected profile (PDF / image)
- Edit / Delete profiles

### 🎞 Profile Slideshow
- Full-screen slideshow with auto-play timer
- Advanced filter panel (Caste, District, Birth Star, Raasi, **Status**, Age range)
- Per-profile photo slideshow with navigation dots
- Active-filters badge

### 📤 Export Biodata
- **PDF export** (A4 layout): header with gender badge, two photos side by side, all detail sections, partner expectations, footer
- **Image export** (JPEG 150 DPI)
- **Business-name watermark** drawn diagonally in the centre of every page
- Business name appears in header and footer of exported document

### 📥 Import from Excel
- Import MALE / FEMALE sheet data (ClosedXML)
- Preview before import; skip duplicates; error reporting

### 📥 Import from PDF
- Auto-extract biodata fields from an existing PDF biodata file (iText7)
- Merge or replace existing fields

### ⚙ Settings (Admin)
- **Business Name**: enter plain text → stored AES-256 encrypted in DB → used as watermark on exports
- **Security Code**: paste code from MBKeyGen → validates expiry and updates business name automatically
- **User Management**: add/remove users, toggle active, set roles
- **Change Password**: change own password

---

## Security & Encryption

### Encryption Scheme
- **Algorithm**: AES-256-CBC with PBKDF2-SHA256 key derivation (100,000 iterations)
- **Passphrase** and **Salt** are hard-coded and identical in both `MarriageBureau` and `MBKeyGen`
- **Business name** stored as AES encrypted Base64 in `AppSettings.EncryptedBusinessName`
- **Security code** embeds `MB|{businessName}|{expiryDate:yyyyMMdd}` encrypted as Base64

### Security Code Format
```
MB|<BusinessName>|<yyyyMMdd>
```
Encrypted with AES-256 → Base64 string stored in `AppSettings.SecurityCode`.

---

## MBKeyGen – Admin Key Generator Tool

A separate standalone WPF application for administrators.

### Tabs

| Tab | Purpose |
|---|---|
| **Security Code Generator** | Enter business name + expiry → generate encrypted security code and encrypted business name |
| **Validate / Decode Code** | Paste a code to verify it and see business name / expiry date |
| **Password Hash Generator** | Hash a password with PBKDF2-SHA256 for manual DB user creation |
| **Encrypt / Decrypt Text** | General-purpose AES encrypt/decrypt |

### Usage Flow
1. Open `MBKeyGen.exe`
2. Enter business name (e.g. "Sri Lakshmi Marriage Bureau") and select expiry date
3. Click **Generate Security Code**
4. Copy the generated security code
5. In the main app, go to **Settings → Security Code** and paste the code
6. Click **Apply Code** – the app validates it, updates business name, and reloads the licence

---

## Database Schema

SQLite database at `%DataRootPath%\MarriageBureau\marriage_bureau.db`

| Table | Purpose |
|---|---|
| `Biodatas` | Profile records with all fields + `Status` column |
| `BiodataPhotos` | Gallery photos linked to a profile |
| `AppUsers` | Login users with PBKDF2 password hashes |
| `AppSettings` | Single-row config (encrypted business name + security code) |

Schema migrations run automatically at startup (adds columns/tables if missing).

---

## Default Credentials

| Username | Password | Role |
|---|---|---|
| admin | Admin@123 | Admin |

**Change the default password immediately after first login.**

---

## Technology Stack

- .NET 8 / WPF (Windows Presentation Foundation)
- Material Design In XAML Toolkit 5.3
- Entity Framework Core 8 (SQLite)
- QuestPDF 2024.3 (PDF & image export)
- iText7 8.0 (PDF import)
- ClosedXML 0.102 (Excel import)
- SkiaSharp (watermark rendering)
- System.Drawing.Common (image utilities)

---

## Building

Open `MarriageBureau.sln` in Visual Studio 2022 (or Rider) and build. Requires Windows (WPF).

```
MarriageBureau.sln
├── MarriageBureau/   ← Main application
└── MBKeyGen/         ← Admin key generator tool
```
