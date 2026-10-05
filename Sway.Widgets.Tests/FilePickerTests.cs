using Xunit;

namespace Sway.Widgets.Tests;

public class FilePickerTests : IDisposable
{
    readonly IFilePickerSource _original = FilePicker.Source;

    public void Dispose() => FilePicker.Source = _original;

    sealed class FakePicker(PickedFile[] files) : IFilePickerSource
    {
        public bool CanPickFolders => false;
        public Task<IReadOnlyList<PickedFile>> PickFilesAsync(FilePickerOptions options) => Task.FromResult<IReadOnlyList<PickedFile>>(files);
        public Task<PickedFolder?> PickFolderAsync(string? title = null, string? initialDirectory = null) => Task.FromResult<PickedFolder?>(null);
    }

    [Fact]
    public void Filter_matches_extensions_case_insensitively()
    {
        Assert.True(FileTypeFilter.Video.Matches("Clip.MP4"));
        Assert.True(FileTypeFilter.Media.Matches("song.flac"));
        Assert.False(FileTypeFilter.Audio.Matches("clip.mp4"));
        Assert.Equal(["mp4", "mkv"], new FileTypeFilter("v", "*.MP4", ".mkv").Extensions);
    }

    [Fact]
    public void Without_a_platform_package_nothing_is_picked()
    {
        FilePicker.Source = new FilePicker_Fallback().Instance;
        Assert.False(FilePicker.IsSupported);
        Assert.Empty(FilePicker.PickFilesAsync().Result);
        Assert.Null(FilePicker.PickFolderAsync().Result);
    }

    [Fact]
    public void PickFileAsync_returns_the_first_file()
    {
        FilePicker.Source = new FakePicker([new PickedFile("a.mp3", "a"), new PickedFile("b.mp3", "b")]);
        Assert.True(FilePicker.IsSupported);
        Assert.Equal("a.mp3", FilePicker.PickFileAsync().Result?.Name);
    }

    [Fact]
    public async Task Folder_lists_sorted_filtered_files_and_can_recurse()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllText(Path.Combine(root.FullName, "b.mp3"), "x");
            File.WriteAllText(Path.Combine(root.FullName, "a.mp4"), "x");
            File.WriteAllText(Path.Combine(root.FullName, "notes.txt"), "x");
            var sub = root.CreateSubdirectory("sub");
            File.WriteAllText(Path.Combine(sub.FullName, "c.mkv"), "x");

            var folder = PickedFolder.FromPath(root.FullName);
            Assert.Equal(["a.mp4", "b.mp3"], (await folder.GetFilesAsync(FileTypeFilter.Media)).Select(f => f.Name));
            Assert.Equal(3, (await folder.GetFilesAsync(FileTypeFilter.Media, recursive: true)).Count);

            var file = (await folder.GetFilesAsync(FileTypeFilter.Video)).First();
            using var stream = await file.OpenReadAsync();
            Assert.Equal(1, stream.Length);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void Post_runs_on_the_next_frame_even_from_another_thread()
    {
        var harness = new Harness(new SizedBox());
        int ran = 0;
        Task.Run(() => harness.Binding.Post(() => ran++)).Wait();
        Assert.Equal(0, ran);
        Assert.True(harness.Binding.NeedsFrame(harness.Width, harness.Height));
        harness.Pump();
        Assert.Equal(1, ran);
    }

    // The fallback is private to FilePicker, so read it from a fresh static state.
    sealed class FilePicker_Fallback
    {
        public IFilePickerSource Instance { get; } = (IFilePickerSource)Activator.CreateInstance(
            typeof(FilePicker).GetNestedType("Fallback", System.Reflection.BindingFlags.NonPublic)!)!;
    }
}
