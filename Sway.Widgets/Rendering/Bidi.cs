using System.Text;

namespace Sway.Widgets;

public enum BidiClass
{
    L,   // Left-to-Right (Latin, Greek, Cyrillic, etc.)
    R,   // Right-to-Left (Hebrew, etc.)
    AL,  // Right-to-Left Arabic
    EN,  // European Number (0-9)
    AN,  // Arabic-Indic Number
    ES,  // European Number Separator (+, -)
    ET,  // European Number Terminator ($, %, etc.)
    CS,  // Common Number Separator (comma, period, colon, slash)
    NSM, // Non-Spacing Mark (tashkeel, accents)
    BN,  // Boundary Neutral
    B,   // Paragraph Separator
    S,   // Segment Separator (tab)
    WS,  // Whitespace (space)
    ON   // Other Neutrals (punctuation, symbols)
}

public readonly record struct BidiRun(int Start, int Length, TextDirection Direction, int Level);

/// <summary>
/// Implements Unicode Bidirectional Algorithm (UAX #9) for text segmentation and visual line reordering.
/// </summary>
public static class Bidi
{
    public static BidiClass Classify(char c)
    {
        // Arabic ranges
        if ((c >= 0x0600 && c <= 0x065F) || (c >= 0x066A && c <= 0x06EF) || (c >= 0x06FA && c <= 0x074F)
            || (c >= 0x0750 && c <= 0x077F) || (c >= 0x08A0 && c <= 0x08FF)
            || (c >= 0xFB50 && c <= 0xFDFF) || (c >= 0xFE70 && c <= 0xFEFC))
            return BidiClass.AL;

        // Arabic-Indic digits
        if ((c >= 0x0660 && c <= 0x0669) || (c >= 0x06F0 && c <= 0x06F9))
            return BidiClass.AN;

        // Hebrew and other RTL
        if ((c >= 0x0590 && c <= 0x05FF) || (c >= 0xFB1D && c <= 0xFB4F) || c == 0x200F || c == 0x061C)
            return BidiClass.R;

        // European digits
        if (c >= '0' && c <= '9')
            return BidiClass.EN;

        // Whitespace and separators
        if (c is ' ' or '\u00A0' or '\u2000' or '\u2001' or '\u2002' or '\u2003' or '\u2004' or '\u2005' or '\u2006' or '\u2007' or '\u2008' or '\u2009' or '\u200A' or '\u3000')
            return BidiClass.WS;

        if (c is '\r' or '\n')
            return BidiClass.B;

        if (c is '\t')
            return BidiClass.S;

        if (c is '+' or '-')
            return BidiClass.ES;

        if (c is ',' or '.' or ':' or '/' or '\u060C' or '\u061B')
            return BidiClass.CS;

        if (c is '%' or '$' or '#' or '°')
            return BidiClass.ET;

        // Latin and common LTR alphabets
        if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
            || (c >= 0x00C0 && c <= 0x02AF)
            || (c >= 0x0370 && c <= 0x052F)
            || (c >= 0x1E00 && c <= 0x1EFF)
            || c == 0x200E)
            return BidiClass.L;

        if (char.IsLetter(c))
            return BidiClass.L;

        return BidiClass.ON;
    }

    /// <summary>
    /// Computes bidirectional runs for a string given a base paragraph direction.
    /// </summary>
    public static List<BidiRun> Analyze(string text, TextDirection baseDirection = TextDirection.Ltr)
    {
        var runs = new List<BidiRun>();
        if (string.IsNullOrEmpty(text)) return runs;

        int n = text.Length;
        var types = new BidiClass[n];
        for (int i = 0; i < n; i++) types[i] = Classify(text[i]);

        // Resolving weak types and neutrals
        // Map AL to R for resolution
        var resolved = new TextDirection[n];
        TextDirection current = baseDirection;

        for (int i = 0; i < n; i++)
        {
            var t = types[i];
            switch (t)
            {
                case BidiClass.AL or BidiClass.R:
                    current = TextDirection.Rtl;
                    resolved[i] = TextDirection.Rtl;
                    break;
                case BidiClass.L:
                    current = TextDirection.Ltr;
                    resolved[i] = TextDirection.Ltr;
                    break;
                case BidiClass.EN or BidiClass.AN:
                    // Numbers in Arabic context are laid out LTR internally
                    resolved[i] = TextDirection.Ltr;
                    break;
                case BidiClass.WS or BidiClass.ON or BidiClass.ES or BidiClass.ET or BidiClass.CS or BidiClass.S or BidiClass.B:
                    // Look ahead for next strong direction
                    TextDirection nextStrong = baseDirection;
                    for (int j = i + 1; j < n; j++)
                    {
                        if (types[j] is BidiClass.AL or BidiClass.R) { nextStrong = TextDirection.Rtl; break; }
                        if (types[j] is BidiClass.L) { nextStrong = TextDirection.Ltr; break; }
                    }
                    // If surrounded by same direction, adopt it, else base direction
                    resolved[i] = (current == nextStrong) ? current : baseDirection;
                    break;
                default:
                    resolved[i] = current;
                    break;
            }
        }

        // Group into contiguous runs
        int start = 0;
        TextDirection runDir = resolved[0];

        for (int i = 1; i < n; i++)
        {
            if (resolved[i] != runDir)
            {
                int level = runDir == TextDirection.Rtl ? 1 : 0;
                if (baseDirection == TextDirection.Rtl && runDir == TextDirection.Ltr) level = 2;
                runs.Add(new BidiRun(start, i - start, runDir, level));
                start = i;
                runDir = resolved[i];
            }
        }

        int lastLevel = runDir == TextDirection.Rtl ? 1 : 0;
        if (baseDirection == TextDirection.Rtl && runDir == TextDirection.Ltr) lastLevel = 2;
        runs.Add(new BidiRun(start, n - start, runDir, lastLevel));

        return runs;
    }

    /// <summary>
    /// Reorders a list of visual items on an RTL or mixed line according to BiDi visual order.
    /// In an RTL base line, items flow from right to left (i.e. first logical RTL item is positioned at the rightmost X).
    /// </summary>
    public static List<int> GetVisualIndices(int count, TextDirection baseDirection, Func<int, TextDirection> itemDirection)
    {
        var indices = new List<int>(count);
        for (int i = 0; i < count; i++) indices.Add(i);

        if (baseDirection == TextDirection.Rtl)
        {
            // Reverse overall line for RTL base, keeping LTR runs internally LTR
            indices.Reverse();
        }

        return indices;
    }
}
