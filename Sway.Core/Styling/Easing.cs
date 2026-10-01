using System.Globalization;

namespace Sway.Core.Styling;

/// <summary>A CSS timing function: maps linear progress 0..1 to eased progress.</summary>
public sealed class TimingFunction
{
    enum Kind { CubicBezier, Steps }
    enum Jump { Start, End, None, Both }

    readonly Kind _kind;
    readonly float _x1, _y1, _x2, _y2;
    readonly int _steps;
    readonly Jump _jump;

    TimingFunction(float x1, float y1, float x2, float y2)
    {
        _kind = Kind.CubicBezier;
        (_x1, _y1, _x2, _y2) = (x1, y1, x2, y2);
    }

    TimingFunction(int steps, Jump jump)
    {
        _kind = Kind.Steps;
        _steps = steps;
        _jump = jump;
    }

    public static readonly TimingFunction Ease = new(0.25f, 0.1f, 0.25f, 1f);
    public static readonly TimingFunction Linear = new(0f, 0f, 1f, 1f);
    public static readonly TimingFunction EaseIn = new(0.42f, 0f, 1f, 1f);
    public static readonly TimingFunction EaseOut = new(0f, 0f, 0.58f, 1f);
    public static readonly TimingFunction EaseInOut = new(0.42f, 0f, 0.58f, 1f);

    public float Evaluate(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return _kind == Kind.Steps ? EvaluateSteps(t) : EvaluateBezier(t);
    }

    float EvaluateSteps(float t)
    {
        // jump-start and jump-end differ in whether the first or last step happens at the boundary.
        int intervals = _jump == Jump.None ? _steps - 1 : _jump == Jump.Both ? _steps + 1 : _steps;
        if (intervals <= 0) return t >= 1 ? 1 : 0;

        float step = MathF.Floor(t * _steps);
        if (_jump is Jump.Start or Jump.Both) step += 1;
        return Math.Clamp(step / intervals, 0f, 1f);
    }

    float EvaluateBezier(float x)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;

        // Solve x(t) = x for t with Newton's method, falling back to bisection when the slope is flat.
        float t = x;
        for (int i = 0; i < 8; i++)
        {
            float error = Sample(_x1, _x2, t) - x;
            if (MathF.Abs(error) < 1e-6f) return Sample(_y1, _y2, t);
            float slope = Slope(_x1, _x2, t);
            if (MathF.Abs(slope) < 1e-6f) break;
            t -= error / slope;
        }

        float lo = 0, hi = 1;
        t = x;
        for (int i = 0; i < 24; i++)
        {
            float value = Sample(_x1, _x2, t);
            if (MathF.Abs(value - x) < 1e-6f) break;
            if (value < x) lo = t; else hi = t;
            t = (lo + hi) / 2;
        }
        return Sample(_y1, _y2, t);
    }

    // One coordinate of a cubic Bezier from (0,0) to (1,1) with control values a and b.
    static float Sample(float a, float b, float t) => ((1 - 3 * b + 3 * a) * t + (3 * b - 6 * a)) * t * t + 3 * a * t;
    static float Slope(float a, float b, float t) => 3 * (1 - 3 * b + 3 * a) * t * t + 2 * (3 * b - 6 * a) * t + 3 * a;

    public static bool TryParse(string text, out TimingFunction function)
    {
        function = Ease;
        text = text.Trim().ToLowerInvariant();

        switch (text)
        {
            case "ease": function = Ease; return true;
            case "linear": function = Linear; return true;
            case "ease-in": function = EaseIn; return true;
            case "ease-out": function = EaseOut; return true;
            case "ease-in-out": function = EaseInOut; return true;
            case "step-start": function = new TimingFunction(1, Jump.Start); return true;
            case "step-end": function = new TimingFunction(1, Jump.End); return true;
        }

        int open = text.IndexOf('(');
        if (open < 0 || !text.EndsWith(')')) return false;
        string name = text[..open];
        var args = text[(open + 1)..^1].Split(',', StringSplitOptions.TrimEntries);

        if (name == "cubic-bezier" && args.Length == 4)
        {
            var v = new float[4];
            for (int i = 0; i < 4; i++)
                if (!float.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return false;
            if (v[0] < 0 || v[0] > 1 || v[2] < 0 || v[2] > 1) return false; // x control points must stay in 0..1
            function = new TimingFunction(v[0], v[1], v[2], v[3]);
            return true;
        }

        if (name == "steps" && args.Length is 1 or 2 && int.TryParse(args[0], out int count) && count > 0)
        {
            var jump = Jump.End;
            if (args.Length == 2)
            {
                switch (args[1])
                {
                    case "jump-start" or "start": jump = Jump.Start; break;
                    case "jump-end" or "end": jump = Jump.End; break;
                    case "jump-none": jump = Jump.None; break;
                    case "jump-both": jump = Jump.Both; break;
                    default: return false;
                }
            }
            if (jump == Jump.None && count < 2) return false;
            function = new TimingFunction(count, jump);
            return true;
        }
        return false;
    }
}
