using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PDTPickingSystem.Helpers.Interfaces
{
    public interface IWifiService
    {
        /// <summary>
        /// Returns the currently connected WiFi SSID.
        /// If SSID is hidden or cannot be detected, returns a descriptive string like "Connected (SSID hidden)" or "Not connected".
        /// </summary>
        string GetConnectedWifiName();
    }
}

