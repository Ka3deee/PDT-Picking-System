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
                var currentPage = Microsoft.Maui.Controls.Application.Current
                                    .MainPage
                                    .Navigation
                                    .NavigationStack
                                    .LastOrDefault();

                if (currentPage != null)
                {
                    // --- Handle PickingPage keys ---
                    if (currentPage is PDTPickingSystem.Views.PickingPage pickingPage)
                    {
                        switch (e.KeyCode)
                        {
                            case Keycode.F1:
                                MainThread.BeginInvokeOnMainThread(() => pickingPage.OnF1Pressed());
                                return true;

                            case Keycode.F2:
                                MainThread.BeginInvokeOnMainThread(() => pickingPage.OnF2Pressed());
                                return true;
                        }
                    }

                    // --- Handle SetRefPage keys ---
                    if (currentPage is PDTPickingSystem.Views.SetRefPage setRefPage)
                    {
                        switch (e.KeyCode)
                        {
                            case Keycode.F1: // Treat as Escape
                                MainThread.BeginInvokeOnMainThread(() => setRefPage.btnBack_Clicked(null, null));
                                return true;

                                // You can add more keys if needed
                        }
                    }
                }
            }

            return base.DispatchKeyEvent(e);
        }
    }
}