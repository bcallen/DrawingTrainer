# Drawing Trainer

A WPF desktop application for improving drawing skills through timed practice sessions with photo references.

## Features

- **Photo Library** — Import reference photos (single or bulk from folder), organize with tags (Portrait, Landscape, Architecture, Figure, Still Life, Animal), filter and browse
- **Inline Tag Editing** — Select one or more photos in the library to quickly add/remove tag assignments
- **Session Planner** — Create reusable session plans with multiple exercises, each with a category and duration
- **Timed Drawing Sessions** — Countdown timer with random reference photos, 30-second breaks between exercises, pause/skip/end controls
- **Post-Session Upload** — Link photos of your drawings to the reference you were working from
- **Gallery** — Browse past drawings filtered by tag, view reference overlays, see exercise duration
- **Zoomable Images** — Scroll-wheel zoom and click-drag pan on reference photos and gallery images
- **EXIF-Aware** — Correctly displays portrait-orientation photos from phones/cameras
- **Keyboard Shortcuts** — Space (pause/resume), Escape (end session/back), arrow keys (gallery navigation)

## Tech Stack

- C# / .NET 8 / WPF
- SQLite via Entity Framework Core
- CommunityToolkit.Mvvm (MVVM source generators)
- Microsoft.Extensions.DependencyInjection

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build & Run

```
dotnet run --project DrawingTrainer/DrawingTrainer.csproj
```

### Quick Start

1. Click **Import Photos** to add reference images and assign tags
2. Go to **Session Planner** and create a plan (e.g. 3 Portraits at 2 minutes each)
3. Click **Start Session** to begin timed drawing practice
4. After the session, upload photos of your drawings
5. Browse your work in the **Gallery** with reference overlays

## Data Storage

- **Database:** `%LocalAppData%/DrawingTrainer/drawingtrainer.db` (SQLite)
- **Photos:** `%LocalAppData%/DrawingTrainer/Photos/` (references and drawings organized by date)
