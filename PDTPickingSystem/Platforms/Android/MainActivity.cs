using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
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
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetTheme(Resource.Style.MainTheme);
            WindowCompat.SetDecorFitsSystemWindows(Window, false);
        }
        public override bool DispatchKeyEvent(KeyEvent e)
        {
            if (e.Action == KeyEventActions.Up)
            {
                var currentPage = Microsoft.Maui.Controls.Application.Current
                                    .MainPage
                                    .Navigation
                                    .NavigationStack
                                    .LastOrDefault();

                if (currentPage != null)
                {
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
                    if (currentPage is PDTPickingSystem.Views.SetRefPage setRefPage)
                    {
                        switch (e.KeyCode)
                        {
                            case Keycode.F1:
                                MainThread.BeginInvokeOnMainThread(() => setRefPage.btnBack_Clicked(null, null));
                                return true;
                        }
                    }
                    if (currentPage is PDTPickingSystem.Views.CheckingPage checkingPage)
                    {
                        switch (e.KeyCode)
                        {
                            case Keycode.Escape:
                                MainThread.BeginInvokeOnMainThread(() => checkingPage.OnEscapePressed());
                                return true;
                            case Keycode.Tab:
                                MainThread.BeginInvokeOnMainThread(() => checkingPage.OnF2Pressed());
                                return true;
                            case Keycode.F1:
                                MainThread.BeginInvokeOnMainThread(() => checkingPage.OnF1Pressed());
                                return true;
                            case Keycode.F2:
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