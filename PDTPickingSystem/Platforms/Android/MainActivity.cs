using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using System.Linq;

namespace PDTPickingSystem
{
    [Activity(Theme = "@style/Maui.SplashTheme",  // ✅ Already correct!
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
        // ✅ ADD THIS METHOD - Switch from splash theme to main theme
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Switch to main app theme after splash screen
            SetTheme(Resource.Style.MainTheme);
        }

        // ✅ Keep existing DispatchKeyEvent method
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
                    // --- Handles PickingPage keys ---
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

                    // --- Handles SetRefPage keys ---
                    if (currentPage is PDTPickingSystem.Views.SetRefPage setRefPage)
                    {
                        switch (e.KeyCode)
                        {
                            case Keycode.F1: // as Escape
                                MainThread.BeginInvokeOnMainThread(() => setRefPage.btnBack_Clicked(null, null));
                                return true;
                        }
                    }

                    // --- Handles CheckingPage keys ---
                    if (currentPage is PDTPickingSystem.Views.CheckingPage checkingPage)
                    {
                        switch (e.KeyCode)
                        {
                            case Keycode.Escape: // Escape key closes page
                                MainThread.BeginInvokeOnMainThread(() => checkingPage.OnEscapePressed());
                                return true;
                            case Keycode.Tab: // Tab key focuses barcode
                                MainThread.BeginInvokeOnMainThread(() => checkingPage.OnF2Pressed());
                                return true;
                            case Keycode.F1: // F1 key
                                MainThread.BeginInvokeOnMainThread(() => checkingPage.OnF1Pressed());
                                return true;
                            case Keycode.F2: // F2 key
                                MainThread.BeginInvokeOnMainThread(() => checkingPage.OnF2Pressed());
                                return true;
                        }
                    }
                }
            }

            return base.DispatchKeyEvent(e);
        }
    }
}