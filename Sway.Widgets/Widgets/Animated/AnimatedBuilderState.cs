using SkiaSharp;

namespace Sway.Widgets;

sealed class AnimatedBuilderState : State<AnimatedBuilder>
{
    public override void InitState() => Widget.Animation.AddListener(OnChanged);

    public override void DidUpdateWidget(AnimatedBuilder old)
    {
        if (ReferenceEquals(old.Animation, Widget.Animation)) return;
        old.Animation.RemoveListener(OnChanged);
        Widget.Animation.AddListener(OnChanged);
    }

    public override void Dispose() => Widget.Animation.RemoveListener(OnChanged);

    void OnChanged() { if (Mounted) SetState(); }

    public override Widget Build(BuildContext context) => Widget.Builder(context, Widget.Child);
}
