using Android.Content;
using Android.Content.Res;
using Android.OS;
using Android.Views;
using Android.Widget;
using Android.Window;
using Android.App;

namespace Sway.Widgets;

/// <summary>
/// Android host. Subclass it, override <see cref="CreateRoot"/> and mark the subclass with <c>[Activity(MainLauncher = true)]</c>:
/// <code>[Activity(Label = "My app", MainLauncher = true)] class MainActivity : SwayActivity { protected override Widget CreateRoot() =&gt; new MyApp(); }</code>
/// </summary>
public abstract class SwayActivity : Activity
{
    WidgetsBinding? _binding;
    SwayView? _view;
    MemoryLocationSource? _history;

    /// <summary>The widget tree to run.</summary>
    protected abstract Widget CreateRoot();

    /// <summary>The binding driving this activity, available after <c>OnCreate</c>.</summary>
    protected WidgetsBinding Binding => _binding ?? throw new InvalidOperationException("The activity has not been created yet.");

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        SystemTheme.Source = new AndroidThemeSource(this);
        FilePicker.Source = new AndroidFilePicker(this);
        // A link that launched the app (https://host/products/42) is where a Router starts; the history lives in memory.
        AppLocation.Source = _history = new MemoryLocationSource(LocationOf(Intent) ?? "/");
        _binding = new WidgetsBinding { PlatformBrightness = SystemTheme.Brightness() };
        _binding.GetClipboard = ReadClipboard;
        _binding.SetClipboard = WriteClipboard;
        _binding.AttachRoot(CreateRoot());

        _view = new SwayView(this, _binding);

        // Edge-to-edge is the default on recent Android; inset the surface so content clears the system bars and the keyboard.
        var frame = new FrameLayout(this);
        frame.AddView(_view, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        frame.SetOnApplyWindowInsetsListener(new InsetsListener());
        SetContentView(frame);

        // From Android 13 the system only reports back through a registered callback when the app opts into predictive back.
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            OnBackInvokedDispatcher.RegisterOnBackInvokedCallback(0, new BackCallback(this));
    }

    /// <summary>The path and query of the link an intent was opened with, or null when it carries none.</summary>
    static string? LocationOf(Intent? intent)
    {
        if (intent?.Data is not { Path: { Length: > 0 } path } uri) return null;
        return uri.EncodedQuery is { Length: > 0 } query ? path + "?" + query : path;
    }

    /// <summary>A link arrived while the app is running (<c>launchMode="singleTop"</c>): open it as a new page.</summary>
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (LocationOf(intent) is { } location) _history?.Open(location);
    }

    /// <summary>The back button or gesture: the <see cref="Router"/> goes back first, and the activity closes once there is nothing left.</summary>
    [Obsolete("Replaced by OnBackInvokedCallback on Android 13+; still the path on older versions.")]
    public override void OnBackPressed()
    {
        if (!(_binding?.HandleBack() ?? false)) base.OnBackPressed();
    }

    sealed class BackCallback(SwayActivity activity) : Java.Lang.Object, IOnBackInvokedCallback
    {
        public void OnBackInvoked()
        {
            if (!(activity._binding?.HandleBack() ?? false)) activity.Finish();
        }
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        if (_binding is not null) _binding.PlatformBrightness = SystemTheme.Brightness();
    }

    protected override void OnResume()
    {
        base.OnResume();
        _view?.OnResume();
        _binding?.RequestFrame();
    }

    protected override void OnPause()
    {
        _view?.OnPause();
        base.OnPause();
    }

    // ---- activity results (file pickers) ----

    readonly Dictionary<int, TaskCompletionSource<Intent?>> _pendingResults = new();
    int _nextRequestCode = 0x5A00;

    /// <summary>Launches <paramref name="intent"/> and completes with the result data, or null if the user backed out.</summary>
    internal Task<Intent?> StartForResultAsync(Intent intent)
    {
        var source = new TaskCompletionSource<Intent?>();
        RunOnUiThread(() =>
        {
            int code = _nextRequestCode++;
            _pendingResults[code] = source;
            try { StartActivityForResult(intent, code); }
            catch (Exception e)
            {
                _pendingResults.Remove(code);
                source.SetException(e); // no app can handle the request
            }
        });
        return source.Task;
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (_pendingResults.Remove(requestCode, out var source)) source.SetResult(resultCode == Result.Ok ? data : null);
    }

    string? ReadClipboard()
    {
        var clipboard = (ClipboardManager?)GetSystemService(ClipboardService);
        return clipboard?.PrimaryClip?.GetItemAt(0)?.CoerceToText(this)?.ToString();
    }

    void WriteClipboard(string text) =>
        ((ClipboardManager?)GetSystemService(ClipboardService))?.PrimaryClip = ClipData.NewPlainText("text", text);

    sealed class InsetsListener : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        public WindowInsets OnApplyWindowInsets(View v, WindowInsets insets)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var bars = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.Ime() | WindowInsets.Type.DisplayCutout());
                v.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
                return WindowInsets.Consumed;
            }
            else
            {
                v.SetPadding(insets.SystemWindowInsetLeft, insets.SystemWindowInsetTop, insets.SystemWindowInsetRight, insets.SystemWindowInsetBottom);
            }
            return insets.ConsumeSystemWindowInsets();
        }
    }
}
