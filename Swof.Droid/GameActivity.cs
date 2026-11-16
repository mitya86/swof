using Android.Content.PM;
using Android.Views;
using Swof.Engine;

[assembly: Android.App.Application(Label = Product.Name)]
[assembly: Android.App.UsesFeature(GLESVersion = 0x00020000, Required = true)]

namespace Swof.Droid;

/// <summary>Starts the same game the desktop host runs, in landscape and full screen.</summary>
[Android.App.Activity(
    Label = Product.Name,
    MainLauncher = true,
    AlwaysRetainTaskState = true,
    LaunchMode = LaunchMode.SingleInstance,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize)]
public class GameActivity : Microsoft.Xna.Framework.AndroidGameActivity
{
    protected override void OnCreate(Android.OS.Bundle? bundle)
    {
        base.OnCreate(bundle);
        var game = new Swof.Game.SwofGame();
        SetContentView((View)game.Services.GetService(typeof(View))!);
        game.Run();
    }
}
