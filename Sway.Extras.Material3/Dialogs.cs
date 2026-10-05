using Sway.Widgets;

namespace Sway.Extras.Material3;

public static class Dialogs
{
    /// <summary>
    /// Shows <paramref name="builder"/> above the app with a dimming scrim. The builder receives a function that closes the dialog.
    /// Returns that same function so callers can also close it. Focus is trapped inside while it is open, Escape closes it (when
    /// <paramref name="barrierDismissible"/>), and focus returns to where it was when it closes.
    /// </summary>
    public static Action Show(BuildContext context, Func<BuildContext, Action, Widget> builder, bool barrierDismissible = true)
    {
        var overlay = Overlay.Of(context);
        var previousFocus = WidgetsBinding.Instance.Focus.Primary;
        OverlayEntry? entry = null;
        void Close()
        {
            if (entry is null) return;
            entry.Remove();
            entry = null;
            if (previousFocus is { Element.Mounted: true }) previousFocus.RequestFocus();
        }
        entry = new OverlayEntry(ctx =>
        {
            var s = Theme.Of(context).ColorScheme;
            return Wrap(context, new Focus(autofocus: true, trapFocus: true, skipTraversal: true,
                onKey: e => e.IsDown && e.Key == "Escape" && barrierDismissible && CloseAndConsume(Close),
                child: new TweenAnimationBuilder<float>(new FloatTween(0, 1), TimeSpan.FromMilliseconds(180),
                    (_, t, __) => new Stack([
                        Positioned.Fill(new GestureDetector(onTap: barrierDismissible ? Close : null, behavior: HitTestBehavior.Opaque,
                            child: new ColoredBox(s.Scrim.WithOpacity(0.32f * t)))),
                        new Center(new Opacity(t, Transform.Scale(0.92f + 0.08f * t, builder(ctx, Close)))),
                    ], fit: StackFit.Expand, clip: false), curve: Curves.EaseOutCubic)));
        });
        overlay.Insert(entry);
        return Close;
    }

    static bool CloseAndConsume(Action close)
    {
        close();
        return true;
    }

    // Overlay entries sit outside the app's theme/text scope, so re-establish them from the opening context.
    internal static Widget Wrap(BuildContext origin, Widget child)
    {
        var theme = Theme.Of(origin);
        return new Theme(theme, new IconTheme(theme.ColorScheme.OnSurfaceVariant, 24,
            new DefaultTextStyle(theme.TextTheme.BodyMedium, new Directionality(Directionality.Of(origin), child))));
    }

    // Snack bars show one at a time; the rest wait their turn.
    static readonly Queue<Func<Action>> PendingSnackBars = new();
    static bool _snackBarShowing;

    /// <summary>Shows a snack bar, or queues it if one is already visible. Returns a function that dismisses (or cancels) this one.</summary>
    public static Action ShowSnackBar(BuildContext context, string message, string? actionLabel = null, Action? onAction = null, TimeSpan? duration = null)
    {
        bool cancelled = false;
        Action? closeVisible = null;

        void Present()
        {
            _snackBarShowing = true;
            closeVisible = PresentSnackBar(context, message, actionLabel, onAction, duration, () =>
            {
                _snackBarShowing = false;
                // A cancelled entry presents nothing, so keep going until one is actually shown.
                while (!_snackBarShowing && PendingSnackBars.TryDequeue(out var next)) next();
            });
        }

        if (_snackBarShowing)
            PendingSnackBars.Enqueue(() =>
            {
                if (!cancelled) Present();
                return () => { };
            });
        else Present();

        return () =>
        {
            cancelled = true;
            closeVisible?.Invoke();
        };
    }

    static Action PresentSnackBar(BuildContext context, string message, string? actionLabel, Action? onAction, TimeSpan? duration, Action onClosed)
    {
        var overlay = Overlay.Of(context);
        OverlayEntry? entry = null;
        Action timer = null!;
        void Close()
        {
            if (entry is null) return;
            WidgetsBinding.Instance.CancelTimer(timer);
            entry.Remove();
            entry = null;
            onClosed();
        }
        timer = Close;
        entry = new OverlayEntry(ctx =>
        {
            var theme = Theme.Of(context);
            var s = theme.ColorScheme;
            return Wrap(context, new Positioned(new Align(Alignment.BottomCenter, new Padding(EdgeInsets.All(16),
                new TweenAnimationBuilder<float>(new FloatTween(0, 1), TimeSpan.FromMilliseconds(200), (_, t, __) => new Opacity(t,
                    new FractionalTranslation(new Offset(0, 0.4f * (1 - t)), new ConstrainedBox(new BoxConstraints(0, 560, 0, float.PositiveInfinity),
                        new Material(new Padding(EdgeInsets.Symmetric(16, 0), new ConstrainedBox(BoxConstraints.TightFor(height: 48), new Row(children:
                        [
                            new Expanded(new Text(message, style: theme.TextTheme.BodyMedium.Merge(new TextStyle(Color: s.OnInverseSurface)))),
                            ..actionLabel is null ? Array.Empty<Widget>() : [new TextButton(new Text(actionLabel), () => { onAction?.Invoke(); Close(); }, color: s.InversePrimary)],
                        ]))), s.InverseSurface, 3, BorderRadius.Circular(Shapes.ExtraSmall))))), curve: Curves.EaseOutCubic))),
                left: 0, top: 0, right: 0, bottom: 0));
        });
        overlay.Insert(entry);
        WidgetsBinding.Instance.ScheduleTimer(duration ?? TimeSpan.FromSeconds(4), timer);
        return Close;
    }
}
