using Sway.Core.Dom;

namespace Sway.Core.Styling;

/// <summary>Per-element transition and animation state.</summary>
public sealed class AnimationState
{
    internal Dictionary<string, TransitionRun> Transitions { get; } = new();
    internal List<AnimationRun> Animations { get; } = new();

    /// <summary>Set when an animation ended, so the restyle that drops its overlay does not start a transition.</summary>
    internal bool SuppressTransitions { get; set; }

    internal bool IsEmpty => Transitions.Count == 0 && Animations.Count == 0;
}

internal sealed class TransitionRun
{
    public required AnimatedProperty Property { get; init; }
    public required object? From { get; init; }
    public required object? To { get; init; }
    public required double StartMs { get; init; }
    public required float DurationMs { get; init; }
    public required float DelayMs { get; init; }
    public required TimingFunction Timing { get; init; }

    public bool IsFinished(double now) => now - StartMs - DelayMs >= DurationMs;

    public object? ValueAt(double now)
    {
        double elapsed = now - StartMs - DelayMs;
        if (elapsed < 0) return From;
        if (DurationMs <= 0 || elapsed >= DurationMs) return To;

        float eased = Timing.Evaluate((float)(elapsed / DurationMs));
        return AnimatedProperties.Interpolate(Property.Kind, From, To, eased);
    }
}

internal sealed record PropertyTrack(AnimatedProperty Property, List<(float Offset, object? Value)> Stops);

internal sealed class AnimationRun
{
    public required AnimationSpec Spec { get; set; }
    public required double StartMs { get; set; }
    public required List<PropertyTrack> Tracks { get; init; }

    /// <summary>Elapsed time (including the delay) frozen at the moment the animation was paused.</summary>
    public double? PausedElapsed { get; set; }
    public bool Finished { get; set; }
}

/// <summary>
/// Drives CSS transitions and keyframe animations. A transition starts when a restyle changes the
/// computed value of a property that has a transition configured; animations start when a style names
/// an @keyframes rule. Running values are written over the computed style, and <see cref="Tick"/> advances them.
/// </summary>
public sealed class Animator
{
    readonly StyleResolver _resolver;
    readonly HashSet<ElementNode> _active = new();

    public Animator(StyleResolver resolver) => _resolver = resolver;

    /// <summary>True while any element still needs per-frame updates.</summary>
    public bool HasActive => _active.Count > 0;

    // ---- restyle hook ----

    /// <summary>
    /// Called with the freshly computed style of an element before it replaces the old one. Starts or
    /// retargets transitions, starts animations, and re-applies the values of anything still running.
    /// </summary>
    public void OnRestyle(ElementNode el, ComputedStyle? old, ComputedStyle fresh, double now)
    {
        var state = el.Animation;
        var transitionSpecs = fresh.TransitionSpecs();
        var animationSpecs = fresh.AnimationSpecs();

        bool wantsTransitions = transitionSpecs.Any(s => s.Property != "none" && s.DurationMs + s.DelayMs > 0);
        if (state is null && !wantsTransitions && animationSpecs.Count == 0) return; // the common case

        state ??= el.Animation = new AnimationState();

        UpdateTransitions(state, old, fresh, transitionSpecs, now);
        UpdateAnimations(el, state, fresh, animationSpecs, now);

        // Whatever is still running overrides the new computed values.
        foreach (var run in state.Transitions.Values) run.Property.Set(fresh, run.ValueAt(now));
        foreach (var run in state.Animations) ApplyAnimation(run, fresh, now);

        if (state.IsEmpty) { el.Animation = null; _active.Remove(el); }
        else if (IsRunning(state, now)) _active.Add(el);
    }

    static bool IsRunning(AnimationState state, double now) =>
        state.Transitions.Values.Any(r => !r.IsFinished(now)) || state.Animations.Any(r => !r.Finished && r.PausedElapsed is null);

    // ---- transitions ----

    static TransitionSpec? FindSpec(List<TransitionSpec> specs, string property)
    {
        // Later entries win, as in CSS; "all" matches everything and "none" matches nothing.
        for (int i = specs.Count - 1; i >= 0; i--)
        {
            var spec = specs[i];
            if (spec.Property == "none") return null;
            if (spec.Property == "all" || spec.Property == property || AnimatedProperties.Expand(spec.Property).Contains(property))
                return spec;
        }
        return null;
    }

