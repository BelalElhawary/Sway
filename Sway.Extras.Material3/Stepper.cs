using SkiaSharp;

using Sway.Widgets;

namespace Sway.Extras.Material3;

/// <summary>
/// A sequence of steps with the current one open. The owner holds <paramref name="currentStep"/> and moves it from
/// <paramref name="onStepTapped"/>, <paramref name="onStepContinue"/> and <paramref name="onStepCancel"/>.
/// </summary>
public sealed class Stepper(IReadOnlyList<Step> steps, int currentStep = 0, Action<int>? onStepTapped = null, Action? onStepContinue = null,
    Action? onStepCancel = null, StepperType type = StepperType.Vertical, Key? key = null) : StatelessWidget(key)
{
    const float Marker = 24;

    public override Widget Build(BuildContext context) => type == StepperType.Vertical ? BuildVertical(context) : BuildHorizontal(context);

    Widget MarkerFor(BuildContext context, int index)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var step = steps[index];
        bool highlighted = step.IsActive || index == currentStep || step.State == StepState.Complete;
        var fill = step.State switch
        {
            StepState.Error => s.Error,
            StepState.Disabled => s.OnSurface.WithOpacity(0.38f),
            _ => highlighted ? s.Primary : s.OnSurface.WithOpacity(0.38f),
        };
        var fg = step.State == StepState.Error ? s.OnError : s.OnPrimary;
        Widget content = step.State switch
        {
            StepState.Complete => new Icon(Icons.Check, 16, fg),
            StepState.Error => new Icon(Icons.PriorityHigh, 16, fg),
            _ => new Text((index + 1).ToString(), style: theme.TextTheme.LabelSmall.Merge(new TextStyle(Color: fg))),
        };
        return new AnimatedContainer(TimeSpan.FromMilliseconds(150), width: Marker, height: Marker, alignment: Alignment.Center,
            decoration: new BoxDecoration(Color: fill, Shape: BoxShape.Circle), child: content);
    }

    Widget TitleFor(BuildContext context, int index)
    {
        var theme = Theme.Of(context);
        var s = theme.ColorScheme;
        var step = steps[index];
        var color = step.State switch
        {
            StepState.Error => s.Error,
            StepState.Disabled => s.OnSurface.WithOpacity(0.38f),
            _ => index == currentStep || step.IsActive ? s.OnSurface : s.OnSurfaceVariant,
        };
        return new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Start, children:
        [
            DefaultTextStyle.Merge(context, theme.TextTheme.TitleSmall.Merge(new TextStyle(Color: color)), step.Title),
            ..step.Subtitle is null ? Array.Empty<Widget>() : [DefaultTextStyle.Merge(context,
                theme.TextTheme.BodySmall.Merge(new TextStyle(Color: s.OnSurfaceVariant)), step.Subtitle)],
        ]);
    }

    Widget Controls(int index) => new Padding(EdgeInsets.Only(top: 16), new Row(mainAxisSize: MainAxisSize.Min, spacing: 8, children:
    [
        new FilledButton(new Text(index == steps.Count - 1 ? "Finish" : "Continue"), onStepContinue),
        new TextButton(new Text("Cancel"), onStepCancel),
    ]));

    Widget BuildVertical(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        var rows = new List<Widget>();
        for (int i = 0; i < steps.Count; i++)
        {
            int index = i;
            var step = steps[i];
            bool open = i == currentStep;
            bool last = i == steps.Count - 1;
            bool enabled = step.State != StepState.Disabled && onStepTapped is not null;
            rows.Add(new Interactive((ctx, st) => new Container(
                padding: EdgeInsets.Symmetric(24, 12),
                color: StateLayer.Blend(Colors.Transparent, s.OnSurface, enabled ? StateLayer.Opacity(st) : 0),
                child: new Row(spacing: 12, children: [MarkerFor(ctx, index), new Expanded(TitleFor(ctx, index))])),
                enabled ? () => onStepTapped!(index) : null));
            rows.Add(new Padding(EdgeInsets.Only(left: 24 + Marker / 2 - 0.5f), new Row(crossAxisAlignment: CrossAxisAlignment.Stretch, children:
            [
                new SizedBox(width: 1, child: new ColoredBox(last ? Colors.Transparent : s.OutlineVariant)),
                new Expanded(new ClipRect(new AnimatedSize(TimeSpan.FromMilliseconds(200),
                    open ? new Padding(EdgeInsets.Only(left: 23, right: 24, top: 4, bottom: last ? 16 : 24),
                            new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Start, children:
                            [step.Content, Controls(index)]))
                        : new SizedBox(height: last ? 0 : 16),
                    Alignment.TopLeft, Curves.EaseInOut))),
            ])));
        }
        return new Column(rows, mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch);
    }

    Widget BuildHorizontal(BuildContext context)
    {
        var s = Theme.Of(context).ColorScheme;
        var header = new List<Widget>();
        for (int i = 0; i < steps.Count; i++)
        {
            int index = i;
            bool enabled = steps[i].State != StepState.Disabled && onStepTapped is not null;
            if (i > 0) header.Add(new Expanded(new SizedBox(height: 1, child: new ColoredBox(s.OutlineVariant))));
            header.Add(new Interactive((ctx, st) => new Container(
                padding: EdgeInsets.Symmetric(16, 12),
                color: StateLayer.Blend(Colors.Transparent, s.OnSurface, enabled ? StateLayer.Opacity(st) : 0),
                child: new Row(mainAxisSize: MainAxisSize.Min, spacing: 8, children: [MarkerFor(ctx, index), TitleFor(ctx, index)])),
                enabled ? () => onStepTapped!(index) : null));
        }
        int current = Math.Clamp(currentStep, 0, steps.Count - 1);
        return new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Stretch, children:
        [
            new Row(header),
            new Padding(EdgeInsets.All(24), new Column(mainAxisSize: MainAxisSize.Min, crossAxisAlignment: CrossAxisAlignment.Start, children:
                [steps[current].Content, Controls(current)])),
        ]);
    }
}
