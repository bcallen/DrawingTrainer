using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingTrainer.Data;
using DrawingTrainer.Models;
using DrawingTrainer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;

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

    [ObservableProperty]
    private string _artistName = string.Empty;

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

public class ReferencePhotoPickItem
{
    public ReferencePhoto Photo { get; init; } = null!;
    public string DisplayPath =>
        !string.IsNullOrEmpty(Photo.ThumbnailPath) ? Photo.ThumbnailPath : Photo.FilePath;
}

public partial class GalleryViewModel : ObservableObject
{
    private readonly IDrawingSessionService _sessionService;
    private readonly IDbContextFactory<DrawingTrainerDbContext> _contextFactory;
    private readonly IArtistService _artistService;

    private bool _pendingAddDrawing;
    private int? _pendingReferencePhotoId;

    [ObservableProperty]
    private ObservableCollection<GalleryItem> _drawings = [];

    [ObservableProperty]
    private ObservableCollection<Tag> _tags = [];

    [ObservableProperty]
    private Tag? _selectedTag;

    [ObservableProperty]
    private ObservableCollection<Artist> _artists = [];

    [ObservableProperty]
    private Artist? _selectedArtist;

    [ObservableProperty]
    private GalleryItem? _selectedDrawing;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSideBySide;

    // Add Drawing form state
    [ObservableProperty]
    private bool _isAddingDrawing;

    [ObservableProperty]
    private Tag? _addTag;

    [ObservableProperty]
    private DateTime _addDate = DateTime.Today;

    [ObservableProperty]
    private string _addDurationMinutes = "5";

    [ObservableProperty]
    private string _addDurationSeconds = "0";

    [ObservableProperty]
    private string _addDrawingFilePath = string.Empty;

    [ObservableProperty]
    private ReferencePhoto? _addReferencePhoto;

    [ObservableProperty]
    private ObservableCollection<ReferencePhotoPickItem> _referencePhotos = [];

    [ObservableProperty]
    private bool _isPickingReferencePhoto;

    [ObservableProperty]
    private Artist? _addArtist;

    public string AddDrawingFileName => string.IsNullOrEmpty(AddDrawingFilePath)
        ? string.Empty
        : Path.GetFileName(AddDrawingFilePath);

    public string AddReferencePhotoDisplayPath
    {
        get
        {
            if (AddReferencePhoto == null) return string.Empty;
            if (!string.IsNullOrEmpty(AddReferencePhoto.ThumbnailPath))
                return AddReferencePhoto.ThumbnailPath;
            return AddReferencePhoto.FilePath;
        }
    }

    public GalleryViewModel(
        IDrawingSessionService sessionService,
        IDbContextFactory<DrawingTrainerDbContext> contextFactory,
        IArtistService artistService)
    {
        _sessionService = sessionService;
        _contextFactory = contextFactory;
        _artistService = artistService;
        _ = LoadAsync();
    }

    /// <summary>
    /// Called from NavigateTo configure callback to auto-open form after load.
    /// Handles both sync and async LoadAsync completion.
    /// </summary>
    public void RequestAddDrawing(int? referencePhotoId = null)
    {
        _pendingReferencePhotoId = referencePhotoId;
        if (!IsLoading)
        {
            // LoadAsync already completed (synchronous path)
            _ = ShowAddDrawingWithReference(referencePhotoId);
        }
        else
        {
            _pendingAddDrawing = true;
        }
    }

    partial void OnSelectedTagChanged(Tag? value)
    {
        _ = LoadDrawingsAsync();
    }

    partial void OnSelectedArtistChanged(Artist? value)
    {
        _ = LoadDrawingsAsync();
    }

    partial void OnAddDrawingFilePathChanged(string value)
    {
        OnPropertyChanged(nameof(AddDrawingFileName));
    }

