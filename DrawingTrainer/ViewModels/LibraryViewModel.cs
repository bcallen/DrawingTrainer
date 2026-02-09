using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingTrainer.Data;
using DrawingTrainer.Models;
using DrawingTrainer.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace DrawingTrainer.ViewModels;

public partial class LibraryPhotoItem : ObservableObject
{
    public ReferencePhoto Photo { get; init; } = null!;

    [ObservableProperty]
    private bool _isSelected;

    public string DisplayImagePath =>
        !string.IsNullOrEmpty(Photo.ThumbnailPath) && File.Exists(Photo.ThumbnailPath)
            ? Photo.ThumbnailPath
            : Photo.FilePath;

    public string TagNames => string.Join(", ",
        Photo.ReferencePhotoTags?.Select(t => t.Tag?.Name).Where(n => n != null) ?? []);

    public void RefreshTagNames() => OnPropertyChanged(nameof(TagNames));
}

public partial class TagAssignment : ObservableObject
{
    public Tag Tag { get; init; } = null!;

    [ObservableProperty]
    private bool _isAssigned;

    [ObservableProperty]
    private bool _isPartial;
}

public partial class LibraryViewModel : ObservableObject
{
    private const int PageSize = 100;

    private readonly IDbContextFactory<DrawingTrainerDbContext> _contextFactory;
    private readonly INavigationService _navigationService;

    private int _loadedCount;
    private int _totalCount;

    [ObservableProperty]
    private ObservableCollection<LibraryPhotoItem> _photos = [];

    [ObservableProperty]
    private ObservableCollection<Tag> _tags = [];

    [ObservableProperty]
    private Tag? _selectedTag;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private bool _hasMorePhotos;

    [ObservableProperty]
    private ObservableCollection<TagAssignment> _tagAssignments = [];

    private List<Tag> _allTags = [];

    public LibraryViewModel(
        IDbContextFactory<DrawingTrainerDbContext> contextFactory,
        INavigationService navigationService)
    {
        _contextFactory = contextFactory;
        _navigationService = navigationService;
        _ = LoadAsync();
    }

    partial void OnSelectedTagChanged(Tag? value)
    {
        _ = LoadPhotosAsync();
    }

    [RelayCommand]
    private void NavigateToImport()
    {
        _navigationService.NavigateTo<PhotoImportViewModel>();
    }

    [RelayCommand]
    private void NavigateToAddDrawing()
    {
        var selected = Photos.Where(p => p.IsSelected).ToList();
        int? refPhotoId = selected.Count == 1 ? selected[0].Photo.Id : null;
        _navigationService.NavigateTo<GalleryViewModel>(vm => vm.RequestAddDrawing(refPhotoId));
    }

