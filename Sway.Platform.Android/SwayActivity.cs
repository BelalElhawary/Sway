using Android.Content;
using Android.Content.Res;
using Android.OS;
using Android.Views;
using Android.Widget;
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

    /// <summary>The widget tree to run.</summary>
    protected abstract Widget CreateRoot();

    /// <summary>The binding driving this activity, available after <c>OnCreate</c>.</summary>
    protected WidgetsBinding Binding => _binding ?? throw new InvalidOperationException("The activity has not been created yet.");

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        SystemTheme.Source = new AndroidThemeSource(this);
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
