using System.IO;

namespace DrawingTrainer.Services;

public interface IPhotoStorageService
{
    string GetStorageBasePath();
    Task<string> StoreReferencePhotoAsync(string sourceFilePath);
    Task<string> StoreDrawingPhotoAsync(string sourceFilePath);
}

public class PhotoStorageService : IPhotoStorageService
{
    private readonly string _basePath;

    public PhotoStorageService()
    {
        _basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DrawingTrainer", "Photos");
        Directory.CreateDirectory(Path.Combine(_basePath, "references"));
        Directory.CreateDirectory(Path.Combine(_basePath, "drawings"));
    }

    public string GetStorageBasePath() => _basePath;

    public async Task<string> StoreReferencePhotoAsync(string sourceFilePath)
    {
        return await StoreFileAsync(sourceFilePath, "references");
    }

    public async Task<string> StoreDrawingPhotoAsync(string sourceFilePath)
    {
        return await StoreFileAsync(sourceFilePath, "drawings");
    }

    private async Task<string> StoreFileAsync(string sourceFilePath, string subfolder)
    {
        var dateFolder = DateTime.Now.ToString("yyyy-MM");
        var extension = Path.GetExtension(sourceFilePath);
        var fileName = $"{Guid.NewGuid()}{extension}";
        var targetDir = Path.Combine(_basePath, subfolder, dateFolder);
        Directory.CreateDirectory(targetDir);
        var targetPath = Path.Combine(targetDir, fileName);

        await using var source = File.OpenRead(sourceFilePath);
        await using var target = File.Create(targetPath);
        await source.CopyToAsync(target);

        return targetPath;
    }
}
