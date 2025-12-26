using Microsoft.Extensions.Logging;
using PDTPickingSystem.Helpers.Interfaces;

#if ANDROID
using PDTPickingSystem.Platforms.Android;
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
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Register WiFi Service
#if ANDROID
            builder.Services.AddSingleton<IWifiService, WifiService_Android>();
#else
            builder.Services.AddSingleton<IWifiService, PDTPickingSystem.Helpers.WifiService_Default>();
#endif

#if DEBUG
            builder.Logging.AddDebug();
#endif
            return builder.Build();
        }
    }
}