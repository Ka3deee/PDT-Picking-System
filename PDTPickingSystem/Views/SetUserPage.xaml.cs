using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;
using PDTPickingSystem.Helpers;
using Microsoft.Data.SqlClient;

namespace PDTPickingSystem.Views
{
    public partial class SetUserPage : ContentPage
    {
        public TaskCompletionSource<string> TaskCompletion { get; } = new TaskCompletionSource<string>();

        public SetUserPage()
        {
            InitializeComponent();

            // Hide loader at start
            actLoading.IsRunning = false;
            actLoading.IsVisible = false;

            UpdateCurrentUserLabel();
        }

        private void UpdateCurrentUserLabel()
        {
            if (!string.IsNullOrEmpty(AppGlobal.UserID))
            {
                lblCurrentUser.Text = $"User: {AppGlobal.UserName}";
            }
            else
            {
                lblCurrentUser.Text = "User: (none)";
            }
        }

        private async void OkButton_Clicked(object sender, EventArgs e)
        {
            string enteredId = txtEENo.Text?.Trim();

            if (string.IsNullOrWhiteSpace(enteredId))
            {
                await DisplayAlert("Error!", "Please enter a User ID.", "OK");
                return;
            }

            try
            {
                // Disable controls and show loader
                btnApply.IsEnabled = false;
                btnBack.IsEnabled = false;
                actLoading.IsRunning = true;
                actLoading.IsVisible = true;

                // Connect SQL first
                bool connected = await AppGlobal.ConnectSqlAsync();
                if (!connected)
                {
                    await DisplayAlert("Connection Error!", "Unable to connect to SQL Server. Check network or server settings.", "OK");
                    return;
                }

                // Load user info
                bool validUser = await AppGlobal.LoadUserInfoAsync(enteredId);
                if (!validUser)
                {
                    await DisplayAlert("Invalid User!", "User not found or inactive.", "OK");
                    return;
                }

                // Update UI
                lblUser.Text = AppGlobal.UserName;
                UpdateCurrentUserLabel();

                // Return user ID to previous page
                TaskCompletion.SetResult(AppGlobal.UserID);

                await DisplayAlert("Welcome!", $"Hello, {AppGlobal.UserName}!", "OK");
                await Navigation.PopModalAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error!", $"Login failed.\n{ex.Message}", "OK");
            }
            finally
            {
                // Reset controls
                btnApply.IsEnabled = true;
                btnBack.IsEnabled = true;
                actLoading.IsRunning = false;
                actLoading.IsVisible = false;
            }
        }

        private async void CancelButton_Clicked(object sender, EventArgs e)
        {
            TaskCompletion.SetResult(string.Empty);
            await Navigation.PopModalAsync();
        }
    }
}
