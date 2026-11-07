using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Timer.Core;

namespace Timer.Android;

[Application]
public class AndroidApp : AvaloniaAndroidApplication<App>
{
    protected AndroidApp(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        App.SoundPlayback = new SoundPlayback(AndroidSound.PlayAsync);
        return base.CustomizeAppBuilder(builder).WithInterFont();
    }
}
