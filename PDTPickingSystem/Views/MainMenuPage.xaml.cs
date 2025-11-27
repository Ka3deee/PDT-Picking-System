using Microsoft.Maui.Controls;
using PDTPickingSystem.Helpers;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Android;
using AEnvironment = Android.OS.Environment;   // <--- FIX: Avoids ambiguity

namespace PDTPickingSystem.Views
{
    public partial class MainMenuPage : ContentPage
    {
        public MainMenuPage()
        {
            InitializeComponent();
            Appearing += MainMenuPage_Appearing;
        }

        // ------------------------------------
        // PAGE APPEARING
        // ------------------------------------
        private async void MainMenuPage_Appearing(object sender, EventArgs e)
        {
            // Android storage runtime permission
            await RequestStoragePermissionAsync();

            // Ensure folder exists in:
            // /storage/emulated/0/Android/data/com.companyname.pdtpickingsystem/Backup/PDTPicking
            EnsureBackupFolderExists();

            // Load server from wifi.txt
            await AppGlobal.LoadServerConfigAsync();
            lblStatus.Text = $"Server: {AppGlobal.Server}";

            // Check DB connection
            await CheckDatabaseConnectionAsync();

            // Display user name
            lblUser.Text = string.IsNullOrEmpty(AppGlobal.UserName)
                ? "User: (none)"
                : $"User: {AppGlobal.UserName}";
        }

        // ------------------------------------
        // PERMISSIONS (Android)
        // ------------------------------------
        private async Task RequestStoragePermissionAsync()
        {
#if ANDROID
            var status = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();

            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.StorageWrite>();
            }
#endif
        }

        // ------------------------------------
        // ENSURE BACKUP FOLDER EXISTS
        // ------------------------------------
        private void EnsureBackupFolderExists()
        {
            try
            {
#if ANDROID
                // Main external storage root
                string root = AEnvironment.ExternalStorageDirectory.AbsolutePath;

                // Example full path:
                // /storage/emulated/0/Android/data/com.companyname.pdtpickingsystem/Backup/PDTPicking
                string fullPath = Path.Combine(
                    root,
                    "Android", "data",
                    "com.companyname.pdtpickingsystem",
                    "Backup", "PDTPicking"
                );

                if (!Directory.Exists(fullPath))
                    Directory.CreateDirectory(fullPath);
#else
                // Other platforms fallback
                string fallback = Path.Combine(FileSystem.AppDataDirectory, "Backup", "PDTPicking");
                if (!Directory.Exists(fallback))
                    Directory.CreateDirectory(fallback);
#endif
            }
            catch(Exception E)
            {
                // Safe fail
                Console.WriteLine("Error creating backup folder: " + E.Message);

            }
        }

        // ------------------------------------
        // CHECK SQL CONNECTION
        // ------------------------------------
        private async Task CheckDatabaseConnectionAsync()
        {
            if (AppGlobal.IsLoaded) return;

            lblStatus.Text = "Checking connection...";
            await Task.Delay(500);

            bool connected = await AppGlobal.ConnectSqlAsync();

            lblStatus.Text = connected
                ? $"✅ Connected to {AppGlobal.Server}\\dbPicking3"
                : $"❌ Cannot connect to {AppGlobal.Server}";

            AppGlobal.IsLoaded = true;
        }

        // ------------------------------------
        // OPTION 1 – START PICKING
        // ------------------------------------
        private async void BtnOpt1_Clicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(AppGlobal.UserID))
            {
                await DisplayAlert("No User ID!", "Please set a User ID first!", "OK");
                return;
            }

            if (string.IsNullOrEmpty(AppGlobal.PickNo))
            {
                await DisplayAlert("No Picking!", "No Picking Reference Set! Please set reference # first.", "OK");
                return;
            }

            if (AppGlobal.IsChecker)
            {
                await DisplayAlert("System Says", "You are a Checker, not a Picker!", "OK");
                return;
            }

            var pickingPage = new PickingPage();
            var navPage = new NavigationPage(pickingPage);
            await Navigation.PushModalAsync(navPage);
        }

        // ------------------------------------
        // OPTION 2 – START CHECKING
        // ------------------------------------
        private async void BtnOpt2_Clicked(object sender, EventArgs e)
        {
            await DisplayAlert("Checking", "Start Checking option selected.", "OK");
        }

        // ------------------------------------
        // OPTION 3 – SET USER
        // ------------------------------------
        private async void BtnOpt3_Clicked(object sender, EventArgs e)
        {
            var setUserPage = new SetUserPage();
            var navPage = new NavigationPage(setUserPage);
            await Navigation.PushModalAsync(navPage);

            string userId = await setUserPage.TaskCompletion.Task;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                bool ok = await AppGlobal.LoadUserInfoAsync(userId);
                if (!ok)
                {
                    await DisplayAlert("User Not Found!", "Invalid User ID!", "OK");
                    return;
                }

                lblUser.Text = $"User: {AppGlobal.UserName}";
            }
        }

        // ------------------------------------
        // OPTION 4 – CONFIRM CHECK
        // ------------------------------------
        private async void BtnOpt4_Clicked(object sender, EventArgs e)
        {
            await DisplayAlert("Confirm Check", "Confirm Check option selected.", "OK");
        }

        // ------------------------------------
        // OPTION 5 – SERVER SETTINGS
        // ------------------------------------
        private async void BtnOpt5_Clicked(object sender, EventArgs e)
        {
            string newServer = await DisplayPromptAsync(
                title: "Server Settings",
                message: "Enter SQL Server IP:",
                accept: "OK",
                cancel: "Cancel",
                initialValue: AppGlobal.Server
            );

            if (string.IsNullOrWhiteSpace(newServer)) return;

            AppGlobal.Server = newServer.Trim();

            try
            {
                await AppGlobal.SaveServerConfigAsync();
                lblStatus.Text = $"Updated Server: {AppGlobal.Server}";
                await DisplayAlert("Saved!", "Server settings saved successfully!", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error!", ex.Message, "OK");
            }
        }

        // ------------------------------------
        // OPTION 6 – SET REFERENCE
        // ------------------------------------
        private async void BtnOpt6_Clicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(SetRefPage));
        }

        // ------------------------------------
        // OPTION 7 – EXIT APP
        // ------------------------------------
        private async void BtnOpt7_Clicked(object sender, EventArgs e)
        {
            if (await DisplayAlert("Exit", "Exit PDT Picking Application?", "Yes", "No"))
                Application.Current.Quit();
        }
    }
}
