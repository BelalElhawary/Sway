using SkiaSharp;

namespace Sway.Widgets;

sealed class _Editable(TextEditState state, TextStyle style, TextStyle hintStyle, string? hint, bool obscure, int? maxLines,
    int? minLines, TextDirection direction, SKColor cursor, SKColor selection, bool focused, bool caretVisible,
    Action<RenderEditable> attach) : LeafRenderObjectWidget
{
    public override RenderObject CreateRenderObject(BuildContext context)
    {
        var r = new RenderEditable(state);
        attach(r);
        Apply(r);
        return r;
    }

    public override void UpdateRenderObject(BuildContext context, RenderObject ro)
    {
        var r = (RenderEditable)ro;
        attach(r);
        Apply(r);
    }

    void Apply(RenderEditable r) =>
        r.Update(state, style, hintStyle, hint, obscure, maxLines, minLines, direction, cursor, selection, focused, caretVisible);
}
