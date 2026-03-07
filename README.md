# 💍 Marriage Bureau - Management System

A professional **WPF Windows Desktop Application** for managing marriage bureau biodata profiles.

---

## ✨ Features

### 📊 Dashboard
- Statistics overview: Total, Male, Female, With Photos, With PDFs
- Quick action buttons (Add, Browse, Slideshow)
- Recently added profiles table

### 📋 Browse Profiles
- Full sortable data grid with all profile fields
- **Live search** across name, phone, caste, district, qualification
- **Gender & caste filter** dropdowns
- Photo thumbnail in grid
- PDF indicator icon for profiles with attached PDF
- Double-click to edit a profile
- Delete with confirmation

### ➕ Add / Edit Biodata
All fields from the Excel file are supported:

**Personal Info:** Name, Gender, Caste, Date of Birth, Time of Birth, AM/PM, Place of Birth, Height, Complexion

**Horoscope:** Birth Star (Nakshatra), Padam, Raasi, Religion, Paternal Gotram, Maternal Gotram

**Education & Career:** Qualification, Designation, Company Name & Address

**Family:** Father Name & Occupation, Mother Name & Occupation, Brother/Sister Count & Occupation, Brother-in-Law, Grand Father Name, Elder Father & Phone

**Address:** Door No., Street/Address, Town/Village, District, State, Country, PIN Code, Living In

**Contact:** Phone 1, Phone 2, References

**Expectations from Partner**

**📸 Photo Upload:** Upload JPG/PNG/BMP photos — shown as preview in form + thumbnail in grid

**📄 PDF Upload:** Upload biodata PDF — open directly in default PDF viewer

### 🎞️ Profile Slideshow
- Dark themed cinematic slideshow view
- Shows profile photo (large) with all details
- **▶ Play / ⏸ Pause** auto-slide button
- Configurable slide interval (2–30 seconds)
- Previous / Next navigation buttons
- Gender filter (All / Male / Female)
- Profile counter display
- Thumbnail strip at bottom

---

## 🛠️ Technology Stack

| Technology | Version |
|---|---|
| .NET | 8.0 |
| WPF | Windows Presentation Foundation |
| Entity Framework Core | 8.0 (SQLite) |
| Material Design In XAML | 5.1.0 |
| SQLite (local database) | via EF Core |

---

## 📁 Project Structure

```
MarriageBureau/
├── Models/
│   └── Biodata.cs              ← All 44 fields from Excel
├── Data/
│   └── AppDbContext.cs         ← SQLite database context
├── ViewModels/
│   ├── BaseViewModel.cs
│   ├── RelayCommand.cs
│   ├── MainViewModel.cs        ← Navigation
│   ├── DashboardViewModel.cs
│   ├── BrowseViewModel.cs      ← Search & filter logic
│   ├── AddEditViewModel.cs     ← Form logic + file upload
│   └── SlideshowViewModel.cs   ← Auto-slide timer
├── Views/
│   ├── MainWindow.xaml         ← Shell with left nav
│   ├── DashboardView.xaml
│   ├── BrowseView.xaml
│   ├── AddEditView.xaml        ← Full 44-field form
│   └── SlideshowView.xaml
├── Converters/
│   └── Converters.cs           ← Value converters
└── Resources/
    └── placeholder.png
```

---

## 🚀 How to Build & Run

### Requirements
- **Windows 10/11** (WPF is Windows-only)
- **Visual Studio 2022** (Community or higher) with **.NET Desktop Development** workload
  OR **dotnet SDK 8.0** for command-line builds

### Option A: Visual Studio
1. Open `MarriageBureau.sln` in **Visual Studio 2022**
2. Wait for NuGet packages to restore (automatic)
3. Press **F5** to run (Debug) or **Ctrl+Shift+B** to build

### Option B: Command Line
```powershell
# From the solution directory
dotnet restore
dotnet build
dotnet run --project MarriageBureau
```

### Option C: Publish Self-Contained EXE
```powershell
dotnet publish MarriageBureau -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
# Run: publish/MarriageBureau.exe
```

---

## 💾 Data Storage

- **Database:** SQLite stored at `%APPDATA%\MarriageBureau\marriage_bureau.db`
- **Photos:** Copied to `%APPDATA%\MarriageBureau\Photos\`
- **PDFs:** Copied to `%APPDATA%\MarriageBureau\PDFs\`

All data is stored locally on the computer — no internet required.

---

## 📊 Fields Captured (from Excel)

| # | Field | Notes |
|---|---|---|
| 1 | S.No | Auto-generated |
| 2 | Name | Required |
| 3 | Caste | e.g. ARYA VYSYA |
| 4 | Gender | Male / Female |
| 5 | D.O.B | Date of Birth |
| 6 | Time of Birth | HH:MM |
| 7 | AM/PM | |
| 8 | Place of Birth | |
| 9 | Height | e.g. 5'4" |
| 10 | Complexion | FAIR/WHITE/MEDIUM etc. |
| 11 | Birth Star | Nakshatra |
| 12 | Padam | 1/2/3/4 |
| 13 | Raasi | Zodiac sign |
| 14 | Religion | |
| 15 | Paternal Gotram | |
| 16 | Maternal Gotram | |
| 17 | Qualification | Education |
| 18 | Designation | Job title |
| 19 | Company & Address | |
| 20 | Father Name | |
| 21 | Father Occupation | |
| 22 | Mother Name | |
| 23 | Mother Occupation | |
| 24 | No. of Siblings | |
| 25 | Brothers | Count |
| 26 | Brother Occupation | |
| 27 | Sisters | Count |
| 28 | Sister Occupation | |
| 29 | Brother-in-Law | |
| 30 | Door No. | |
| 31 | Address | Street/landmark |
| 32 | Town/Village | |
| 33 | District | |
| 34 | State | |
| 35 | Country | |
| 36 | PIN Code | |
| 37 | Living In | Current city |
| 38 | Phone 1 | |
| 39 | Phone 2 | |
| 40 | References | |
| 41 | Grand Father Name | |
| 42 | Expectations | Partner preferences |
| 43 | Elder Father | Uncle name |
| 44 | Elder Father Phone | |
| + | Profile Photo | JPG/PNG upload |
| + | Biodata PDF | PDF upload |
