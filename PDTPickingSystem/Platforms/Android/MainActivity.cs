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
                // Fully qualify MAUI Application
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
                                // Add more keys if needed
                        }
                    }

                    // --- Handle CheckingPage keys ---
                    if (currentPage is PDTPickingSystem.Views.CheckingPage checkingPage)
                    {
                        switch (e.KeyCode)
                        {
                            case Keycode.Escape: // Escape key closes page
                                MainThread.BeginInvokeOnMainThread(() => checkingPage.OnEscapePressed());
                                return true;

                            case Keycode.Tab: // ✅ FIXED: Tab key focuses barcode
                                MainThread.BeginInvokeOnMainThread(() => checkingPage.OnF2Pressed());
                                return true;

                            case Keycode.F1: // F1 key
                                MainThread.BeginInvokeOnMainThread(() => checkingPage.OnF1Pressed());
                                return true;

                            case Keycode.F2: // F2 key
                                MainThread.BeginInvokeOnMainThread(() => checkingPage.OnF2Pressed());
                                return true;

                                // Add more keys if needed
                        }
                    }
                }
            }

            return base.DispatchKeyEvent(e);
        }
    }
}