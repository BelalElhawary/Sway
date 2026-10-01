using System.Globalization;
using SkiaSharp;

namespace Sway.Core.Styling;

/// <summary>Parsing for shadows, gradients, transforms and filters.</summary>
static class EffectValues
{
    // ---- shadows ----

    public static bool TryParseShadows(string value, float fontSize, in StyleContext ctx, SKColor currentColor,
        bool isText, out List<Shadow> shadows)
    {
        shadows = new List<Shadow>();
        if (value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase)) return true;

        foreach (var layer in SplitTopLevel(value, ','))
        {
            float[] lengths = new float[4];
            int count = 0;
            bool inset = false;
            SKColor color = currentColor;

            foreach (var token in CssValues.SplitTokens(layer))
            {
                if (token.Equals("inset", StringComparison.OrdinalIgnoreCase)) { inset = true; continue; }
                if (CssValues.TryColor(token, currentColor, out var c)) { color = c; continue; }
                if (!CssValues.TryLength(token, fontSize, ctx, out var len) || len.Unit != LengthUnit.Px || count >= 4) return false;
                lengths[count++] = len.Value;
            }

            if (count < 2 || (isText && (inset || count > 3))) return false;
            shadows.Add(new Shadow(lengths[0], lengths[1], Math.Max(0, lengths[2]), isText ? 0 : lengths[3], color, inset));
        }
        return shadows.Count > 0;
    }

    // ---- gradients ----

    /// <summary>Parses a comma-separated list of gradient functions; non-gradient layers (url(), none) are skipped.</summary>
    public static List<Gradient> ParseGradients(string value, float fontSize, in StyleContext ctx, SKColor currentColor)
    {
        var result = new List<Gradient>();
        foreach (var layer in SplitTopLevel(value, ','))
        {
            foreach (var token in CssValues.SplitTokens(layer))
                if (TryParseGradient(token, fontSize, ctx, currentColor, out var gradient)) result.Add(gradient!);
        }
        return result;
    }

    public static bool TryParseGradient(string token, float fontSize, in StyleContext ctx, SKColor currentColor, out Gradient? gradient)
    {
        gradient = null;
        int open = token.IndexOf('(');
        if (open < 0 || !token.EndsWith(')')) return false;

        string name = token[..open].Trim().ToLowerInvariant();
        bool repeating = name.StartsWith("repeating-");
        string kind = repeating ? name["repeating-".Length..] : name;
        if (kind is not ("linear-gradient" or "radial-gradient")) return false;

        var parts = SplitTopLevel(token[(open + 1)..^1], ',');
        if (parts.Count < 2) return false;

        int firstStop = 0;
        float angle = 180;
        int cornerX = 0, cornerY = 0;
        bool circle = false;
        var size = RadialSize.FarthestCorner;
        Length rx = Length.Auto, ry = Length.Auto;
        Length cx = new(50, LengthUnit.Percent), cy = new(50, LengthUnit.Percent);

        string head = parts[0].Trim().ToLowerInvariant();
        if (kind == "linear-gradient")
        {
            if (head.StartsWith("to "))
            {
                var sides = head[3..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var side in sides)
                {
                    switch (side)
                    {
                        case "top": cornerY = -1; break;
                        case "bottom": cornerY = 1; break;
                        case "left": cornerX = -1; break;
                        case "right": cornerX = 1; break;
                        default: return false;
                    }
                }
                if (cornerX == 0) angle = cornerY < 0 ? 0 : 180;
                else if (cornerY == 0) angle = cornerX > 0 ? 90 : 270;
                firstStop = 1;
            }
            else if (TryAngle(head, out var a)) { angle = a; firstStop = 1; }
        }
        else if (IsRadialConfig(head))
        {
            if (!ParseRadialConfig(head, fontSize, ctx, out circle, out size, out rx, out ry, out cx, out cy)) return false;
            firstStop = 1;
        }

        var stops = new List<ColorStop>();
        for (int i = firstStop; i < parts.Count; i++)
        {
            var tokens = CssValues.SplitTokens(parts[i]);
            if (tokens.Count is < 1 or > 3 || !CssValues.TryColor(tokens[0], currentColor, out var color)) return false;

            if (tokens.Count == 1) { stops.Add(new ColorStop(color, Length.Auto)); continue; }
            for (int t = 1; t < tokens.Count; t++)
            {
                if (!CssValues.TryLength(tokens[t], fontSize, ctx, out var pos) || pos.IsAuto) return false;
                stops.Add(new ColorStop(color, pos)); // two positions make a hard-edged band
            }
        }
        if (stops.Count < 2) return false;

        gradient = new Gradient
        {
            IsRadial = kind == "radial-gradient", Repeating = repeating, AngleDegrees = angle, CornerX = cornerX, CornerY = cornerY,
            IsCircle = circle, Size = size, RadiusX = rx, RadiusY = ry, CenterX = cx, CenterY = cy, Stops = stops
        };
        return true;
    }

    static bool IsRadialConfig(string head) =>
        head.Contains("circle") || head.Contains("ellipse") || head.Contains("closest-") || head.Contains("farthest-")
        || head.StartsWith("at ") || head.Contains(" at ");

    static bool ParseRadialConfig(string head, float fontSize, in StyleContext ctx, out bool circle, out RadialSize size,
        out Length rx, out Length ry, out Length cx, out Length cy)
    {
        circle = false;
        size = RadialSize.FarthestCorner;
        rx = ry = Length.Auto;
        cx = cy = new Length(50, LengthUnit.Percent);

        int at = head.IndexOf("at ", StringComparison.Ordinal);
        string shapePart = at >= 0 ? head[..at] : head;
        string? position = at >= 0 ? head[(at + 3)..] : null;

        var lengths = new List<Length>();
        foreach (var token in CssValues.SplitTokens(shapePart))
        {
            switch (token)
            {
                case "circle": circle = true; break;
                case "ellipse": break;
                case "closest-side": size = RadialSize.ClosestSide; break;
                case "closest-corner": size = RadialSize.ClosestCorner; break;
                case "farthest-side": size = RadialSize.FarthestSide; break;
                case "farthest-corner": size = RadialSize.FarthestCorner; break;
                default:
                    if (!CssValues.TryLength(token, fontSize, ctx, out var len)) return false;
                    lengths.Add(len);
                    break;
            }
        }

        if (lengths.Count > 0)
        {
            size = RadialSize.Explicit;
            rx = lengths[0];
            ry = lengths.Count > 1 ? lengths[1] : lengths[0];
            if (lengths.Count == 1 && !circle) return false; // an ellipse needs two radii
        }

        if (position is not null && !TryParsePosition(position, fontSize, ctx, out cx, out cy)) return false;
        return true;
    }

    /// <summary>Parses "left top", "50% 20px", "center" and similar into a pair of lengths.</summary>
    public static bool TryParsePosition(string text, float fontSize, in StyleContext ctx, out Length x, out Length y)
    {
        x = y = new Length(50, LengthUnit.Percent);
        var tokens = CssValues.SplitTokens(text);
        if (tokens.Count is 0 or > 2) return false;

        Length? first = null, second = null;
        bool firstIsVertical = false;
        for (int i = 0; i < tokens.Count; i++)
        {
            Length value;
            bool vertical = false;
            switch (tokens[i])
            {
                case "left": value = new Length(0, LengthUnit.Percent); break;
                case "right": value = new Length(100, LengthUnit.Percent); break;
                case "top": value = new Length(0, LengthUnit.Percent); vertical = true; break;
                case "bottom": value = new Length(100, LengthUnit.Percent); vertical = true; break;
                case "center": value = new Length(50, LengthUnit.Percent); break;
                default:
                    if (!CssValues.TryLength(tokens[i], fontSize, ctx, out value) || value.IsAuto) return false;
                    break;
            }
            if (i == 0) { first = value; firstIsVertical = vertical; } else second = value;
        }

        if (tokens.Count == 1) { if (firstIsVertical) y = first!.Value; else x = first!.Value; return true; }
        if (firstIsVertical) { y = first!.Value; x = second!.Value; } else { x = first!.Value; y = second!.Value; }
        return true;
    }

    // ---- transforms ----

    public static bool TryParseTransform(string value, float fontSize, in StyleContext ctx, out TransformList? transform)
    {
        transform = null;
        if (value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase)) return true;

        var ops = new List<TransformOp>();
        foreach (var token in CssValues.SplitTokens(value))
        {
            int open = token.IndexOf('(');
            if (open < 0 || !token.EndsWith(')')) return false;

            string name = token[..open].ToLowerInvariant();
            var args = SplitTopLevel(token[(open + 1)..^1].Replace(" ", ",").Replace(",,", ","), ',')
                .Select(a => a.Trim()).Where(a => a.Length > 0).ToList();

            if (!TryParseTransformFunction(name, args, fontSize, ctx, ops)) return false;
        }
        if (ops.Count == 0) return false;
        transform = new TransformList(ops);
        return true;
    }

    static bool TryParseTransformFunction(string name, List<string> args, float fontSize, StyleContext ctx, List<TransformOp> ops)
    {
        bool Len(string text, out Length l) => CssValues.TryLength(text, fontSize, ctx, out l) && !l.IsAuto;
        bool Num(string text, out float n) => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out n);
        TransformOp Translate(Length x, Length y) => new(TransformKind.Translate,
            x.Unit == LengthUnit.Px ? x.Value : 0, y.Unit == LengthUnit.Px ? y.Value : 0,
            x.Unit == LengthUnit.Percent ? x.Value : 0, y.Unit == LengthUnit.Percent ? y.Value : 0);

        switch (name)
        {
            case "translate" or "translate3d" when args.Count is 1 or 2 or 3:
            {
                if (!Len(args[0], out var x)) return false;
                var y = Length.Zero;
                if (args.Count > 1 && !Len(args[1], out y)) return false;
                ops.Add(Translate(x, y));
                return true;
            }
            case "translatex" when args.Count == 1:
                if (!Len(args[0], out var tx)) return false;
                ops.Add(Translate(tx, Length.Zero));
                return true;
            case "translatey" when args.Count == 1:
                if (!Len(args[0], out var ty)) return false;
                ops.Add(Translate(Length.Zero, ty));
                return true;
            case "scale" or "scale3d" when args.Count is 1 or 2 or 3:
            {
                if (!Num(args[0], out var sx)) return false;
                float sy = sx;
                if (args.Count > 1 && !Num(args[1], out sy)) return false;
                ops.Add(new TransformOp(TransformKind.Scale, sx, sy));
                return true;
            }
            case "scalex" when args.Count == 1:
                if (!Num(args[0], out var scx)) return false;
                ops.Add(new TransformOp(TransformKind.Scale, scx, 1));
                return true;
            case "scaley" when args.Count == 1:
                if (!Num(args[0], out var scy)) return false;
                ops.Add(new TransformOp(TransformKind.Scale, 1, scy));
                return true;
            case "rotate" or "rotatez" when args.Count == 1:
                if (!TryAngle(args[0], out var deg)) return false;
                ops.Add(new TransformOp(TransformKind.Rotate, deg));
                return true;
            case "skew" when args.Count is 1 or 2:
            {
                if (!TryAngle(args[0], out var ax)) return false;
                float ay = 0;
                if (args.Count > 1 && !TryAngle(args[1], out ay)) return false;
                ops.Add(new TransformOp(TransformKind.Skew, ax, ay));
                return true;
            }
            case "skewx" when args.Count == 1:
                if (!TryAngle(args[0], out var skx)) return false;
                ops.Add(new TransformOp(TransformKind.Skew, skx, 0));
                return true;
            case "skewy" when args.Count == 1:
                if (!TryAngle(args[0], out var sky)) return false;
                ops.Add(new TransformOp(TransformKind.Skew, 0, sky));
                return true;
            case "matrix" when args.Count == 6:
            {
                var m = new float[6];
                for (int i = 0; i < 6; i++) if (!Num(args[i], out m[i])) return false;
                ops.Add(new TransformOp(TransformKind.Matrix, m[0], m[1], m[2], m[3], m[4], m[5]));
                return true;
            }
        }
        return false; // perspective, rotateX/Y and other 3D functions are not supported
    }

    public static bool TryAngle(string text, out float degrees)
    {
        degrees = 0;
        text = text.Trim().ToLowerInvariant();
        if (!CssValues.TrySplitUnit(text, out var number, out var unit)) return false;
        switch (unit)
        {
            case "deg": degrees = number; return true;
            case "grad": degrees = number * 0.9f; return true;
            case "rad": degrees = number * 180f / MathF.PI; return true;
            case "turn": degrees = number * 360f; return true;
            case "" when number == 0: degrees = 0; return true;
            default: return false;
        }
    }

    // ---- filters ----

    public static bool TryParseFilters(string value, float fontSize, in StyleContext ctx, SKColor currentColor, out List<FilterOp> filters)
    {
        filters = new List<FilterOp>();
        if (value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase)) return true;

        foreach (var token in CssValues.SplitTokens(value))
        {
            int open = token.IndexOf('(');
            if (open < 0 || !token.EndsWith(')')) return false;
            string name = token[..open].ToLowerInvariant();
            string arg = token[(open + 1)..^1].Trim();

            switch (name)
            {
                case "blur":
                    if (!CssValues.TryLength(arg.Length == 0 ? "0" : arg, fontSize, ctx, out var radius) || radius.Unit != LengthUnit.Px) return false;
                    filters.Add(new FilterOp(FilterKind.Blur, radius.Value));
                    break;
                case "hue-rotate":
                    if (!TryAngle(arg.Length == 0 ? "0deg" : arg, out var degrees)) return false;
                    filters.Add(new FilterOp(FilterKind.HueRotate, degrees));
                    break;
                case "drop-shadow":
                {
                    if (!TryParseShadows(arg, fontSize, ctx, currentColor, isText: true, out var shadow)) return false;
                    var s = shadow[0];
                    filters.Add(new FilterOp(FilterKind.DropShadow, s.Blur, s.X, s.Y, s.Color));
                    break;
                }
                default:
                {
                    var kind = name switch
                    {
                        "brightness" => FilterKind.Brightness, "contrast" => FilterKind.Contrast, "grayscale" => FilterKind.Grayscale,
                        "sepia" => FilterKind.Sepia, "saturate" => FilterKind.Saturate, "invert" => FilterKind.Invert,
                        "opacity" => FilterKind.Opacity, _ => (FilterKind?)null
                    };
                    if (kind is null || !TryAmount(arg, out var amount)) return false;
                    filters.Add(new FilterOp(kind.Value, amount));
                    break;
                }
            }
        }
        return filters.Count > 0;
    }

    // A filter amount is a number or a percentage; an empty argument means 1 (100%).
    static bool TryAmount(string arg, out float amount)
    {
        amount = 1;
        if (arg.Length == 0) return true;
        bool percent = arg.EndsWith('%');
        if (!float.TryParse(percent ? arg[..^1] : arg, NumberStyles.Float, CultureInfo.InvariantCulture, out amount)) return false;
        if (percent) amount /= 100f;
        return amount >= 0;
    }

    // ---- helpers ----

    public static List<string> SplitTopLevel(string value, char separator)
    {
        var parts = new List<string>();
        int start = 0;
        foreach (int index in CssParser.TopLevelSplits(value, separator))
        {
            parts.Add(value[start..index]);
            start = index + 1;
        }
        parts.Add(value[start..]);
        return parts;
    }
}
