using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace PixelPress.Desktop.Views;

/// <summary>
/// The before/after comparison surface: two bitmaps, one viewport, a wipe
/// divider, and zoom/pan.
///
/// It is a single custom-rendered control rather than a pair of <see cref="Image"/>
/// elements in a clipping <see cref="Border"/>, because the layout system is the
/// wrong tool for this job and the old arrangement proved it three ways:
///
///   * Two images that each fit *themselves* land on two different rectangles the
///     moment their pixel sizes differ — which is exactly what a resize, or a
///     source with non-96 DPI metadata, produces. The optimized frame drew larger
///     than the original and the comparison was a lie.
///   * Driving the wipe by setting a Border's Width ran a full measure/arrange
///     pass per pointer move, re-fitting the image mid-drag. That is the smearing.
///   * A 30px handle in a Canvas has a 30px grab target and nothing else.
///
/// Here both bitmaps are drawn into one shared destination rect (<see cref="ImageRect"/>),
/// so they are aligned by construction; the wipe is a render-time clip, so dragging
/// it costs one repaint and touches no layout; and the handle carries a grab region
/// wider than the handle itself.
///
/// Geometry lives here rather than in the view model on purpose: it is measured in
/// device pixels against this control's bounds, means nothing to the planner or the
/// encoder, and must not survive a re-plan.
/// </summary>
public sealed class CompareView : Control
{
    private const double MinZoom = 0.05;
    private const double MaxZoom = 16.0;

    /// <summary>One wheel notch. 1.2 is a brisk-but-controllable ramp: ~13 notches
    /// covers the whole 0.05–16 range.</summary>
    private const double WheelStep = 1.2;

    private const double ButtonStep = 1.25;

    private const double HandleRadius = 17;

    /// <summary>How far either side of the divider still counts as grabbing it.
    /// Wider than the handle, so the drag starts on approach rather than demanding
    /// a hit on a 34px circle.</summary>
    private const double HandleGrabHalfWidth = 24;

    private const double DividerLineWidth = 2;

    public static readonly StyledProperty<IImage?> BeforeImageProperty =
        AvaloniaProperty.Register<CompareView, IImage?>(nameof(BeforeImage));

    public static readonly StyledProperty<IImage?> AfterImageProperty =
        AvaloniaProperty.Register<CompareView, IImage?>(nameof(AfterImage));

    /// <summary>Where the wipe sits, as a fraction of the viewport's width. A ratio
    /// rather than a pixel offset so it holds its place across a window resize, a
    /// zoom, or a differently-shaped image.</summary>
    public static readonly StyledProperty<double> SplitRatioProperty =
        AvaloniaProperty.Register<CompareView, double>(nameof(SplitRatio), 0.5,
            coerce: (_, v) => Math.Clamp(v, 0, 1));

    public static readonly StyledProperty<IBrush?> DividerBrushProperty =
        AvaloniaProperty.Register<CompareView, IBrush?>(nameof(DividerBrush));

    public static readonly StyledProperty<IBrush?> HandleBackgroundProperty =
        AvaloniaProperty.Register<CompareView, IBrush?>(nameof(HandleBackground));

    public static readonly StyledProperty<IBrush?> HandleBorderBrushProperty =
        AvaloniaProperty.Register<CompareView, IBrush?>(nameof(HandleBorderBrush));

    public static readonly StyledProperty<IBrush?> HandleForegroundProperty =
        AvaloniaProperty.Register<CompareView, IBrush?>(nameof(HandleForeground));

    public static readonly DirectProperty<CompareView, string> ZoomTextProperty =
        AvaloniaProperty.RegisterDirect<CompareView, string>(nameof(ZoomText), o => o.ZoomText);

    public static readonly DirectProperty<CompareView, bool> IsFitToWindowProperty =
        AvaloniaProperty.RegisterDirect<CompareView, bool>(nameof(IsFitToWindow), o => o.IsFitToWindow);

    private static readonly Cursor GrabCursor = new(StandardCursorType.Hand);
    private static readonly Cursor SplitCursor = new(StandardCursorType.SizeWestEast);
    private static readonly Cursor ArrowCursor = new(StandardCursorType.Arrow);

    private enum Drag
    {
        None,
        Divider,
        Pan,
    }

    /// <summary>Scale from image pixels to *device* pixels: 1.0 means one image
    /// pixel per physical screen pixel, whatever the display's DPI scaling. The
    /// previous viewer measured in DIPs and so rendered "100%" at 2× on a HiDPI
    /// screen.</summary>
    private double _zoom = 1;

