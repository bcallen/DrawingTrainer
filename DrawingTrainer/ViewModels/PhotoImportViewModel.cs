using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingTrainer.Data;
using DrawingTrainer.Models;
using DrawingTrainer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System.Collections.ObjectModel;

namespace DrawingTrainer.ViewModels;

public partial class PhotoImportViewModel : ObservableObject
{
    private readonly IPhotoImportService _importService;
    private readonly IDbContextFactory<DrawingTrainerDbContext> _contextFactory;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ObservableCollection<Tag> _tags = [];

    [ObservableProperty]
    private ObservableCollection<Tag> _selectedTags = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    private ObservableCollection<string> _selectedFiles = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    private bool _isImporting;

    [ObservableProperty]
    private int _importProgress;

    [ObservableProperty]
    private int _importTotal;

    [ObservableProperty]
    private string _statusText = "Select files to import";

    public PhotoImportViewModel(
        IPhotoImportService importService,
        IDbContextFactory<DrawingTrainerDbContext> contextFactory,
        INavigationService navigationService)
    {
        _importService = importService;
        _contextFactory = contextFactory;
        _navigationService = navigationService;
        _ = LoadTagsAsync();
    }

    [RelayCommand]
    private void SelectFiles()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.tiff;*.tif|All Files|*.*",
            Title = "Select Reference Photos"
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedFiles = new ObservableCollection<string>(dialog.FileNames);
            StatusText = $"{SelectedFiles.Count} file(s) selected";
        }
    }

    [RelayCommand]
    private void SelectFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Folder with Reference Photos"
        };

        if (dialog.ShowDialog() == true)
        {
            var files = Directory.GetFiles(dialog.FolderName)
                .Where(f => _importService.IsValidImageFile(f))
                .ToList();
            SelectedFiles = new ObservableCollection<string>(files);
            StatusText = $"{SelectedFiles.Count} image(s) found in folder";
        }
    }

    [RelayCommand]
    private void ToggleTag(Tag tag)
    {
        if (SelectedTags.Contains(tag))
            SelectedTags.Remove(tag);
        else
            SelectedTags.Add(tag);
        ImportCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task Import()
    {
        if (SelectedFiles.Count == 0 || SelectedTags.Count == 0) return;

        IsImporting = true;
        ImportTotal = SelectedFiles.Count;
        ImportProgress = 0;

        var tagIds = SelectedTags.Select(t => t.Id).ToList();
        var progress = new Progress<int>(p =>
        {
            ImportProgress = p;
            StatusText = $"Importing {p}/{ImportTotal}...";
        });

        try
        {
            foreach (var file in SelectedFiles)
            {
                await _importService.ImportPhotoAsync(file, tagIds);
                ImportProgress++;
                StatusText = $"Importing {ImportProgress}/{ImportTotal}...";
            }

            StatusText = $"Successfully imported {ImportTotal} photo(s)!";
            await Task.Delay(1500);
            _navigationService.NavigateTo<LibraryViewModel>();
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
        }
    }

    private bool CanImport() => SelectedFiles.Count > 0 && SelectedTags.Count > 0 && !IsImporting;

    [RelayCommand]
    private void Cancel()
    {
        _navigationService.NavigateTo<LibraryViewModel>();
    }

    private async Task LoadTagsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var tags = await context.Tags.OrderBy(t => t.Name).ToListAsync();
        Tags = new ObservableCollection<Tag>(tags);
    }
}
