using Android.Content;
using Android.Net.Wifi;
using Android.OS;
using PDTPickingSystem.Helpers.Interfaces;

namespace PDTPickingSystem.Platforms.Android
{
    public class WifiService_Android : IWifiService
    {
        public string GetConnectedWifiName()
        {
            try
            {
                var context = global::Android.App.Application.Context;
                var wifiManager = (WifiManager)context.GetSystemService(Context.WifiService);
                if (wifiManager == null)
                    return "Not connected";

                if (!wifiManager.IsWifiEnabled)
                    return "Not connected";

#pragma warning disable CS0618
                var wifiInfo = wifiManager.ConnectionInfo;
#pragma warning restore CS0618

                if (wifiInfo == null)
                    return "Not connected";

                string ssid = wifiInfo.SSID;

                if (string.IsNullOrWhiteSpace(ssid) ||
                    ssid == "<unknown ssid>" ||
                    ssid == "0x" ||
                    ssid == "0x0" ||
                    ssid == "\"\"" ||
                    ssid.Contains("0x"))
                {
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                        return "Connected (SSID hidden)";
                    return "Not connected";
                }

                ssid = ssid.Replace("\"", "");
                return ssid;
            }
            catch
            {
                return "Not connected";
            }
        }
    }
}