    static void UpdateTransitions(AnimationState state, ComputedStyle? old, ComputedStyle fresh,
        List<TransitionSpec> specs, double now)
    {
        bool visible = old is not null && old.Display != Display.None && fresh.Display != Display.None;
        if (!visible)
        {
            state.Transitions.Clear();
            return;
        }

        bool suppress = state.SuppressTransitions;
        state.SuppressTransitions = false;

        foreach (var property in AnimatedProperties.All)
        {
            var spec = FindSpec(specs, property.Name);
            bool configured = spec is not null && spec.DurationMs + spec.DelayMs > 0;
            object? target = property.Get(fresh);

            if (state.Transitions.TryGetValue(property.Name, out var running))
            {
                if (!configured) { state.Transitions.Remove(property.Name); continue; }
                if (AnimatedProperties.Equal(property.Kind, running.To, target)) continue; // keep going
                state.Transitions.Remove(property.Name);
            }

            if (!configured || suppress) continue;

            // The old style holds what is currently on screen, including any in-flight value.
            object? from = property.Get(old!);
            if (AnimatedProperties.Equal(property.Kind, from, target)) continue;

            state.Transitions[property.Name] = new TransitionRun
            {
                Property = property, From = from, To = target, StartMs = now,
                DurationMs = spec!.DurationMs, DelayMs = spec.DelayMs, Timing = spec.Timing
            };
        }
    }

    // ---- animations ----

    void UpdateAnimations(ElementNode el, AnimationState state, ComputedStyle fresh, List<AnimationSpec> specs, double now)
    {
        // Animations whose name left the list are cancelled; the fresh style already lacks their values.
        state.Animations.RemoveAll(run => specs.All(s => s.Name != run.Spec.Name));

        foreach (var spec in specs)
        {
            var existing = state.Animations.FirstOrDefault(r => r.Spec.Name == spec.Name);
            if (existing is not null)
            {
                existing.Spec = spec;
                SyncPlayState(existing, now);
                continue;
            }

            var rule = _resolver.FindKeyframes(spec.Name);
            if (rule is null) continue;

            var run = new AnimationRun { Spec = spec, StartMs = now, Tracks = BuildTracks(el, fresh, rule) };
            SyncPlayState(run, now);
            state.Animations.Add(run);
        }
    }

    static void SyncPlayState(AnimationRun run, double now)
    {
        if (run.Spec.Paused && run.PausedElapsed is null) run.PausedElapsed = now - run.StartMs;
        else if (!run.Spec.Paused && run.PausedElapsed is { } frozen)
        {
            run.StartMs = now - frozen; // resume where it stopped
            run.PausedElapsed = null;
        }
    }

    /// <summary>
    /// Builds one track per animated property. Properties missing from the first or last keyframe fall
    /// back to the element's own computed value, as the spec describes for implicit from/to keyframes.
    /// </summary>
    List<PropertyTrack> BuildTracks(ElementNode el, ComputedStyle baseStyle, KeyframesRule rule)
    {
        var parent = el.ParentElement?.Style;
        var stops = new Dictionary<string, List<(float, object?)>>();

        foreach (var frame in rule.Frames.OrderBy(f => f.Offset))
        {
            var scratch = baseStyle.Clone();
            var names = new HashSet<string>();

            // font-size and color first, since em units and currentColor depend on them.
            var ordered = frame.Declarations.OrderBy(d => d.Name is "font-size" or "color" ? 0 : 1);
            foreach (var declaration in ordered)
            {
                _resolver.ApplyDeclaration(scratch, parent, declaration);
                foreach (var name in AnimatedProperties.Expand(declaration.Name))
                    if (AnimatedProperties.Find(name) is not null) names.Add(name);
            }

            foreach (var name in names)
            {
                if (!stops.TryGetValue(name, out var list)) stops[name] = list = new List<(float, object?)>();
                list.Add((frame.Offset, AnimatedProperties.Find(name)!.Get(scratch)));
            }
        }

        var tracks = new List<PropertyTrack>();
        foreach (var (name, list) in stops)
        {
            var property = AnimatedProperties.Find(name)!;
            object? baseValue = property.Get(baseStyle);
            if (list[0].Item1 > 0) list.Insert(0, (0f, baseValue));
            if (list[^1].Item1 < 1) list.Add((1f, baseValue));
            tracks.Add(new PropertyTrack(property, list));
        }
        return tracks;
    }