    [RelayCommand]
    private void TogglePhotoSelection(LibraryPhotoItem item)
    {
        item.IsSelected = !item.IsSelected;
        UpdateSelectionState();
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var p in Photos) p.IsSelected = true;
        UpdateSelectionState();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var p in Photos) p.IsSelected = false;
        UpdateSelectionState();
    }

    [RelayCommand]
    private async Task ToggleTagOnSelection(TagAssignment assignment)
    {
        var selected = Photos.Where(p => p.IsSelected).ToList();
        if (selected.Count == 0) return;

        var tagId = assignment.Tag.Id;
        // If all selected have it, remove. Otherwise, add to those missing it.
        bool shouldAdd = !assignment.IsAssigned;

        await using var context = await _contextFactory.CreateDbContextAsync();

        foreach (var item in selected)
        {
            var hasTag = item.Photo.ReferencePhotoTags.Any(rpt => rpt.TagId == tagId);

            if (shouldAdd && !hasTag)
            {
                var rpt = new ReferencePhotoTag
                {
                    ReferencePhotoId = item.Photo.Id,
                    TagId = tagId
                };
                context.ReferencePhotoTags.Add(rpt);
                item.Photo.ReferencePhotoTags.Add(new ReferencePhotoTag
                {
                    ReferencePhotoId = item.Photo.Id,
                    TagId = tagId,
                    Tag = assignment.Tag
                });
            }
            else if (!shouldAdd && hasTag)
            {
                var existing = await context.ReferencePhotoTags
                    .FirstOrDefaultAsync(rpt => rpt.ReferencePhotoId == item.Photo.Id && rpt.TagId == tagId);
                if (existing != null)
                    context.ReferencePhotoTags.Remove(existing);
                var local = item.Photo.ReferencePhotoTags.FirstOrDefault(rpt => rpt.TagId == tagId);
                if (local != null)
                    item.Photo.ReferencePhotoTags.Remove(local);
            }
        }

        await context.SaveChangesAsync();

        foreach (var item in selected)
            item.RefreshTagNames();

        UpdateTagAssignments();
    }

    [RelayCommand]
    private async Task DeletePhoto(LibraryPhotoItem item)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var dbPhoto = await context.ReferencePhotos
            .Include(p => p.ReferencePhotoTags)
            .FirstOrDefaultAsync(p => p.Id == item.Photo.Id);

        if (dbPhoto == null) return;

        context.ReferencePhotoTags.RemoveRange(dbPhoto.ReferencePhotoTags);
        context.ReferencePhotos.Remove(dbPhoto);
        await context.SaveChangesAsync();

        try { File.Delete(item.Photo.FilePath); } catch { }
        if (!string.IsNullOrEmpty(item.Photo.ThumbnailPath))
        {
            try { File.Delete(item.Photo.ThumbnailPath); } catch { }
        }

        Photos.Remove(item);
        _loadedCount--;
        _totalCount--;
        UpdateSelectionState();
    }

    [RelayCommand]
    private async Task DeleteSelected()
    {
        var selected = Photos.Where(p => p.IsSelected).ToList();
        if (selected.Count == 0) return;

        await using var context = await _contextFactory.CreateDbContextAsync();
        var ids = selected.Select(s => s.Photo.Id).ToList();

        var dbPhotos = await context.ReferencePhotos
            .Include(p => p.ReferencePhotoTags)
            .Where(p => ids.Contains(p.Id))
            .ToListAsync();

        foreach (var dbPhoto in dbPhotos)
        {
            context.ReferencePhotoTags.RemoveRange(dbPhoto.ReferencePhotoTags);
            context.ReferencePhotos.Remove(dbPhoto);
        }
        await context.SaveChangesAsync();

        foreach (var item in selected)
        {
            try { File.Delete(item.Photo.FilePath); } catch { }
            if (!string.IsNullOrEmpty(item.Photo.ThumbnailPath))
            {
                try { File.Delete(item.Photo.ThumbnailPath); } catch { }
            }
            Photos.Remove(item);
        }

        _loadedCount -= selected.Count;
        _totalCount -= selected.Count;
        UpdateSelectionState();
    }

    [RelayCommand]
    private async Task LoadMorePhotos()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        IQueryable<ReferencePhoto> query = context.ReferencePhotos
            .Include(rp => rp.ReferencePhotoTags)
                .ThenInclude(rpt => rpt.Tag);

        if (SelectedTag != null)
        {
            query = query.Where(rp => rp.ReferencePhotoTags.Any(rpt => rpt.TagId == SelectedTag.Id));
        }

        var morePhotos = await query
            .OrderByDescending(rp => rp.ImportedAt)
            .Skip(_loadedCount)
            .Take(PageSize)
            .ToListAsync();

        foreach (var p in morePhotos)
        {
            Photos.Add(new LibraryPhotoItem { Photo = p });
        }

        _loadedCount += morePhotos.Count;
        HasMorePhotos = _loadedCount < _totalCount;
    }

    private void UpdateSelectionState()
    {
        HasSelection = Photos.Any(p => p.IsSelected);
        if (HasSelection)
            UpdateTagAssignments();
    }

    private void UpdateTagAssignments()
    {
        var selected = Photos.Where(p => p.IsSelected).ToList();
        var assignments = new ObservableCollection<TagAssignment>();

        foreach (var tag in _allTags)
        {
            int count = selected.Count(s => s.Photo.ReferencePhotoTags.Any(rpt => rpt.TagId == tag.Id));
            assignments.Add(new TagAssignment
            {
                Tag = tag,
                IsAssigned = count == selected.Count,
                IsPartial = count > 0 && count < selected.Count
            });
        }

        TagAssignments = assignments;
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        await using var context = await _contextFactory.CreateDbContextAsync();

        _allTags = await context.Tags.OrderBy(t => t.Name).ToListAsync();
        Tags = new ObservableCollection<Tag>(_allTags);

        await LoadPhotosAsync();
        IsLoading = false;
    }

    private async Task LoadPhotosAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        IQueryable<ReferencePhoto> query = context.ReferencePhotos
            .Include(rp => rp.ReferencePhotoTags)
                .ThenInclude(rpt => rpt.Tag);

        if (SelectedTag != null)
        {
            query = query.Where(rp => rp.ReferencePhotoTags.Any(rpt => rpt.TagId == SelectedTag.Id));
        }

        _totalCount = await query.CountAsync();

        var photos = await query
            .OrderByDescending(rp => rp.ImportedAt)
            .Take(PageSize)
            .ToListAsync();

        Photos = new ObservableCollection<LibraryPhotoItem>(
            photos.Select(p => new LibraryPhotoItem { Photo = p }));
        _loadedCount = photos.Count;
        HasMorePhotos = _loadedCount < _totalCount;
        HasSelection = false;
    }
}
