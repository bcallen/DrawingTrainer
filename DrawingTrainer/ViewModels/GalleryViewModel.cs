using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingTrainer.Data;
using DrawingTrainer.Models;
using DrawingTrainer.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace DrawingTrainer.ViewModels;

public partial class GalleryItem : ObservableObject
{
    public CompletedDrawing Drawing { get; set; } = null!;

    [ObservableProperty]
    private string _drawingPath = string.Empty;

    [ObservableProperty]
    private string _referencePath = string.Empty;

    [ObservableProperty]
    private string _categoryName = string.Empty;

    [ObservableProperty]
    private DateTime _date;

    [ObservableProperty]
    private bool _showReference;

    [ObservableProperty]
    private int _durationSeconds;

    public string DurationText
    {
        get
        {
            if (DurationSeconds <= 0) return string.Empty;
            var ts = TimeSpan.FromSeconds(DurationSeconds);
            if (ts.TotalMinutes >= 1)
                return ts.Seconds > 0 ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s" : $"{(int)ts.TotalMinutes}m";
            return $"{ts.Seconds}s";
        }
    }
}

public partial class GalleryViewModel : ObservableObject
{
    private readonly IDrawingSessionService _sessionService;
    private readonly IDbContextFactory<DrawingTrainerDbContext> _contextFactory;

    [ObservableProperty]
    private ObservableCollection<GalleryItem> _drawings = [];

    [ObservableProperty]
    private ObservableCollection<Tag> _tags = [];

    [ObservableProperty]
    private Tag? _selectedTag;

    [ObservableProperty]
    private GalleryItem? _selectedDrawing;

    [ObservableProperty]
    private bool _isLoading;

    public GalleryViewModel(
        IDrawingSessionService sessionService,
        IDbContextFactory<DrawingTrainerDbContext> contextFactory)
    {
        _sessionService = sessionService;
        _contextFactory = contextFactory;
        _ = LoadAsync();
    }

    partial void OnSelectedTagChanged(Tag? value)
    {
        _ = LoadDrawingsAsync();
    }

    [RelayCommand]
    private void ToggleReference(GalleryItem item)
    {
        item.ShowReference = !item.ShowReference;
    }

    [RelayCommand]
    private void SelectDrawing(GalleryItem item)
    {
        SelectedDrawing = item;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        SelectedDrawing = null;
    }

    [RelayCommand]
    private void NavigateNext()
    {
        if (SelectedDrawing == null || Drawings.Count == 0) return;
        var index = Drawings.IndexOf(SelectedDrawing);
        if (index < Drawings.Count - 1)
            SelectedDrawing = Drawings[index + 1];
    }

    [RelayCommand]
    private void NavigatePrevious()
    {
        if (SelectedDrawing == null || Drawings.Count == 0) return;
        var index = Drawings.IndexOf(SelectedDrawing);
        if (index > 0)
            SelectedDrawing = Drawings[index - 1];
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        await using var context = await _contextFactory.CreateDbContextAsync();
        var tags = await context.Tags.OrderBy(t => t.Name).ToListAsync();
        Tags = new ObservableCollection<Tag>(tags);

        await LoadDrawingsAsync();
        IsLoading = false;
    }

    private async Task LoadDrawingsAsync()
    {
        var drawings = await _sessionService.GetDrawingsAsync(SelectedTag?.Id);
        Drawings = new ObservableCollection<GalleryItem>(
            drawings.Select(d => new GalleryItem
            {
                Drawing = d,
                DrawingPath = d.FilePath,
                ReferencePath = d.SessionExerciseResult?.ReferencePhoto?.FilePath ?? string.Empty,
                CategoryName = d.SessionExerciseResult?.SessionExercise?.Tag?.Name ?? "Unknown",
                Date = d.UploadedAt,
                DurationSeconds = d.SessionExerciseResult?.SessionExercise?.DurationSeconds ?? 0
            }));
    }
}
