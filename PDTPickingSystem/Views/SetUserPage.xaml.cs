using Microsoft.Maui.Controls;
using Microsoft.Data.SqlClient;
using PDTPickingSystem.Helpers;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PDTPickingSystem.Views
{
    public partial class SetUserPage : ContentPage
    {
        // ====================================================================
        // CLASS-LEVEL VARIABLES (VB equivalents of Tag properties)
        // ====================================================================
        private string lblNameTag = "";      // VB: lblName.Tag (stores EENo)
        private int _storedUserID = 0;       // ✅ ADDED: VB: txtEENo.Tag (stores ID)

        // ====================================================================
        // CONSTRUCTOR
        // ====================================================================
        public SetUserPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            // Hide loader at start
            actLoading.IsRunning = false;
            actLoading.IsVisible = false;

            // Pre-populate if user already set (VB.NET Lines 17-20)
            if (!string.IsNullOrEmpty(AppGlobal.sEENo))
            {
                txtEENo.Text = AppGlobal.sEENo;
                lblName.Text = $"( {AppGlobal.sUserName} )";
            }

            // Update current user label
            UpdateCurrentUserLabel();

            // Subscribe to events
            txtEENo.TextChanged += TxtEENo_TextChanged;
            txtEENo.Completed += TxtEENo_Completed;
            txtEENo.Focused += TxtEENo_Focused;
            txtEENo.Unfocused += TxtEENo_Unfocused;

            btnApply.Clicked += BtnApply_Clicked;
            btnBack.Clicked += BtnBack_Clicked;
            btnApply.Focused += BtnButtons_Focused;
            btnBack.Focused += BtnButtons_Focused;
            btnApply.Unfocused += BtnButtons_Unfocused;
            btnBack.Unfocused += BtnButtons_Unfocused;
        }

        // ====================================================================
        // ✅ ADDED: PAGE APPEARING (Reference: AppGlobal patterns)
        // ====================================================================
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // ✅ DEBUG: Check server configuration
            Debug.WriteLine($"[SetUserPage] OnAppearing - sServer: '{AppGlobal.sServer}'");

            // ✅ FIXED: Check if user input should be readonly (VB.NET Line 22)
            bool isReadOnly = await AppGlobal._CheckOption_User();
            txtEENo.IsReadOnly = isReadOnly;

            // Set focus to entry when page appears
            if (!isReadOnly)
            {
                Dispatcher.Dispatch(() => txtEENo.Focus());
            }
        }

        // ====================================================================
        // UPDATE CURRENT USER LABEL (Reference: AppGlobal._SetUser pattern)
        // ====================================================================
        private void UpdateCurrentUserLabel()
        {
            lblUser.Text = string.IsNullOrEmpty(AppGlobal.sEENo)
                ? "User: (none)"
                : $"User: {AppGlobal.sUserName}";
        }

        // ====================================================================
        // TEXT CHANGED - ENABLE/DISABLE APPLY BUTTON
        // ====================================================================
        private void TxtEENo_TextChanged(object sender, TextChangedEventArgs e)
        {
            btnApply.IsEnabled = !string.IsNullOrWhiteSpace(e.NewTextValue);
        }

        // ====================================================================
        // ENTRY COMPLETED (ENTER KEY) - VB.NET txtEENo_KeyPress
        // ====================================================================
        private async void TxtEENo_Completed(object sender, EventArgs e)
        {
            // ✅ FIXED: VB calls GetUserName, then focuses button (doesn't auto-apply)
            await GetUserNameAsync();

            // VB Line 84: Just focus the Apply button, don't click it
            if (!string.IsNullOrEmpty(lblNameTag))
            {
                btnApply.Focus();
            }
        }

        // ====================================================================
        // FOCUS HANDLERS (VB.NET txtEENo_GotFocus / LostFocus)
        // ====================================================================
        private void TxtEENo_Focused(object sender, FocusEventArgs e)
        {
            txtEENo.BackgroundColor = Colors.PaleGreen;
        }

        private void TxtEENo_Unfocused(object sender, FocusEventArgs e)
        {
            txtEENo.BackgroundColor = Colors.WhiteSmoke;
        }

        // ====================================================================
        // BUTTON FOCUS HANDLERS (VB.NET btnButtons_GotFocus / LostFocus)
        // ====================================================================
        private void BtnButtons_Focused(object sender, FocusEventArgs e)
        {
            if (sender is Button btn) btn.TextColor = Colors.OrangeRed;
        }

        private void BtnButtons_Unfocused(object sender, FocusEventArgs e)
        {
            if (sender is Button btn) btn.TextColor = Colors.Black;
        }

        // ====================================================================
        // APPLY BUTTON CLICKED (VB.NET btnApply_Click)
        // ====================================================================
        private async void BtnApply_Clicked(object sender, EventArgs e)
        {
            // ✅ FIXED: Only fetch if not already looked up — prevents double
            //           ConnectAsync calls which caused multiple stacked alerts
            if (string.IsNullOrEmpty(lblNameTag))
                await GetUserNameAsync();

            if (string.IsNullOrEmpty(lblNameTag))
                return;

            try
            {
                AppGlobal.ID_User = _storedUserID;
                AppGlobal.sEENo = lblNameTag; // EE number — unchanged, still used by PDT app

                // Normalize to uppercase so SignalR group key matches desktop
                AppGlobal.sUserName = lblName.Text
                    .Replace("( ", "")
                    .Replace(" )", "")
                    .Trim()
                    .ToUpper();

                UpdateCurrentUserLabel();

                // Connect using full name as SignalR group key
                // EE number (sEENo) is untouched and still used everywhere else in the app
                await SignalRService.ConnectAsync(AppGlobal.sUserName);

                await DisplayAlert("Welcome!", $"User accepted: {AppGlobal.sUserName}", "OK");

                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to apply user.\n{ex.Message}", "OK");
            }
        }

        // ====================================================================
        // BACK BUTTON CLICKED (VB.NET btnBack_Click)
        // ====================================================================
        private async void BtnBack_Clicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        // ====================================================================
        // GET USER NAME (VB.NET GetUserName() Sub)
        // Reference: AppGlobal.LoadUserInfoAsync pattern
        // ====================================================================
        public async Task GetUserNameAsync()
        {
            if (string.IsNullOrWhiteSpace(txtEENo.Text))
                return;

            Debug.WriteLine($"[GetUserNameAsync] Starting - EENo: {txtEENo.Text}");
            Debug.WriteLine($"[GetUserNameAsync] Server configured: '{AppGlobal.sServer}'");

            SqlConnection? conn = null;

            try
            {
                // Show loading indicator
                actLoading.IsVisible = true;
                actLoading.IsRunning = true;

                // Get connection
                Debug.WriteLine("[GetUserNameAsync] Calling _SQL_Connect...");
                conn = await AppGlobal._SQL_Connect();
                Debug.WriteLine($"[GetUserNameAsync] Connection returned: {(conn != null ? "SUCCESS" : "NULL")}");

                if (conn == null)
                {
                    Debug.WriteLine("[GetUserNameAsync] ERROR: Connection is null");
                    await DisplayAlert("Error", "Cannot connect to database!\n\nPlease check server configuration.", "OK");
                    return;
                }

                // ✅ CRITICAL: Verify connection is actually open
                Debug.WriteLine($"[GetUserNameAsync] Connection State: {conn.State}");

                if (conn.State != System.Data.ConnectionState.Open)
                {
                    Debug.WriteLine($"[GetUserNameAsync] ERROR: Connection not open - State: {conn.State}");
                    await DisplayAlert("Error",
                        $"Connection not open.\n\nState: {conn.State}\nServer: {AppGlobal.sServer}",
                        "OK");
                    return;
                }

                Debug.WriteLine("[GetUserNameAsync] Connection verified as OPEN - Executing query...");

                // ✅ Now safe to execute query
                string sql = "SELECT ID, (LName + ', ' + FName + ' ' + MI) AS FullName, " +
                            "ID_SumHdr, isStocker, isChecker " +
                            "FROM tblUsers WHERE isActive=1 AND EENo=@EENo";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@EENo", txtEENo.Text.Trim());

                Debug.WriteLine($"[GetUserNameAsync] Executing query for EENo: {txtEENo.Text.Trim()}");
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    Debug.WriteLine("[GetUserNameAsync] User found!");

                    // ✅ Store ID temporarily
                    _storedUserID = Convert.ToInt32(reader["ID"]);

                    // Set display
                    lblName.Text = $"( {reader["FullName"].ToString()?.Trim()} )";
                    lblNameTag = txtEENo.Text.Trim();

                    // Set stocker/checker flags
                    AppGlobal.isStocker = Convert.ToInt32(reader["isStocker"]);
                    AppGlobal.isChecker = Convert.ToInt32(reader["isChecker"]);

                    Debug.WriteLine($"[GetUserNameAsync] User details - ID: {_storedUserID}, Name: {lblName.Text}");

                    // Focus on Apply button
                    Dispatcher.Dispatch(() => btnApply.Focus());
                }
                else
                {
                    Debug.WriteLine("[GetUserNameAsync] User NOT found in database");

                    // User not found
                    await DisplayAlert("Not Found!", "User ID not found!", "OK");
                    lblName.Text = "( Name )";
                    lblNameTag = "";
                    _storedUserID = 0;

                    Dispatcher.Dispatch(() =>
                    {
                        txtEENo.Focus();
                        txtEENo.CursorPosition = 0;
                        txtEENo.SelectionLength = txtEENo.Text?.Length ?? 0;
                    });
                }
            }
            catch (SqlException sqlEx)
            {
                Debug.WriteLine($"[GetUserNameAsync] SQL ERROR: {sqlEx.Message}");
                Debug.WriteLine($"[GetUserNameAsync] SQL ERROR Number: {sqlEx.Number}");
                Debug.WriteLine($"[GetUserNameAsync] SQL ERROR State: {sqlEx.State}");
                Debug.WriteLine($"[GetUserNameAsync] Stack Trace: {sqlEx.StackTrace}");

                await DisplayAlert("Database Error",
                    $"Failed to get user info.\n\n" +
                    $"SQL Error: {sqlEx.Message}\n" +
                    $"Error Number: {sqlEx.Number}",
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetUserNameAsync] GENERAL ERROR: {ex.GetType().Name}");
                Debug.WriteLine($"[GetUserNameAsync] Message: {ex.Message}");
                Debug.WriteLine($"[GetUserNameAsync] Stack Trace: {ex.StackTrace}");

                await DisplayAlert("Error",
                    $"Failed to get user info.\n\n" +
                    $"Error: {ex.Message}\n" +
                    $"Type: {ex.GetType().Name}",
                    "OK");
            }
            finally
            {
                // Hide loading indicator
                actLoading.IsRunning = false;
                actLoading.IsVisible = false;

                // ✅ CRITICAL: Properly close and dispose connection
                if (conn != null)
                {
                    Debug.WriteLine($"[GetUserNameAsync] Finally block - Connection State: {conn.State}");

                    if (conn.State == System.Data.ConnectionState.Open)
                    {
                        Debug.WriteLine("[GetUserNameAsync] Closing connection...");
                        await conn.CloseAsync();
                    }

                    Debug.WriteLine("[GetUserNameAsync] Disposing connection...");
                    conn.Dispose();
                    Debug.WriteLine("[GetUserNameAsync] Connection disposed");
                }
                else
                {
                    Debug.WriteLine("[GetUserNameAsync] Finally block - Connection was null");
                }
            }
        }

        // ====================================================================
        // ✅ ADDED: ESCAPE KEY HANDLER (VB.NET txtEENo_KeyPress Line 54)
        // ====================================================================
        public void OnEscapePressed()
        {
            _ = Shell.Current.GoToAsync("..");
        }

        // ====================================================================
        // ✅ ADDED: HARDWARE BACK BUTTON OVERRIDE
        // ====================================================================
        protected override bool OnBackButtonPressed()
        {
            _ = Shell.Current.GoToAsync("..");
            return true;
        }
    }
}