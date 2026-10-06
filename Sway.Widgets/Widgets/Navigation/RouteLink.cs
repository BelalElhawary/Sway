namespace Sway.Widgets;

/// <summary>Makes its child a link to <paramref name="location"/>: a pointer cursor, and a tap pushes the page (or <see cref="RouterController.Go"/>s to it).</summary>
public sealed class RouteLink(string location, Widget child, bool go = false, Key? key = null) : StatelessWidget(key)
{
    public override Widget Build(BuildContext context) => new MouseRegion(
        new GestureDetector(child, onTap: () =>
        {
            var router = context.Get<RouterScope>()?.Controller ?? throw new InvalidOperationException("No Router above this context.");
            if (go) router.Go(location); else router.Push(location);
        }),
        cursor: MouseCursor.Click);
}