    partial void OnAddReferencePhotoChanged(ReferencePhoto? value)
    {
        OnPropertyChanged(nameof(AddReferencePhotoDisplayPath));
        IsPickingReferencePhoto = false;
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

    [RelayCommand]
    private void ToggleSideBySide()
    {
        IsSideBySide = !IsSideBySide;
    }

    [RelayCommand]
    private async Task ShowAddDrawing()
    {
        int? refId = SelectedDrawing?.Drawing.ReferencePhotoId
            ?? SelectedDrawing?.Drawing.SessionExerciseResult?.ReferencePhotoId;
        await ShowAddDrawingWithReference(refId);
    }

    private async Task ShowAddDrawingWithReference(int? referencePhotoId)
    {
        AddTag = null;
        AddDate = DateTime.Today;
        AddDurationMinutes = "5";
        AddDurationSeconds = "0";
        AddDrawingFilePath = string.Empty;
        AddReferencePhoto = null;
        AddArtist = null;
        IsPickingReferencePhoto = false;

        await using var context = await _contextFactory.CreateDbContextAsync();
        var photos = await context.ReferencePhotos.OrderBy(rp => rp.OriginalFileName).ToListAsync();
        ReferencePhotos = new ObservableCollection<ReferencePhotoPickItem>(
            photos.Select(p => new ReferencePhotoPickItem { Photo = p }));

        if (referencePhotoId.HasValue)
        {
            AddReferencePhoto = photos.FirstOrDefault(p => p.Id == referencePhotoId.Value);
        }

        IsAddingDrawing = true;
    }

    [RelayCommand]
    private void CancelAddDrawing()
    {
        IsAddingDrawing = false;
        IsPickingReferencePhoto = false;
    }

    [RelayCommand]
    private void BrowseDrawingFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Drawing Photo",
            Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            AddDrawingFilePath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void ToggleReferencePicker()
    {
        IsPickingReferencePhoto = !IsPickingReferencePhoto;
    }

    [RelayCommand]
    private void SelectReferencePhoto(ReferencePhotoPickItem item)
    {
        AddReferencePhoto = item.Photo;
        // OnAddReferencePhotoChanged closes the picker
    }

    [RelayCommand]
    private void ClearReferencePhoto()
    {
        AddReferencePhoto = null;
    }

    [RelayCommand]
    private async Task SaveManualDrawing()
    {
        if (AddTag == null || string.IsNullOrEmpty(AddDrawingFilePath))
            return;

        int.TryParse(AddDurationMinutes, out var minutes);
        int.TryParse(AddDurationSeconds, out var seconds);
        var totalSeconds = (minutes * 60) + seconds;

        await _sessionService.UploadManualDrawingAsync(
            AddDrawingFilePath,
            AddTag.Id,
            totalSeconds,
            AddDate,
            AddReferencePhoto?.Id,
            AddArtist?.Id);

        IsAddingDrawing = false;
        IsPickingReferencePhoto = false;
        await LoadDrawingsAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        await using var context = await _contextFactory.CreateDbContextAsync();
        var tags = await context.Tags.OrderBy(t => t.Name).ToListAsync();
        Tags = new ObservableCollection<Tag>(tags);

        var artists = await _artistService.GetAllArtistsAsync();
        Artists = new ObservableCollection<Artist>(artists);

        await LoadDrawingsAsync();
        IsLoading = false;

        if (_pendingAddDrawing)
        {
            _pendingAddDrawing = false;
            await ShowAddDrawingWithReference(_pendingReferencePhotoId);
        }
    }

    private async Task LoadDrawingsAsync()
    {
        var drawings = await _sessionService.GetDrawingsAsync(SelectedTag?.Id, SelectedArtist?.Id);
        Drawings = new ObservableCollection<GalleryItem>(
            drawings.Select(d => new GalleryItem
            {
                Drawing = d,
                DrawingPath = d.FilePath,
                ReferencePath = d.ReferencePhoto?.FilePath
                    ?? d.SessionExerciseResult?.ReferencePhoto?.FilePath
                    ?? string.Empty,
                CategoryName = d.Tag?.Name
                    ?? d.SessionExerciseResult?.SessionExercise?.Tag?.Name
                    ?? "Unknown",
                Date = d.DrawnAt ?? d.UploadedAt,
                DurationSeconds = d.DurationSeconds > 0
                    ? d.DurationSeconds
                    : d.SessionExerciseResult?.SessionExercise?.DurationSeconds ?? 0,
                ArtistName = d.Artist?.Name ?? string.Empty
            }));
    }
}
