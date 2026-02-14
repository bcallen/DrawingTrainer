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

        // Migration 3: Add Artists table and ArtistId to CompletedDrawings (+ remove unique constraint for one-to-many)
        bool needsArtistMigration = false;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Artists'";
            needsArtistMigration = cmd.ExecuteScalar() == null;
        }

        if (needsArtistMigration)
        {
            using var transaction3 = connection.BeginTransaction();
            try
            {
                using var createArtists = connection.CreateCommand();
                createArtists.CommandText = @"
                    CREATE TABLE Artists (
                        Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL
                    )";
                createArtists.ExecuteNonQuery();

                // Rebuild CompletedDrawings to add ArtistId and remove unique index on SessionExerciseResultId
                using var pragmaOff3 = connection.CreateCommand();
                pragmaOff3.CommandText = "PRAGMA foreign_keys=off";
                pragmaOff3.ExecuteNonQuery();

                using var createNew3 = connection.CreateCommand();
                createNew3.CommandText = @"
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
                        ArtistId INTEGER NULL,
                        FOREIGN KEY (SessionExerciseResultId) REFERENCES SessionExerciseResults(Id),
                        FOREIGN KEY (TagId) REFERENCES Tags(Id),
                        FOREIGN KEY (ReferencePhotoId) REFERENCES ReferencePhotos(Id),
                        FOREIGN KEY (ArtistId) REFERENCES Artists(Id)
                    )";
                createNew3.ExecuteNonQuery();

                using var copyData3 = connection.CreateCommand();
                copyData3.CommandText = @"
                    INSERT INTO CompletedDrawings_new (Id, SessionExerciseResultId, FilePath, OriginalFileName, UploadedAt, TagId, DurationSeconds, DrawnAt, ReferencePhotoId)
                    SELECT Id, SessionExerciseResultId, FilePath, OriginalFileName, UploadedAt, TagId, DurationSeconds, DrawnAt, ReferencePhotoId
                    FROM CompletedDrawings";
                copyData3.ExecuteNonQuery();

                using var dropOld3 = connection.CreateCommand();
                dropOld3.CommandText = "DROP TABLE CompletedDrawings";
                dropOld3.ExecuteNonQuery();

                using var rename3 = connection.CreateCommand();
                rename3.CommandText = "ALTER TABLE CompletedDrawings_new RENAME TO CompletedDrawings";
                rename3.ExecuteNonQuery();

                using var pragmaOn3 = connection.CreateCommand();
                pragmaOn3.CommandText = "PRAGMA foreign_keys=on";
                pragmaOn3.ExecuteNonQuery();

                transaction3.Commit();
            }
            catch
            {
                transaction3.Rollback();
                throw;
            }
        }

        // Migration 4: Assign all unattributed drawings to "Brian"
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                UPDATE CompletedDrawings
                SET ArtistId = (SELECT Id FROM Artists WHERE Name = 'Brian' LIMIT 1)
                WHERE ArtistId IS NULL
                  AND EXISTS (SELECT 1 FROM Artists WHERE Name = 'Brian')";
            cmd.ExecuteNonQuery();
        }

        // Migration 5: Make SessionExerciseId nullable in SessionExerciseResults
        // (allows deleting exercises from plans without breaking past session history)
        bool needsSerMigration = false;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(SessionExerciseResults)";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (reader.GetString(1) == "SessionExerciseId")
                {
                    // Column 3 is 'notnull' flag: 1 = NOT NULL, 0 = nullable
                    needsSerMigration = reader.GetInt32(3) == 1;
                    break;
                }
            }
        }

        if (needsSerMigration)
        {
            // PRAGMA foreign_keys must be set OUTSIDE the transaction to take effect in SQLite
            using var pragmaOff5 = connection.CreateCommand();
            pragmaOff5.CommandText = "PRAGMA foreign_keys=off";
            pragmaOff5.ExecuteNonQuery();

            using var transaction5 = connection.BeginTransaction();
            try
            {
                using var createNew5 = connection.CreateCommand();
                createNew5.CommandText = @"
                    CREATE TABLE SessionExerciseResults_new (
                        Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        DrawingSessionId INTEGER NOT NULL,
                        SessionExerciseId INTEGER NULL,
                        ReferencePhotoId INTEGER NOT NULL,
                        SortOrder INTEGER NOT NULL,
                        WasSkipped INTEGER NOT NULL,
                        FOREIGN KEY (DrawingSessionId) REFERENCES DrawingSessions(Id),
                        FOREIGN KEY (SessionExerciseId) REFERENCES SessionExercises(Id) ON DELETE SET NULL,
                        FOREIGN KEY (ReferencePhotoId) REFERENCES ReferencePhotos(Id)
                    )";
                createNew5.ExecuteNonQuery();

                using var copyData5 = connection.CreateCommand();
                copyData5.CommandText = @"
                    INSERT INTO SessionExerciseResults_new (Id, DrawingSessionId, SessionExerciseId, ReferencePhotoId, SortOrder, WasSkipped)
                    SELECT Id, DrawingSessionId, SessionExerciseId, ReferencePhotoId, SortOrder, WasSkipped
                    FROM SessionExerciseResults";
                copyData5.ExecuteNonQuery();

                using var dropOld5 = connection.CreateCommand();
                dropOld5.CommandText = "DROP TABLE SessionExerciseResults";
                dropOld5.ExecuteNonQuery();

                using var rename5 = connection.CreateCommand();
                rename5.CommandText = "ALTER TABLE SessionExerciseResults_new RENAME TO SessionExerciseResults";
                rename5.ExecuteNonQuery();

                transaction5.Commit();
            }
            catch
            {
                transaction5.Rollback();
                throw;
            }

            using var pragmaOn5 = connection.CreateCommand();
            pragmaOn5.CommandText = "PRAGMA foreign_keys=on";
            pragmaOn5.ExecuteNonQuery();
        }

        // Migration 6: Backfill TagId, DurationSeconds, ReferencePhotoId on session-based CompletedDrawings
        // Previously these fields were only set for manual uploads; session drawings relied on navigation chain
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                UPDATE CompletedDrawings
                SET TagId = COALESCE(TagId, (
                        SELECT se.TagId
                        FROM SessionExerciseResults ser
                        JOIN SessionExercises se ON ser.SessionExerciseId = se.Id
                        WHERE ser.Id = CompletedDrawings.SessionExerciseResultId
                    )),
                    DurationSeconds = COALESCE(NULLIF(DurationSeconds, 0), (
                        SELECT se.DurationSeconds
                        FROM SessionExerciseResults ser
                        JOIN SessionExercises se ON ser.SessionExerciseId = se.Id
                        WHERE ser.Id = CompletedDrawings.SessionExerciseResultId
                    ), 0),
                    ReferencePhotoId = COALESCE(ReferencePhotoId, (
                        SELECT ser.ReferencePhotoId
                        FROM SessionExerciseResults ser
                        WHERE ser.Id = CompletedDrawings.SessionExerciseResultId
                    ))
                WHERE SessionExerciseResultId IS NOT NULL
                  AND (TagId IS NULL OR DurationSeconds = 0)";
            cmd.ExecuteNonQuery();
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
        services.AddSingleton<IArtistService, ArtistService>();
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
        services.AddTransient<ArtistManagementViewModel>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider.Dispose();
        base.OnExit(e);
    }
}
