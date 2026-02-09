using System.IO;
using DrawingTrainer.Data;
using DrawingTrainer.Services;
using DrawingTrainer.ViewModels;
using DrawingTrainer.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace DrawingTrainer;

public partial class App : Application
{
    private ServiceProvider _serviceProvider = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Ensure database is created
        using var scope = _serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DrawingTrainerDbContext>>();
        using var context = factory.CreateDbContext();
        context.Database.EnsureCreated();

        // Migrate: add ThumbnailPath column if missing
        MigrateSchema(context);

        var mainWindow = new MainWindow
        {
            DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();

        // Generate thumbnails for existing photos in background
        var thumbnailService = _serviceProvider.GetRequiredService<IThumbnailService>();
        _ = Task.Run(() => thumbnailService.GenerateMissingThumbnailsAsync());
    }

    private static void MigrateSchema(DrawingTrainerDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        connection.Open();

        // Migration 1: Add ThumbnailPath to ReferencePhotos
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(ReferencePhotos)";
            using var reader = cmd.ExecuteReader();

            bool hasThumbnailPath = false;
            while (reader.Read())
            {
                if (reader.GetString(1) == "ThumbnailPath")
                {
                    hasThumbnailPath = true;
                    break;
                }
            }
            reader.Close();

            if (!hasThumbnailPath)
            {
                using var alter = connection.CreateCommand();
                alter.CommandText = "ALTER TABLE ReferencePhotos ADD COLUMN ThumbnailPath TEXT NULL";
                alter.ExecuteNonQuery();
            }
        }

        // Migration 2: Add manual drawing fields to CompletedDrawings
        // (TagId, DurationSeconds, DrawnAt, ReferencePhotoId, make SessionExerciseResultId nullable)
        bool needsCompletedDrawingsMigration = false;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(CompletedDrawings)";
            using var reader = cmd.ExecuteReader();

            bool hasTagId = false;
            while (reader.Read())
            {
                if (reader.GetString(1) == "TagId")
                {
                    hasTagId = true;
                    break;
                }
            }
            reader.Close();
            needsCompletedDrawingsMigration = !hasTagId;
        }

        if (needsCompletedDrawingsMigration)
        {
            // SQLite doesn't support ALTER COLUMN, so we rebuild the table
            using var transaction = connection.BeginTransaction();
            try
            {
                using var pragmaOff = connection.CreateCommand();
                pragmaOff.CommandText = "PRAGMA foreign_keys=off";
                pragmaOff.ExecuteNonQuery();

                using var createNew = connection.CreateCommand();
                createNew.CommandText = @"
                    CREATE TABLE CompletedDrawings_new (
                        Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        SessionExerciseResultId INTEGER NULL,
                        FilePath TEXT NOT NULL,
                        OriginalFileName TEXT NOT NULL,
                        UploadedAt TEXT NOT NULL,
                        TagId INTEGER NULL,
                        DurationSeconds INTEGER NOT NULL DEFAULT 0,
                        DrawnAt TEXT NULL,
                        ReferencePhotoId INTEGER NULL,
                        FOREIGN KEY (SessionExerciseResultId) REFERENCES SessionExerciseResults(Id),
                        FOREIGN KEY (TagId) REFERENCES Tags(Id),
                        FOREIGN KEY (ReferencePhotoId) REFERENCES ReferencePhotos(Id)
                    )";
                createNew.ExecuteNonQuery();

                using var copyData = connection.CreateCommand();
                copyData.CommandText = @"
                    INSERT INTO CompletedDrawings_new (Id, SessionExerciseResultId, FilePath, OriginalFileName, UploadedAt, DurationSeconds)
                    SELECT Id, SessionExerciseResultId, FilePath, OriginalFileName, UploadedAt, 0
                    FROM CompletedDrawings";
                copyData.ExecuteNonQuery();

                using var dropOld = connection.CreateCommand();
                dropOld.CommandText = "DROP TABLE CompletedDrawings";
                dropOld.ExecuteNonQuery();

                using var rename = connection.CreateCommand();
                rename.CommandText = "ALTER TABLE CompletedDrawings_new RENAME TO CompletedDrawings";
                rename.ExecuteNonQuery();

                using var pragmaOn = connection.CreateCommand();
                pragmaOn.CommandText = "PRAGMA foreign_keys=on";
                pragmaOn.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        connection.Close();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Database
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DrawingTrainer", "drawingtrainer.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        services.AddDbContextFactory<DrawingTrainerDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Services
        services.AddSingleton<IPhotoStorageService, PhotoStorageService>();
        services.AddSingleton<IThumbnailService, ThumbnailService>();
        services.AddSingleton<IPhotoImportService, PhotoImportService>();
        services.AddSingleton<ISessionPlanService, SessionPlanService>();
        services.AddSingleton<IDrawingSessionService, DrawingSessionService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddTransient<ITimerService, TimerService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<PhotoImportViewModel>();
        services.AddTransient<SessionPlannerViewModel>();
        services.AddTransient<ActiveSessionViewModel>();
        services.AddTransient<PostSessionViewModel>();
        services.AddTransient<GalleryViewModel>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider.Dispose();
        base.OnExit(e);
    }
}
