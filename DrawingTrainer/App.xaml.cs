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

        var mainWindow = new MainWindow
        {
            DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();
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
