namespace Sway.Widgets;

public sealed record FilePickerOptions
{
    public string? Title { get; init; }
    public bool AllowMultiple { get; init; }

    /// <summary>The types to offer. Empty or null means every file. Hosts without a filter UI apply the first one.</summary>
    public IReadOnlyList<FileTypeFilter>? Filters { get; init; }

    /// <summary>A starting directory, honoured where the platform lets an app choose one.</summary>
    public string? InitialDirectory { get; init; }
}
