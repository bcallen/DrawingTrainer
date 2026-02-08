using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DrawingTrainer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var icoPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (File.Exists(icoPath))
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(icoPath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            Icon = bitmap;
        }
    }
}
