using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingTrainer.Models;
using DrawingTrainer.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;

namespace DrawingTrainer.ViewModels;

public partial class UploadedDrawingItem : ObservableObject
{
    public CompletedDrawing Drawing { get; set; } = null!;

    [ObservableProperty]
    private string _drawingPath = string.Empty;

    [ObservableProperty]
    private string _artistName = string.Empty;
}

public partial class ExerciseResultItem : ObservableObject
{
    public SessionExerciseResult Result { get; set; } = null!;

    [ObservableProperty]
    private string _referencePhotoPath = string.Empty;

    [ObservableProperty]
    private string _categoryName = string.Empty;

    [ObservableProperty]
    private bool _wasSkipped;

    [ObservableProperty]
    private ObservableCollection<UploadedDrawingItem> _uploadedDrawings = [];
}

public partial class PostSessionViewModel : ObservableObject
{
    private readonly IDrawingSessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly IArtistService _artistService;

    [ObservableProperty]
    private ObservableCollection<ExerciseResultItem> _exerciseResults = [];

    [ObservableProperty]
    private string _sessionSummary = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Artist> _artists = [];

    [ObservableProperty]
    private Artist? _selectedArtist;

    public PostSessionViewModel(
        IDrawingSessionService sessionService,
        INavigationService navigationService,
        IArtistService artistService)
    {
        _sessionService = sessionService;
        _navigationService = navigationService;
        _artistService = artistService;
    }

    public void Initialize(int drawingSessionId)
    {
        _ = LoadSessionAsync(drawingSessionId);
    }

    [RelayCommand]
    private async Task UploadDrawing(ExerciseResultItem item)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.tiff;*.tif|All Files|*.*",
            Title = "Select Your Drawing"
        };

        if (dialog.ShowDialog() == true)
        {
            var drawing = await _sessionService.UploadDrawingAsync(
                item.Result.Id, dialog.FileName, SelectedArtist?.Id);
            item.UploadedDrawings.Add(new UploadedDrawingItem
            {
                Drawing = drawing,
                DrawingPath = drawing.FilePath,
                ArtistName = SelectedArtist?.Name ?? string.Empty
            });
        }
    }

    [RelayCommand]
    private void Done()
    {
        _navigationService.NavigateTo<GalleryViewModel>();
    }

    [RelayCommand]
    private void BackToPlanner()
    {
        _navigationService.NavigateTo<SessionPlannerViewModel>();
    }

    private async Task LoadSessionAsync(int sessionId)
    {
        var artistList = await _artistService.GetAllArtistsAsync();
        Artists = new ObservableCollection<Artist>(artistList);

        var session = await _sessionService.GetSessionWithResultsAsync(sessionId);
        if (session == null) return;

        SessionSummary = $"Session: {session.SessionPlan?.Name ?? "Unknown"} - " +
                         $"{session.ExerciseResults.Count} exercises completed";

        var items = session.ExerciseResults
            .OrderBy(r => r.SortOrder)
            .Select(r => new ExerciseResultItem
            {
                Result = r,
                ReferencePhotoPath = r.ReferencePhoto?.FilePath ?? string.Empty,
                CategoryName = r.SessionExercise?.Tag?.Name ?? "Unknown",
                WasSkipped = r.WasSkipped,
                UploadedDrawings = new ObservableCollection<UploadedDrawingItem>(
                    r.CompletedDrawings.Select(cd => new UploadedDrawingItem
                    {
                        Drawing = cd,
                        DrawingPath = cd.FilePath,
                        ArtistName = cd.Artist?.Name ?? string.Empty
                    }))
            })
            .ToList();

        ExerciseResults = new ObservableCollection<ExerciseResultItem>(items);
    }
}
