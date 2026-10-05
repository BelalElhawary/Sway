namespace Sway.Widgets;

static class TapRecognizerExt
{
    public static void Apply(this TapGestureRecognizer r, GestureDetector w)
    {
        r.OnDoubleTap = w.OnDoubleTap; r.OnTap = w.OnTap; r.OnTapDown = w.OnTapDown; r.OnTapUp = w.OnTapUp; r.OnTapCancel = w.OnTapCancel;
    }
}
