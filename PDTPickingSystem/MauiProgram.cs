using Microsoft.Extensions.Logging;
using PDTPickingSystem.Helpers.Interfaces;
using Plugin.Maui.Audio;

#if ANDROID
using Android.App;
using PDTPickingSystem.Platforms.Android;
using System.IO;
#endif

namespace PDTPickingSystem
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("Montserrat-Regular.ttf", "MontserratRegular");
                    fonts.AddFont("Montserrat-Semibold.ttf", "MontserratSemibold");
                });

            // Register WiFi Service
#if ANDROID
            builder.Services.AddSingleton<IWifiService, WifiService_Android>();
#else
            builder.Services.AddSingleton<IWifiService, WifiService_Default>();
#endif

            // Register Audio Service for Idle Alarm
            builder.Services.AddSingleton(AudioManager.Current);

            // REMOVE ENTRY UNDERLINE (Android)
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(
                "NoUnderline",
                (handler, view) =>
                {
#if ANDROID
                    handler.PlatformView.BackgroundTintList =
                        Android.Content.Res.ColorStateList.ValueOf(
                            Android.Graphics.Color.Transparent);
#endif
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            var app = builder.Build();

#if ANDROID
            // Delete old root-level wifi.txt if it exists
            _ = DeleteOldRootWifiFileAsync();
#endif

            return app;
        }

#if ANDROID
        private static async Task DeleteOldRootWifiFileAsync()
        {
            try
            {
                var dir = Android.App.Application.Context.GetExternalFilesDir(null);
                if (dir == null)
                    return;

                string oldWifiFile = Path.Combine(dir.AbsolutePath, "wifi.txt");
                if (File.Exists(oldWifiFile))
                    File.Delete(oldWifiFile);

                // No need to create a new wifi.txt here; AppGlobal handles it in Backup/PDTPicking
            }
            catch (System.Exception ex)
            {
                Android.Util.Log.Warn(
                    "PDTPickingSystem",
                    $"Failed to delete old wifi.txt: {ex.Message}");
            }
        }
#endif
    }
}
