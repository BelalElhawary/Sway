namespace Sway.Widgets;

sealed class OffstageBox(Widget? child, bool offstage) : SingleChildRenderObjectWidget(child)
{
    public override RenderObject CreateRenderObject(BuildContext context) => new RenderOffstage(offstage);
    public override void UpdateRenderObject(BuildContext context, RenderObject ro) => ((RenderOffstage)ro).Offstage = offstage;
}
