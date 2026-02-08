using System.IO;
using DrawingTrainer.Data;
using DrawingTrainer.Models;
using Microsoft.EntityFrameworkCore;
using System.Windows.Media.Imaging;

namespace DrawingTrainer.Services;

public interface IPhotoImportService
{
    Task<ReferencePhoto> ImportPhotoAsync(string filePath, List<int> tagIds);
    Task<List<ReferencePhoto>> ImportPhotosFromFolderAsync(string folderPath, List<int> tagIds, IProgress<int>? progress = null);
    bool IsValidImageFile(string filePath);
}

public class PhotoImportService : IPhotoImportService
{
    private static readonly HashSet<string> ValidExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif"
    };

    private readonly IDbContextFactory<DrawingTrainerDbContext> _contextFactory;
    private readonly IPhotoStorageService _storageService;

    public PhotoImportService(
        IDbContextFactory<DrawingTrainerDbContext> contextFactory,
        IPhotoStorageService storageService)
    {
        _contextFactory = contextFactory;
        _storageService = storageService;
    }

    public bool IsValidImageFile(string filePath)
    {
        return ValidExtensions.Contains(Path.GetExtension(filePath));
    }

    public async Task<ReferencePhoto> ImportPhotoAsync(string filePath, List<int> tagIds)
    {
        var storedPath = await _storageService.StoreReferencePhotoAsync(filePath);
        var (width, height) = GetImageDimensions(storedPath);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var photo = new ReferencePhoto
        {
            FilePath = storedPath,
            OriginalFileName = Path.GetFileName(filePath),
            Width = width,
            Height = height,
            ImportedAt = DateTime.Now,
        };

        context.ReferencePhotos.Add(photo);
        await context.SaveChangesAsync();

        foreach (var tagId in tagIds)
        {
            context.ReferencePhotoTags.Add(new ReferencePhotoTag
            {
                ReferencePhotoId = photo.Id,
                TagId = tagId
            });
        }
        await context.SaveChangesAsync();

        return photo;
    }

    public async Task<List<ReferencePhoto>> ImportPhotosFromFolderAsync(
        string folderPath, List<int> tagIds, IProgress<int>? progress = null)
    {
        var files = Directory.GetFiles(folderPath)
            .Where(IsValidImageFile)
            .ToList();

        var imported = new List<ReferencePhoto>();
        for (int i = 0; i < files.Count; i++)
        {
            var photo = await ImportPhotoAsync(files[i], tagIds);
            imported.Add(photo);
            progress?.Report(i + 1);
        }

        return imported;
    }

    private static (int width, int height) GetImageDimensions(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var decoder = BitmapDecoder.Create(stream,
                BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            var frame = decoder.Frames[0];
            return (frame.PixelWidth, frame.PixelHeight);
        }
        catch
        {
            return (0, 0);
        }
    }
}
