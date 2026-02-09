using Android;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using PDTPickingSystem.Helpers;
using System;
using System.IO;
using System.Threading.Tasks;
using AEnvironment = Android.OS.Environment;

namespace PDTPickingSystem.Views
{
    public partial class MainMenuPage : ContentPage
    {
        private IDispatcherTimer _wifiSignalTimer;
        public string VersionText { get; set; }

        // ✅ NEW: Loading state flags for each button
        private bool _isLoadingOpt1 = false;
        private bool _isLoadingOpt2 = false;
        private bool _isLoadingOpt3 = false;
        private bool _isLoadingOpt4 = false;
        private bool _isLoadingOpt5 = false;
        private bool _isLoadingOpt6 = false;
        private bool _isLoadingOpt7 = false;

        public MainMenuPage()
        {
            InitializeComponent();

            VersionText = $"PDT Picking System v{AppInfo.VersionString} • 2025 - {DateTime.Now:yyyy}";

            BindingContext = this;

            Appearing += MainMenuPage_Appearing;
            Disappearing += MainMenuPage_Disappearing;
            NavigationPage.SetHasNavigationBar(this, false);
        }

        // ====================================================================
        // PAGE APPEARING
        // ====================================================================
        private async void MainMenuPage_Appearing(object sender, EventArgs e)
        {
            // Android storage runtime permission
            await RequestStoragePermissionAsync();

            // Ensure backup folder exists
            EnsureBackupFolderExists();

            // Load server config from wifi.txt
            await AppGlobal.LoadServerConfigAsync();
            UpdateServerStatusLabel();

            // Display user name
            UpdateUserLabel();

            // Check database connection
            await CheckDatabaseConnectionAsync();

            // Auto-update check (only once per app session)
            await CheckForUpdatesAsync();

            // Start WiFi signal monitoring
            StartWifiSignalMonitoring();
        }

        // ====================================================================
        // PAGE DISAPPEARING
        // ====================================================================
        private void MainMenuPage_Disappearing(object sender, EventArgs e)
        {
            // Stop WiFi signal timer when leaving page
            StopWifiSignalMonitoring();
        }

        // ====================================================================
        // PERMISSIONS (Android)
        // ====================================================================
        private async Task RequestStoragePermissionAsync()
        {
#if ANDROID
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();

                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.StorageWrite>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Storage permission error: {ex.Message}");
            }
#endif
        }

        // ====================================================================
        // ENSURE BACKUP FOLDER EXISTS
        // ====================================================================
        private void EnsureBackupFolderExists()
        {
            try
            {
#if ANDROID
                string root = AEnvironment.ExternalStorageDirectory?.AbsolutePath;
                if (!string.IsNullOrEmpty(root))
                {
                    string fullPath = Path.Combine(
                        root,
                        "Android", "data",
                        AppInfo.PackageName,
                        "files",
                        "Backup", "PDTPicking"
                    );

                    if (!Directory.Exists(fullPath))
                        Directory.CreateDirectory(fullPath);
                }
#else
                string fallback = Path.Combine(FileSystem.AppDataDirectory, "Backup", "PDTPicking");
                if (!Directory.Exists(fallback))
                    Directory.CreateDirectory(fallback);
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating backup folder: {ex.Message}");
            }
        }

        // ====================================================================
        // CHECK SQL CONNECTION
        // ====================================================================
        private async Task CheckDatabaseConnectionAsync()
        {
            if (string.IsNullOrEmpty(AppGlobal.sServer) ||
                AppGlobal.sServer == "000.000.000.000")
            {
                lblStatus.Text = "⚠️ Server not configured";
                return;
            }

            lblStatus.Text = "Checking connection...";
            await Task.Delay(500);

            try
            {
                using var conn = await AppGlobal._SQL_Connect();
                bool connected = conn != null;

                lblStatus.Text = connected
                    ? $"✅ Connected to Server: {AppGlobal.sServer}"
                    : $"❌ Cannot connect to {AppGlobal.sServer}";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"❌ Connection error: {ex.Message}";
            }
        }

