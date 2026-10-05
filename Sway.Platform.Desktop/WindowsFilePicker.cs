using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Sway.Widgets;

/// <summary>The Windows common item dialog (IFileOpenDialog), including real folder selection.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsFilePicker : DesktopFilePicker
{
    public override Task<IReadOnlyList<PickedFile>> PickFilesAsync(FilePickerOptions options) =>
        OnStaThread<IReadOnlyList<PickedFile>>(() => ToFiles(WindowsDialog.Show(options.Title, options.Filters, options.AllowMultiple, false, options.InitialDirectory)), []);

    protected override Task<string?> PickFolderPathAsync(string? title, string? initialDirectory) =>
        OnStaThread<string?>(() => WindowsDialog.Show(title, null, false, true, initialDirectory).FirstOrDefault(), null);

    // COM dialogs need a single-threaded apartment, and the host's window thread must keep pumping messages.
    static Task<T> OnStaThread<T>(Func<T> work, T cancelled)
    {
        var source = new TaskCompletionSource<T>();
        var thread = new Thread(() =>
        {
            try { source.SetResult(work()); }
            catch (COMException) { source.SetResult(cancelled); }
            catch (Exception e) { source.SetException(e); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return source.Task;
    }
}

/// <summary>The Windows IFileOpenDialog, declared by hand so no extra package is needed.</summary>
[SupportedOSPlatform("windows")]
static class WindowsDialog
{
    /// <summary>The owner window; the desktop host sets it once its window exists.</summary>
    public static IntPtr Owner;

    const uint FosPickFolders = 0x20, FosForceFileSystem = 0x40, FosAllowMultiSelect = 0x200, FosPathMustExist = 0x800, FosFileMustExist = 0x1000;
    const uint SigdnFileSysPath = 0x80058000;
    const int ErrorCancelled = unchecked((int)0x800704C7);

    /// <summary>Shows the dialog and returns the chosen paths; empty when the user cancelled.</summary>
    public static IReadOnlyList<string> Show(string? title, IReadOnlyList<FileTypeFilter>? filters, bool multiple, bool folders, string? initialDirectory)
    {
        var dialog = (IFileOpenDialog)new FileOpenDialogCoClass();
        try
        {
            dialog.GetOptions(out uint options);
            options |= FosForceFileSystem | FosPathMustExist;
            options |= folders ? FosPickFolders : FosFileMustExist;
            if (multiple) options |= FosAllowMultiSelect;
            dialog.SetOptions(options);

            if (title is not null) dialog.SetTitle(title);
            if (!folders && filters is { Count: > 0 })
            {
                var specs = filters
                    .Select(f => new FilterSpec { Name = f.Name, Spec = string.Join(';', f.Extensions.Select(e => "*." + e)) })
                    .Append(new FilterSpec { Name = "All files", Spec = "*.*" })
                    .ToArray();
                dialog.SetFileTypes((uint)specs.Length, specs);
                dialog.SetFileTypeIndex(1);
            }
            if (initialDirectory is not null && Directory.Exists(initialDirectory))
            {
                var iid = typeof(IShellItem).GUID;
                SHCreateItemFromParsingName(initialDirectory, IntPtr.Zero, ref iid, out var folder);
                dialog.SetFolder(folder);
            }

            int hr = dialog.Show(Owner);
            if (hr == ErrorCancelled) return [];
            Marshal.ThrowExceptionForHR(hr);

            dialog.GetResults(out var results);
            results.GetCount(out uint count);
            var paths = new List<string>();
            for (uint i = 0; i < count; i++)
            {
                results.GetItemAt(i, out var item);
                item.GetDisplayName(SigdnFileSysPath, out var buffer);
                paths.Add(Marshal.PtrToStringUni(buffer) ?? "");
                Marshal.FreeCoTaskMem(buffer);
            }
            return paths;
        }
        finally
        {
            Marshal.FinalReleaseComObject(dialog);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItem item);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct FilterSpec
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string Name;
        [MarshalAs(UnmanagedType.LPWStr)] public string Spec;
    }

    [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    class FileOpenDialogCoClass { }

    // Method order is the vtable order; do not reorder or drop entries.
    [ComImport, Guid("D57C7288-D4AD-4768-BE02-9D969532D960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr owner);
        void SetFileTypes(uint count, [MarshalAs(UnmanagedType.LPArray)] FilterSpec[] specs);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(IntPtr events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint options);
        void GetOptions(out uint options);
        void SetDefaultFolder(IShellItem item);
        void SetFolder(IShellItem item);
        void GetFolder(out IShellItem item);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem item);
        void AddPlace(IShellItem item, int placement);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr filter);
        void GetResults(out IShellItemArray items);
        void GetSelectedItems(out IShellItemArray items);
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem
    {
        void BindToHandler(IntPtr bindContext, ref Guid handler, ref Guid iid, out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint form, out IntPtr name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [ComImport, Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemArray
    {
        void BindToHandler(IntPtr bindContext, ref Guid handler, ref Guid iid, out IntPtr result);
        void GetPropertyStore(int flags, ref Guid iid, out IntPtr store);
        void GetPropertyDescriptionList(IntPtr keyType, ref Guid iid, out IntPtr list);
        void GetAttributes(int flags, uint mask, out uint attributes);
        void GetCount(out uint count);
        void GetItemAt(uint index, out IShellItem item);
        void EnumItems(out IntPtr items);
    }
}
