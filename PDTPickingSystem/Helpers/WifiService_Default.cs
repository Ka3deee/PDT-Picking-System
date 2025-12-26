using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PDTPickingSystem.Helpers.Interfaces;

namespace PDTPickingSystem.Helpers
{
    public class WifiService_Default : IWifiService
    {
        public string GetConnectedWifiName()
        {
            return "WiFi status unavailable";
        }
    }
}
