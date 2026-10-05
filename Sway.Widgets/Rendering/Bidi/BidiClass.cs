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
