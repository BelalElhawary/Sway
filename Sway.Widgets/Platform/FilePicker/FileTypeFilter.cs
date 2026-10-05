namespace Sway.Widgets;

/// <summary>A named set of file extensions offered in an open dialog, such as "Video" with mp4, mkv and webm.</summary>
public sealed class FileTypeFilter
{
    public FileTypeFilter(string name, params string[] extensions)
    {
        Name = name;
        Extensions = extensions.Select(e => e.TrimStart('*', '.').ToLowerInvariant()).ToArray();
    }

    public string Name { get; }

    /// <summary>Extensions without the dot or wildcard, lower case.</summary>
    public IReadOnlyList<string> Extensions { get; }

    public bool Matches(string fileName)
    {
        var ext = Path.GetExtension(fileName).TrimStart('.');
        return Extensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
    }

    public static FileTypeFilter Video { get; } = new("Video", "mp4", "m4v", "mkv", "webm", "avi", "mov", "wmv", "flv", "mpg", "mpeg", "ts", "ogv", "3gp");
    public static FileTypeFilter Audio { get; } = new("Audio", "mp3", "m4a", "aac", "flac", "wav", "ogg", "oga", "opus", "wma", "aiff", "mka");
    public static FileTypeFilter Images { get; } = new("Images", "png", "jpg", "jpeg", "gif", "webp", "bmp");

    /// <summary>Video and audio together, for pickers that feed a media player.</summary>
    public static FileTypeFilter Media { get; } = new("Media", [.. Video.Extensions, .. Audio.Extensions]);
}
