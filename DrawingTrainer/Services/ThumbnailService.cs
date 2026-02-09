using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using DrawingTrainer.Data;
using DrawingTrainer.Helpers;
using Microsoft.EntityFrameworkCore;

namespace DrawingTrainer.Services;

public interface IThumbnailService
{
    Task<string> GenerateThumbnailAsync(string sourceImagePath, int maxDimension = 200);
    Task GenerateMissingThumbnailsAsync(CancellationToken cancellationToken = default);
}

public class ThumbnailService : IThumbnailService
{
    private readonly string _thumbnailBasePath;
    private readonly IDbContextFactory<DrawingTrainerDbContext> _contextFactory;

    public ThumbnailService(IDbContextFactory<DrawingTrainerDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
        _thumbnailBasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DrawingTrainer", "Photos", "thumbnails");
        Directory.CreateDirectory(_thumbnailBasePath);
    }

    public async Task<string> GenerateThumbnailAsync(string sourceImagePath, int maxDimension = 200)
    {
        var dateFolder = DateTime.Now.ToString("yyyy-MM");
        var targetDir = Path.Combine(_thumbnailBasePath, dateFolder);
        Directory.CreateDirectory(targetDir);
        var thumbnailPath = Path.Combine(targetDir, $"{Guid.NewGuid()}.jpg");

        await Task.Run(() =>
        {
            // Read EXIF orientation from the source
            using var metaStream = new FileStream(sourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var frame = BitmapFrame.Create(metaStream,
                BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.OnLoad);
            int orientation = ExifHelper.GetExifOrientation(frame);

            // Decode to thumbnail size
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(sourceImagePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            if (orientation >= 5 && orientation <= 8)
                bitmap.DecodePixelHeight = maxDimension;
            else
                bitmap.DecodePixelWidth = maxDimension;
            bitmap.EndInit();
            bitmap.Freeze();

            // Apply EXIF rotation so thumbnail is pre-oriented
            var oriented = ExifHelper.ApplyExifOrientation(bitmap, orientation);
            oriented.Freeze();

            // Encode as JPEG
            var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
            encoder.Frames.Add(BitmapFrame.Create(oriented));

            using var output = File.Create(thumbnailPath);
            encoder.Save(output);
        });

        return thumbnailPath;
    }

    public async Task GenerateMissingThumbnailsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var photosNeedingThumbnails = await context.ReferencePhotos
            .Where(p => p.ThumbnailPath == null || p.ThumbnailPath == "")
            .Select(p => new { p.Id, p.FilePath })
            .ToListAsync(cancellationToken);

        foreach (var photo in photosNeedingThumbnails)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (!File.Exists(photo.FilePath))
                continue;

            try
            {
                var thumbnailPath = await GenerateThumbnailAsync(photo.FilePath);

                // Update DB with a fresh context to avoid tracking issues
                await using var updateContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
                var dbPhoto = await updateContext.ReferencePhotos.FindAsync([photo.Id], cancellationToken);
                if (dbPhoto != null)
                {
                    dbPhoto.ThumbnailPath = thumbnailPath;
                    await updateContext.SaveChangesAsync(cancellationToken);
                }
            }
            catch
            {
                // Skip photos that fail thumbnail generation
            }
        }
    }
}
