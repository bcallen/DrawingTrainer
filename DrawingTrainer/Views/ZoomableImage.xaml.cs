using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DrawingTrainer.Views;

public partial class ZoomableImage : UserControl
{
    private Point _lastMousePos;
    private bool _isDragging;

    private const double MinScale = 1.0;
    private const double MaxScale = 10.0;
    private const double ZoomFactor = 1.15;

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(ImageSource), typeof(ZoomableImage),
            new PropertyMetadata(null, OnSourceChanged));

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public ZoomableImage()
    {
        InitializeComponent();

        MouseWheel += OnMouseWheel;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;
        MouseDoubleClick += OnMouseDoubleClick;
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomableImage control)
        {
            control.Img.Source = e.NewValue as ImageSource;
            control.ResetZoom();
        }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var mousePos = e.GetPosition(Container);
        double scale = e.Delta > 0 ? ZoomFactor : 1.0 / ZoomFactor;

        double newScaleX = ScaleTransform.ScaleX * scale;
        double newScaleY = ScaleTransform.ScaleY * scale;

        if (newScaleX < MinScale || newScaleX > MaxScale)
            return;

        // Zoom toward mouse position
        var imgPos = Container.TranslatePoint(mousePos, Img);
        double relX = imgPos.X / Img.ActualWidth;
        double relY = imgPos.Y / Img.ActualHeight;

        ScaleTransform.ScaleX = newScaleX;
        ScaleTransform.ScaleY = newScaleY;

        // Adjust translation so the point under the mouse stays fixed
        Img.UpdateLayout();
        var newImgPos = Container.TranslatePoint(mousePos, Img);
        double deltaX = (newImgPos.X / Img.ActualWidth - relX) * Img.ActualWidth * newScaleX;
        double deltaY = (newImgPos.Y / Img.ActualHeight - relY) * Img.ActualHeight * newScaleY;

        TranslateTransform.X += deltaX;
        TranslateTransform.Y += deltaY;

        ClampPan();
        e.Handled = true;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ScaleTransform.ScaleX <= MinScale)
            return;

        _isDragging = true;
        _lastMousePos = e.GetPosition(this);
        CaptureMouse();
        Cursor = Cursors.Hand;
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
            e.Handled = true;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        var currentPos = e.GetPosition(this);
        double dx = currentPos.X - _lastMousePos.X;
        double dy = currentPos.Y - _lastMousePos.Y;
        _lastMousePos = currentPos;

        TranslateTransform.X += dx;
        TranslateTransform.Y += dy;
        ClampPan();
    }

    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ScaleTransform.ScaleX > MinScale)
        {
            ResetZoom();
        }
        else
        {
            // Zoom to 3x on double-click, centered on click point
            var mousePos = e.GetPosition(Container);
            double targetScale = 3.0;

            ScaleTransform.ScaleX = targetScale;
            ScaleTransform.ScaleY = targetScale;

            // Center the zoom on the clicked point
            double containerCenterX = Container.ActualWidth / 2;
            double containerCenterY = Container.ActualHeight / 2;
            TranslateTransform.X = (containerCenterX - mousePos.X) * (targetScale - 1) / targetScale;
            TranslateTransform.Y = (containerCenterY - mousePos.Y) * (targetScale - 1) / targetScale;

            ClampPan();
        }
        e.Handled = true;
    }

    private void ResetZoom()
    {
        ScaleTransform.ScaleX = 1.0;
        ScaleTransform.ScaleY = 1.0;
        TranslateTransform.X = 0;
        TranslateTransform.Y = 0;
    }

    private void ClampPan()
    {
        if (ScaleTransform.ScaleX <= MinScale)
        {
            TranslateTransform.X = 0;
            TranslateTransform.Y = 0;
            return;
        }

        double imgW = Img.ActualWidth * ScaleTransform.ScaleX;
        double imgH = Img.ActualHeight * ScaleTransform.ScaleY;
        double containerW = Container.ActualWidth;
        double containerH = Container.ActualHeight;

        double maxX = Math.Max(0, (imgW - containerW) / 2);
        double maxY = Math.Max(0, (imgH - containerH) / 2);

        TranslateTransform.X = Math.Clamp(TranslateTransform.X, -maxX, maxX);
        TranslateTransform.Y = Math.Clamp(TranslateTransform.Y, -maxY, maxY);
    }
}
