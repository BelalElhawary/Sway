using Android.App;
using Android.Content.PM;
using Sway.Example;
using Sway.Media;
using Sway.Widgets;

namespace Sway.Example.Android;

[Activity(Label = "Sway Example", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.Density | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.LayoutDirection,
    WindowSoftInputMode = global::Android.Views.SoftInput.AdjustResize)]
public class MainActivity : SwayActivity
{
    protected override Widget CreateRoot()
    {
        LibVlcMediaBackend.Install();
        return new DemoRoot();
    }
}