        // ====================================================================
        // AUTO-UPDATE CHECK (VB.NET: tmrCheckUpdate_Tick)
        // ====================================================================
        private async Task CheckForUpdatesAsync()
        {
            if (AppGlobal.isLoaded)
                return;

            if (string.IsNullOrEmpty(AppGlobal.sServer) ||
                AppGlobal.sServer == "000.000.000.000")
            {
                AppGlobal.isLoaded = true;
                return;
            }

            try
            {
                await AppGlobal._CheckUpdate(
                    AppGlobal.sServer,
                    AppGlobal.SqlUser,
                    AppGlobal.SqlPass,
                    "dbPicking3",
                    "LCC Picking System.exe"
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update check error: {ex.Message}");
            }
            finally
            {
                AppGlobal.isLoaded = true;
            }
        }

        // ====================================================================
        // WIFI SIGNAL MONITORING
        // ====================================================================
        private void StartWifiSignalMonitoring()
        {
            try
            {
                _wifiSignalTimer = Application.Current.Dispatcher.CreateTimer();
                _wifiSignalTimer.Interval = TimeSpan.FromSeconds(5);
                _wifiSignalTimer.Tick += (s, e) => UpdateWifiSignal();
                _wifiSignalTimer.Start();

                UpdateWifiSignal();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WiFi timer error: {ex.Message}");
            }
        }

        private void StopWifiSignalMonitoring()
        {
            try
            {
                if (_wifiSignalTimer != null)
                {
                    _wifiSignalTimer.Stop();
                    _wifiSignalTimer = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Stop WiFi timer error: {ex.Message}");
            }
        }

        private void UpdateWifiSignal()
        {
            try
            {
                string wifiStatus = AppGlobal.GetWifiStatus();
                AppGlobal._FlushMemory();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WiFi update error: {ex.Message}");
            }
        }

        // ====================================================================
        // UPDATE UI LABELS
        // ====================================================================
        private void UpdateUserLabel()
        {
            lblUser.Text = string.IsNullOrEmpty(AppGlobal.sUserName)
                ? "User: (none)"
                : $"User: {AppGlobal.sUserName}";
        }

        private void UpdateServerStatusLabel()
        {
            lblStatus.Text = string.IsNullOrEmpty(AppGlobal.sServer) ||
                            AppGlobal.sServer == "000.000.000.000"
                ? "Server: Not configured"
                : $"Server: {AppGlobal.sServer}";
        }

        // ====================================================================
        // ✅ HELPER: SET BUTTON LOADING STATE
        // ====================================================================
        private void SetButtonLoading(Button button, View loadingView, bool isLoading)
        {
            button.IsEnabled = !isLoading;
            loadingView.IsVisible = isLoading;
        }

        // ====================================================================
        // OPTION 1 – START PICKING
        // ====================================================================
        private async void BtnOpt1_Clicked(object sender, EventArgs e)
        {
            if (_isLoadingOpt1) return;
            _isLoadingOpt1 = true;
            SetButtonLoading(btnOpt1, loadingOpt1, true);

            try
            {
                if (string.IsNullOrEmpty(AppGlobal.sEENo))
                {
                    await DisplayAlert("No User ID!", "Please set a User ID first!", "OK");
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
            catch (Exception ex)
            {
                await DisplayAlert("Error!", $"Failed to open Picking page: {ex.Message}", "OK");
            }
            finally
            {
                _isLoadingOpt1 = false;
                SetButtonLoading(btnOpt1, loadingOpt1, false);
            }
        }

        // ====================================================================
        // OPTION 2 – START CHECKING
        // ====================================================================
        private async void BtnOpt2_Clicked(object sender, EventArgs e)
        {
            if (_isLoadingOpt2) return;
            _isLoadingOpt2 = true;
            SetButtonLoading(btnOpt2, loadingOpt2, true);

            try
            {
                if (string.IsNullOrEmpty(AppGlobal.sEENo))
                {
                    await DisplayAlert("No User ID!", "Please set a User ID first!", "OK");
                    return;
                }

                string pickSetup = await AppGlobal._GetPickNo();
                if (string.IsNullOrEmpty(pickSetup))
                {
                    await DisplayAlert("No Picking!", "No Picking Reference Set! Please ask to set reference #", "OK");
                    return;
                }

                if (pickSetup == "Per Transfer")
                {
                    AppGlobal.isSummary = 2;
                }

                var checkerPage = new CheckingPage(this);
                var navChecker = new NavigationPage(checkerPage);
                await Navigation.PushModalAsync(navChecker);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error!", $"Failed to open Checking page: {ex.Message}", "OK");
            }
            finally
            {
                _isLoadingOpt2 = false;
                SetButtonLoading(btnOpt2, loadingOpt2, false);
            }
        }

        // ====================================================================
        // OPTION 3 – SET USER
        // ====================================================================
        private async void BtnOpt3_Clicked(object sender, EventArgs e)
        {
            if (_isLoadingOpt3) return;
            _isLoadingOpt3 = true;
            SetButtonLoading(btnOpt3, loadingOpt3, true);

            try
            {
                await Shell.Current.GoToAsync(nameof(SetUserPage));
                UpdateUserLabel();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to open Set User page: {ex.Message}", "OK");
            }
            finally
            {
                _isLoadingOpt3 = false;
                SetButtonLoading(btnOpt3, loadingOpt3, false);
            }
        }

        // ====================================================================
        // OPTION 4 – CONFIRM CHECK
        // ====================================================================
        private async void BtnOpt4_Clicked(object sender, EventArgs e)
        {
            if (_isLoadingOpt4) return;
            _isLoadingOpt4 = true;
            SetButtonLoading(btnOpt4, loadingOpt4, true);

            try
            {
                if (string.IsNullOrEmpty(AppGlobal.sEENo))
                {
                    await DisplayAlert("No User ID!", "Please set a User ID first!", "OK");
                    return;
                }

                string pickSetup = await AppGlobal._GetPickNo();
                if (string.IsNullOrEmpty(pickSetup))
                {
                    await DisplayAlert("No Picking!", "No Picking Reference Set! Please ask to set reference #", "OK");
                    return;
                }

                if (!AppGlobal.IsChecker)
                {
                    await DisplayAlert("System Says", "Only Checker can use this option!", "OK");
                    return;
                }

                var frmCheck = new ConfirmCheckPage();
                await Navigation.PushModalAsync(frmCheck);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error!", $"An error occurred: {ex.Message}", "OK");
            }
            finally
            {
                _isLoadingOpt4 = false;
                SetButtonLoading(btnOpt4, loadingOpt4, false);
            }
        }

        // ====================================================================
        // OPTION 5 – SERVER SETTINGS
        // ====================================================================
        private async void BtnOpt5_Clicked(object sender, EventArgs e)
        {
            if (_isLoadingOpt5) return;
            _isLoadingOpt5 = true;
            SetButtonLoading(btnOpt5, loadingOpt5, true);

            try
            {
                string newServer = await DisplayPromptAsync(
                    title: "Server Settings",
                    message: "Enter SQL Server IP:",
                    accept: "Save",
                    cancel: "Cancel",
                    placeholder: "",
                    initialValue: AppGlobal.sServer,
                    keyboard: Keyboard.Text
                );

                if (string.IsNullOrWhiteSpace(newServer))
                    return;

                AppGlobal.sServer = newServer.Trim();

                await AppGlobal.SaveServerConfigAsync();
                UpdateServerStatusLabel();
                await DisplayAlert("Server Updated!", $"Server updated to: {AppGlobal.sServer}", "OK");
                await CheckDatabaseConnectionAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error!", $"Failed to save: {ex.Message}", "OK");
            }
            finally
            {
                _isLoadingOpt5 = false;
                SetButtonLoading(btnOpt5, loadingOpt5, false);
            }
        }

        // ====================================================================
        // OPTION 6 – SET REFERENCE
        // ====================================================================
        private async void BtnOpt6_Clicked(object sender, EventArgs e)
        {
            if (_isLoadingOpt6) return;
            _isLoadingOpt6 = true;
            SetButtonLoading(btnOpt6, loadingOpt6, true);

            try
            {
                await Shell.Current.GoToAsync(nameof(SetRefPage));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to open Set Reference page: {ex.Message}", "OK");
            }
            finally
            {
                _isLoadingOpt6 = false;
                SetButtonLoading(btnOpt6, loadingOpt6, false);
            }
        }

        // ====================================================================
        // OPTION 7 – EXIT APP
        // ====================================================================
        private async void BtnOpt7_Clicked(object sender, EventArgs e)
        {
            if (_isLoadingOpt7) return;
            _isLoadingOpt7 = true;
            SetButtonLoading(btnOpt7, loadingOpt7, true);

            try
            {
                await ConfirmExitAsync();
            }
            finally
            {
                _isLoadingOpt7 = false;
                SetButtonLoading(btnOpt7, loadingOpt7, false);
            }
        }

        // ====================================================================
        // VERSION INFO
        // ====================================================================
        private async void ImgLogo_Tapped(object sender, EventArgs e)
        {
            await DisplayAlert("LCC Picking System", $"Version {AppGlobal.sysVersion}\n\nPDT Picking Application", "OK");
        }

        // ====================================================================
        // EXIT CONFIRMATION
        // ====================================================================
        private async Task ConfirmExitAsync()
        {
            bool exit = await DisplayAlert(
                "EXIT",
                "Exit PDT Picking System?",
                "Yes",
                "No"
            );

            if (exit)
            {
                StopWifiSignalMonitoring();
                Application.Current.Quit();
            }
        }

        // ====================================================================
        // HANDLE HARDWARE BACK BUTTON
        // ====================================================================
        protected override bool OnBackButtonPressed()
        {
            _ = ConfirmExitAsync();
            return true;
        }
    }
}