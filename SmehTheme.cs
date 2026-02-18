using Spectre.Console;

namespace SMEH;

/// <summary>
/// FICSIT-style color theme: dark background, orange accent, light gray text.
/// </summary>
public static class SmehTheme
{
    // Orange accent (labels, highlights, borders) — #E66700
    public static readonly Color Accent = new(230, 103, 0);
    public const string AccentHex = "#E66700";

    // Light gray secondary text — #D7D7D7
    public static readonly Color TextSecondary = new(215, 215, 215);
    public const string TextSecondaryHex = "#D7D7D7";

    // Dark gray (borders, subtle elements) — #404040
    public static readonly Color Border = new(64, 64, 64);
    public const string BorderHex = "#404040";

    public static Style AccentStyle => Style.Plain.Foreground(Accent);
}