    /// <summary>True while the zoom is slaved to the viewport, so a window resize
    /// re-fits instead of leaving the image stranded at a stale scale.</summary>
    private bool _isFitToWindow = true;

    /// <summary>Displacement of the image's centre from the viewport's centre, in
    /// DIPs. Zero is centred, which is where a fitted image always sits.</summary>
    private Point _pan;

    private Drag _drag;
    private Point _dragPointerOrigin;
    private Point _dragPanOrigin;
    private string _zoomText = "100%";

    static CompareView()
    {
        AffectsRender<CompareView>(
            BeforeImageProperty,
            AfterImageProperty,
            SplitRatioProperty,
            DividerBrushProperty,
            HandleBackgroundProperty,
            HandleBorderBrushProperty,
            HandleForegroundProperty);

        ClipToBoundsProperty.OverrideDefaultValue<CompareView>(true);
    }

    public CompareView()
    {
        // Both bitmaps are drawn scaled on essentially every frame, so the
        // interpolation mode is not a detail: the default would show a downscaled
        // photo as an aliased mess and invite the user to blame the encoder.
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);
    }

    public IImage? BeforeImage
    {
        get => GetValue(BeforeImageProperty);
        set => SetValue(BeforeImageProperty, value);
    }

    public IImage? AfterImage
    {
        get => GetValue(AfterImageProperty);
        set => SetValue(AfterImageProperty, value);
    }

    public double SplitRatio
    {
        get => GetValue(SplitRatioProperty);
        set => SetValue(SplitRatioProperty, value);
    }

    public IBrush? DividerBrush
    {
        get => GetValue(DividerBrushProperty);
        set => SetValue(DividerBrushProperty, value);
    }

    public IBrush? HandleBackground
    {
        get => GetValue(HandleBackgroundProperty);
        set => SetValue(HandleBackgroundProperty, value);
    }

    public IBrush? HandleBorderBrush
    {
        get => GetValue(HandleBorderBrushProperty);
        set => SetValue(HandleBorderBrushProperty, value);
    }

    public IBrush? HandleForeground
    {
        get => GetValue(HandleForegroundProperty);
        set => SetValue(HandleForegroundProperty, value);
    }

    /// <summary>The zoom, as the toolbar shows it.</summary>
    public string ZoomText
    {
        get => _zoomText;
        private set => SetAndRaise(ZoomTextProperty, ref _zoomText, value);
    }

    /// <summary>Lets the toolbar light up the mode the view is actually in, rather
    /// than offering "Fit" as if it were not already fitted.</summary>
    public bool IsFitToWindow
    {
        get => _isFitToWindow;
        private set => SetAndRaise(IsFitToWindowProperty, ref _isFitToWindow, value);
    }

    // --- Commands the toolbar and the View menu call -------------------------

    public void ZoomIn() => ZoomAbout(ViewportCentre, _zoom * ButtonStep);

    public void ZoomOut() => ZoomAbout(ViewportCentre, _zoom / ButtonStep);

    /// <summary>Scales the image to show all of it, and keeps doing so as the
    /// window changes size until the user zooms explicitly.</summary>
    public void FitToWindow()
    {
        IsFitToWindow = true;
        _pan = default;
        ApplyFit();
    }

    /// <summary>One image pixel per screen pixel — the only zoom level at which
    /// judging compression artefacts means anything.</summary>
    public void ActualSize() => ZoomAbout(ViewportCentre, 1.0);

    public void ResetView()
    {
        SplitRatio = 0.5;
        FitToWindow();
    }

    // --- Geometry -------------------------------------------------------------

    private Point ViewportCentre => new(Bounds.Width / 2, Bounds.Height / 2);

    /// <summary>The image whose pixels define the frame. Both bitmaps are drawn
    /// into that one frame, so a resized output is compared against the original
    /// at the same size on screen — which is the whole point of a wipe, and what
    /// separate Uniform fits could never guarantee.</summary>
    private IImage? SourceImage => BeforeImage ?? AfterImage;

    /// <summary>Display scaling, so "actual size" means device pixels rather than
    /// DIPs.</summary>
    private double RenderScaling => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;

    /// <summary>The image's natural size in DIPs — what it would occupy at 100%.
    /// Taken from <see cref="Bitmap.PixelSize"/>, not <c>Size</c>: the latter is
    /// divided by the file's DPI tag, so a 300-DPI scan would claim to be a third
    /// of its real size.</summary>
    private Size NaturalSize
    {
        get
        {
            if (SourceImage is not { } image)
            {
                return default;
            }

            var pixels = image is Bitmap bitmap
                ? new Size(bitmap.PixelSize.Width, bitmap.PixelSize.Height)
                : image.Size;

            var scaling = RenderScaling;
            return scaling > 0 ? new Size(pixels.Width / scaling, pixels.Height / scaling) : pixels;
        }
    }

    private double FitZoom
    {
        get
        {
            var natural = NaturalSize;
            if (natural.Width <= 0 || natural.Height <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
            {
                return 1;
            }

            var fit = Math.Min(Bounds.Width / natural.Width, Bounds.Height / natural.Height);
            return Math.Clamp(fit, MinZoom, MaxZoom);
        }
    }

    /// <summary>The one destination rect both bitmaps are drawn into.</summary>
    private Rect ImageRect()
    {
        var natural = NaturalSize;
        if (natural.Width <= 0 || natural.Height <= 0)
        {
            return default;
        }

        var size = natural * _zoom;
        var origin = new Point(
            ((Bounds.Width - size.Width) / 2) + _pan.X,
            ((Bounds.Height - size.Height) / 2) + _pan.Y);

        return new Rect(origin, size);
    }

    private void ApplyFit()
    {
        SetZoom(FitZoom);
        InvalidateVisual();
    }

    private void SetZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        ZoomText = $"{Math.Round(_zoom * 100)}%";
    }

    /// <summary>Zooms while holding one point of the image still under <paramref name="anchor"/>.
    /// Wheel-zoom that ignores the cursor is the single most common way to make a
    /// viewer feel broken: the thing you were looking at slides off screen.</summary>
    private void ZoomAbout(Point anchor, double zoom)
    {
        var before = ImageRect();
        var target = Math.Clamp(zoom, MinZoom, MaxZoom);

        // Snapping out of fit mode has to happen even when the scale does not
        // change, or "Actual size" on an image that happens to fit exactly would
        // leave the view still slaved to the window.
        IsFitToWindow = false;

        if (before.Width <= 0 || before.Height <= 0)
        {
            SetZoom(target);
            InvalidateVisual();
            return;
        }

        // Where the anchor falls within the image, 0..1.
        var u = (anchor.X - before.X) / before.Width;
        var v = (anchor.Y - before.Y) / before.Height;

        SetZoom(target);

        var size = NaturalSize * _zoom;

        // Put that same image point back under the anchor, then express the result
        // as a displacement from centred.
        var origin = new Point(anchor.X - (u * size.Width), anchor.Y - (v * size.Height));
        _pan = new Point(
            origin.X - ((Bounds.Width - size.Width) / 2),
            origin.Y - ((Bounds.Height - size.Height) / 2));

        ClampPan();
        InvalidateVisual();
    }

    /// <summary>Keeps the image tethered to the viewport: an axis smaller than the
    /// viewport stays centred, a larger one may be panned but not dragged clear of
    /// the frame. Without this the user can lose the picture entirely and has no
    /// way back except Reset.</summary>
    private void ClampPan()
    {
        var size = NaturalSize * _zoom;

        var slackX = Math.Max(0, (size.Width - Bounds.Width) / 2);
        var slackY = Math.Max(0, (size.Height - Bounds.Height) / 2);

        _pan = new Point(
            Math.Clamp(_pan.X, -slackX, slackX),
            Math.Clamp(_pan.Y, -slackY, slackY));
    }

    private bool CanPan()
    {
        var size = NaturalSize * _zoom;
        return size.Width > Bounds.Width + 0.5 || size.Height > Bounds.Height + 0.5;
    }

    private double DividerX => Bounds.Width * SplitRatio;

    private bool IsOverDivider(Point p) =>
        HasComparison && Math.Abs(p.X - DividerX) <= HandleGrabHalfWidth;

    /// <summary>The wipe is only meaningful with two images to wipe between. With
    /// one (an AVIF output no decoder here can read, or a preview still encoding)
    /// this is a plain viewer, and it must not offer a divider that does nothing.</summary>
    private bool HasComparison => BeforeImage is not null && AfterImage is not null;

    // --- Input ---------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Double-click toggles the two zoom levels anyone actually wants, at the
        // point they clicked: the whole picture, or its real pixels.
        if (e.ClickCount == 2)
        {
            if (IsFitToWindow)
            {
                ZoomAbout(point.Position, 1.0);
            }
            else
            {
                FitToWindow();
            }

            e.Handled = true;
            return;
        }

        if (IsOverDivider(point.Position))
        {
            _drag = Drag.Divider;

            // Grabbing near the divider takes it where it is rather than snapping
            // it to the press point, so a grab is not also a jump.
            _dragPointerOrigin = point.Position;
            _dragPanOrigin = new Point(DividerX, 0);
        }
        else if (CanPan())
        {
            _drag = Drag.Pan;
            _dragPointerOrigin = point.Position;
            _dragPanOrigin = _pan;
            Cursor = GrabCursor;
        }
        else
        {
            return;
        }

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var position = e.GetPosition(this);

        switch (_drag)
        {
            case Drag.Divider when Bounds.Width > 0:
                var x = _dragPanOrigin.X + (position.X - _dragPointerOrigin.X);
                SplitRatio = x / Bounds.Width;
                e.Handled = true;
                break;

            case Drag.Pan:
                _pan = _dragPanOrigin + (position - _dragPointerOrigin);
                ClampPan();
                InvalidateVisual();
                e.Handled = true;
                break;

            case Drag.None:
                Cursor = IsOverDivider(position) ? SplitCursor
                    : CanPan() ? GrabCursor
                    : ArrowCursor;
                break;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_drag == Drag.None)
        {
            return;
        }

        _drag = Drag.None;
        e.Pointer.Capture(null);
        Cursor = IsOverDivider(e.GetPosition(this)) ? SplitCursor : ArrowCursor;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (SourceImage is null || e.Delta.Y == 0)
        {
            return;
        }

        ZoomAbout(e.GetPosition(this), _zoom * Math.Pow(WheelStep, e.Delta.Y));
        e.Handled = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BeforeImageProperty || change.Property == AfterImageProperty)
        {
            // A new picture starts framed. Carrying a 400% zoom and a pan offset
            // over from the last one would open on a corner of it.
            FitToWindow();
        }
        else if (change.Property == BoundsProperty)
        {
            if (IsFitToWindow)
            {
                ApplyFit();
            }
            else
            {
                ClampPan();
                InvalidateVisual();
            }
        }
    }

    // --- Render ---------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var dest = ImageRect();
        if (dest.Width <= 0 || dest.Height <= 0)
        {
            return;
        }

        var before = BeforeImage;
        var after = AfterImage;

        if (before is not null)
        {
            context.DrawImage(before, new Rect(before.Size), dest);
        }

        if (after is null)
        {
            return;
        }

        if (before is null)
        {
            // Nothing to compare against — the output is simply the picture.
            context.DrawImage(after, new Rect(after.Size), dest);
            return;
        }

        // The wipe. A render-time clip, so dragging it repaints and nothing else:
        // no measure, no arrange, no re-fitting the image under the cursor.
        // ORIGINAL is left of the divider, OPTIMIZED right of it — which is the
        // order the pane labels promise.
        var x = DividerX;
        var band = new Rect(x, 0, Math.Max(0, Bounds.Width - x), Bounds.Height);

        using (context.PushClip(band))
        {
            context.DrawImage(after, new Rect(after.Size), dest);
        }

        DrawDivider(context, x);
    }

    private void DrawDivider(DrawingContext context, double x)
    {
        var height = Bounds.Height;

        if (DividerBrush is { } line)
        {
            context.FillRectangle(line, new Rect(x - (DividerLineWidth / 2), 0, DividerLineWidth, height));
        }

        var centre = new Point(x, height / 2);
        var pen = HandleBorderBrush is { } border ? new Pen(border) : null;

        context.DrawEllipse(HandleBackground, pen, centre, HandleRadius, HandleRadius);

        if (HandleForeground is not { } arrows)
        {
            return;
        }

        // Two triangles rather than a "⇄" glyph: an arrow drawn by the platform's
        // font would be a different arrow on every platform, and on some of them
        // a colour emoji that ignores Foreground.
        context.DrawGeometry(arrows, null, ArrowGeometry(centre, pointsLeft: true));
        context.DrawGeometry(arrows, null, ArrowGeometry(centre, pointsLeft: false));
    }

    private static StreamGeometry ArrowGeometry(Point centre, bool pointsLeft)
    {
        const double tip = 9;
        const double baseX = 3.5;
        const double halfHeight = 4.5;

        var direction = pointsLeft ? -1 : 1;
        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(centre.X + (tip * direction), centre.Y), isFilled: true);
            ctx.LineTo(new Point(centre.X + (baseX * direction), centre.Y - halfHeight));
            ctx.LineTo(new Point(centre.X + (baseX * direction), centre.Y + halfHeight));
            ctx.EndFigure(isClosed: true);
        }

        return geometry;
    }
}