    /// <summary>Where an animation is at <paramref name="now"/>: whether it affects the style, its progress 0..1, and whether it ended.</summary>
    static (bool apply, float progress, bool finished) Evaluate(AnimationRun run, double now)
    {
        var spec = run.Spec;
        double elapsed = (run.PausedElapsed ?? now - run.StartMs) - spec.DelayMs;

        if (elapsed < 0)
        {
            bool backwards = spec.Fill is AnimationFill.Backwards or AnimationFill.Both;
            return (backwards, DirectedProgress(spec, 0, 0), false);
        }

        double duration = spec.DurationMs;
        double total = duration * spec.Iterations;
        if (duration <= 0 || (!float.IsInfinity(spec.Iterations) && elapsed >= total))
        {
            bool forwards = spec.Fill is AnimationFill.Forwards or AnimationFill.Both;
            if (duration <= 0 || spec.Iterations <= 0) return (forwards, DirectedProgress(spec, 0, 1), true);

            // The last iteration may be partial (for example 2.5 iterations).
            double iterations = spec.Iterations;
            int lastIteration = (int)Math.Ceiling(iterations) - 1;
            double fraction = iterations - Math.Floor(iterations);
            double p = fraction == 0 ? 1 : fraction;
            return (forwards, DirectedProgress(spec, lastIteration, (float)p), true);
        }

        int iteration = (int)(elapsed / duration);
        float progress = (float)((elapsed - iteration * duration) / duration);
        return (true, DirectedProgress(spec, iteration, progress), false);
    }

    static float DirectedProgress(AnimationSpec spec, int iteration, float progress)
    {
        bool reverse = spec.Direction switch
        {
            AnimationDirection.Reverse => true,
            AnimationDirection.Alternate => iteration % 2 == 1,
            AnimationDirection.AlternateReverse => iteration % 2 == 0,
            _ => false
        };
        return reverse ? 1 - progress : progress;
    }

    static object? ValueOf(PropertyTrack track, float progress, TimingFunction timing)
    {
        var stops = track.Stops;
        if (progress <= stops[0].Offset) return stops[0].Value;

        for (int i = 0; i < stops.Count - 1; i++)
        {
            var (a, b) = (stops[i], stops[i + 1]);
            if (progress > b.Offset) continue;

            float span = b.Offset - a.Offset;
            float local = span <= 0 ? 1 : (progress - a.Offset) / span;
            return AnimatedProperties.Interpolate(track.Property.Kind, a.Value, b.Value, timing.Evaluate(local));
        }
        return stops[^1].Value;
    }

    /// <summary>Writes the animation's current values into a style; returns true if any affect layout.</summary>
    static bool ApplyAnimation(AnimationRun run, ComputedStyle style, double now)
    {
        var (apply, progress, finished) = Evaluate(run, now);
        run.Finished = finished;
        if (!apply) return false;

        bool layout = false;
        foreach (var track in run.Tracks)
        {
            track.Property.Set(style, ValueOf(track, progress, run.Spec.Timing));
            layout |= track.Property.AffectsLayout;
        }
        return layout;
    }

    // ---- per-frame update ----

    /// <summary>Advances every running transition and animation to <paramref name="now"/> and marks what changed.</summary>
    public void Tick(Document document, double now)
    {
        if (_active.Count == 0) return;

        foreach (var el in _active.ToList())
        {
            if (el.Animation is not { } state || !IsAttached(document, el))
            {
                _active.Remove(el);
                continue;
            }

            var style = el.Style;
            bool layout = false, restyle = false;

            foreach (var run in state.Transitions.Values.ToList())
            {
                run.Property.Set(style, run.ValueAt(now));
                layout |= run.Property.AffectsLayout;
                if (run.IsFinished(now)) state.Transitions.Remove(run.Property.Name);
            }

            foreach (var run in state.Animations.ToList())
            {
                layout |= ApplyAnimation(run, style, now);
                if (!run.Finished) continue;

                // Without fill-forwards the element returns to its own computed style.
                if (run.Spec.Fill is AnimationFill.None or AnimationFill.Backwards)
                {
                    state.Animations.Remove(run);
                    restyle = true;
                }
            }

            if (restyle)
            {
                state.SuppressTransitions = true;
                document.InvalidateStyleOf(el);
            }
            if (layout) el.MarkLayoutDirty();
            document.InvalidatePaint();

            if (state.IsEmpty) el.Animation = null;
            if (el.Animation is null || !IsRunning(state, now)) _active.Remove(el);
        }
    }

    static bool IsAttached(Document document, ElementNode el)
    {
        Node node = el;
        while (node.Parent is { } parent) node = parent;
        return node == document.Root;
    }
}
