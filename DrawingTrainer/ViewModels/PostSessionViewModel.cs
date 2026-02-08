using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingTrainer.Models;
using DrawingTrainer.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;

namespace DrawingTrainer.ViewModels;

public partial class ExerciseResultItem : ObservableObject
{
    public SessionExerciseResult Result { get; set; } = null!;

    [ObservableProperty]
    private string _referencePhotoPath = string.Empty;

    [ObservableProperty]
    private string _categoryName = string.Empty;

    [ObservableProperty]
    private string _drawingPath = string.Empty;

    [ObservableProperty]
    private bool _hasDrawing;

    [ObservableProperty]
    private bool _wasSkipped;
}

public partial class PostSessionViewModel : ObservableObject
{
    private readonly IDrawingSessionService _sessionService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ObservableCollection<ExerciseResultItem> _exerciseResults = [];

    [ObservableProperty]
    private string _sessionSummary = string.Empty;

    public PostSessionViewModel(
        IDrawingSessionService sessionService,
        INavigationService navigationService)
    {
        _sessionService = sessionService;
        _navigationService = navigationService;
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
            var drawing = await _sessionService.UploadDrawingAsync(item.Result.Id, dialog.FileName);
            item.DrawingPath = drawing.FilePath;
            item.HasDrawing = true;
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
                DrawingPath = r.CompletedDrawing?.FilePath ?? string.Empty,
                HasDrawing = r.CompletedDrawing != null,
                WasSkipped = r.WasSkipped
            })
            .ToList();

        ExerciseResults = new ObservableCollection<ExerciseResultItem>(items);
    }
}
