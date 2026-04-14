using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;

namespace PDTPickingSystem.Helpers
{
    public static class SignalRService
    {
        private static HubConnection? _connection;

        private static string HubUrl =>
            $"http://{AppGlobal.sServer}:5000/notificationHub";

        // Normalize key — both PDT and desktop must use same format
        private static string UserKey =>
            AppGlobal.sUserName.Trim().ToUpper();

        public static async Task ConnectAsync(string eeNumber)
        {
            try
            {
                _connection = new HubConnectionBuilder()
                    .WithUrl(HubUrl)
                    .WithAutomaticReconnect()
                    .Build();

                _connection.On<string>("ReceiveMessage", (message) =>
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Shell.Current.DisplayAlert(
                            "📩 Message from Office",
                            message,
                            "OK");
                    });
                });

                await _connection.StartAsync();

                // Register using normalized full name (LNAME, FNAME MI)
                await _connection.InvokeAsync("RegisterUser", UserKey);

                Debug.WriteLine($"[SignalR] Connected and registered as: '{UserKey}'");

                // DEBUG: confirm registration key on device — remove after confirming
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Shell.Current.DisplayAlert(
                        "✅ SignalR Connected",
                        $"Registered as:\n'{UserKey}'\nServer: {HubUrl}",
                        "OK");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR] Connection failed: {ex.Message}");
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Shell.Current.DisplayAlert(
                        "❌ SignalR Failed",
                        $"User: {UserKey}\nError: {ex.Message}\nServer: {HubUrl}",
                        "OK");
                });
            }
        }

        public static async Task DisconnectAsync()
        {
            if (_connection != null)
            {
                await _connection.StopAsync();
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
    }
}