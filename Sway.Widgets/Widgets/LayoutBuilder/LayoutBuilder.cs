namespace Sway.Widgets;

/// <summary>Builds its child from the constraints its parent gives it, at layout time.</summary>
public sealed class LayoutBuilder(Func<BuildContext, BoxConstraints, Widget> builder, Key? key = null) : RenderObjectWidget(key)
{
    internal Func<BuildContext, BoxConstraints, Widget> Builder => builder;

    public override Element CreateElement() => new LayoutBuilderElement(this);
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderLayoutBuilder();
}
