using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using System.Linq;

namespace PDTPickingSystem
{
    [Activity(Theme = "@style/Maui.SplashTheme",
              MainLauncher = true,
              LaunchMode = LaunchMode.SingleTop,
              ConfigurationChanges = ConfigChanges.ScreenSize |
                                     ConfigChanges.Orientation |
                                     ConfigChanges.UiMode |
                                     ConfigChanges.ScreenLayout |
                                     ConfigChanges.SmallestScreenSize |
                                     ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        public override bool DispatchKeyEvent(KeyEvent e)
        {
            if (e.Action == KeyEventActions.Up) // Key released
            {
                var page = Microsoft.Maui.Controls.Application.Current
                            .MainPage
                            .Navigation
                            .NavigationStack
                            .LastOrDefault() as PDTPickingSystem.Views.PickingPage;

                if (page != null)
                {
                    switch (e.KeyCode)
                    {
                        case Keycode.F1:
                            MainThread.BeginInvokeOnMainThread(() => page.OnF1Pressed());
                            return true;

                        case Keycode.F2:
                            MainThread.BeginInvokeOnMainThread(() => page.OnF2Pressed());
                            return true;
                    }
                }
            }

            return base.DispatchKeyEvent(e);
        }
    }
}
