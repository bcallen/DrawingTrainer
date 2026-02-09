using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DrawingTrainer.Views;

public partial class ZoomableImage : UserControl
{
    private Point _mouseDownPos;
    private Point _lastMousePos;
    private bool _isDragging;

    private const double MinScale = 1.0;
    private const double MaxScale = 10.0;
    private const double ZoomFactor = 1.15;
    private const double ClickZoomScale = 3.0;
    private const double ClickThreshold = 5.0;

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
        MouseRightButtonUp += OnMouseRightButtonUp;
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

        var imgPos = Container.TranslatePoint(mousePos, Img);
        double relX = imgPos.X / Img.ActualWidth;
        double relY = imgPos.Y / Img.ActualHeight;

        ScaleTransform.ScaleX = newScaleX;
        ScaleTransform.ScaleY = newScaleY;

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
        _mouseDownPos = e.GetPosition(this);
        _lastMousePos = _mouseDownPos;
        _isDragging = false;
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        bool wasDragging = _isDragging;
        _isDragging = false;
        ReleaseMouseCapture();
        Cursor = Cursors.Arrow;

        if (!wasDragging)
        {
            // Single click: zoom in centered on click point
            var clickPos = e.GetPosition(Container);
            ClickZoom(clickPos);
        }
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!IsMouseCaptured) return;

        var currentPos = e.GetPosition(this);

        if (!_isDragging)
        {
            double distance = (currentPos - _mouseDownPos).Length;
            if (distance > ClickThreshold && ScaleTransform.ScaleX > MinScale)
            {
                _isDragging = true;
                Cursor = Cursors.Hand;
            }
            else
            {
                return;
            }
        }

        double dx = currentPos.X - _lastMousePos.X;
        double dy = currentPos.Y - _lastMousePos.Y;
        _lastMousePos = currentPos;

        TranslateTransform.X += dx;
        TranslateTransform.Y += dy;
        ClampPan();
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        ResetZoom();
        e.Handled = true;
    }

    private void ClickZoom(Point containerPoint)
    {
        ScaleTransform.ScaleX = ClickZoomScale;
        ScaleTransform.ScaleY = ClickZoomScale;

        double containerCenterX = Container.ActualWidth / 2;
        double containerCenterY = Container.ActualHeight / 2;
        TranslateTransform.X = (containerCenterX - containerPoint.X) * (ClickZoomScale - 1) / ClickZoomScale;
        TranslateTransform.Y = (containerCenterY - containerPoint.Y) * (ClickZoomScale - 1) / ClickZoomScale;

        ClampPan();
    }

    public void ResetZoom()
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
