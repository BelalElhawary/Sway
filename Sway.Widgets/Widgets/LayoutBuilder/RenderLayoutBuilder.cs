namespace Sway.Widgets;

/// <summary>Asks its element to build the child during layout, passing the incoming constraints.</summary>
public sealed class RenderLayoutBuilder : RenderProxyBox
{
    public Action<BoxConstraints>? BuildChild;

    protected override void PerformLayout()
    {
        BuildChild?.Invoke(Constraints);
        base.PerformLayout();
    }

    // The child does not exist until layout, so it cannot be measured ahead of it.
    public override float MinIntrinsicWidth(float h) => 0;
    public override float MaxIntrinsicWidth(float h) => 0;
    public override float MinIntrinsicHeight(float w) => 0;
    public override float MaxIntrinsicHeight(float w) => 0;
}
