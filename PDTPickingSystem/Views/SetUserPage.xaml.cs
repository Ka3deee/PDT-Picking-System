using Microsoft.Maui.Controls;
using Microsoft.Data.SqlClient;
using PDTPickingSystem.Helpers;
using System;
using System.Threading.Tasks;

namespace PDTPickingSystem.Views
{
    public partial class SetUserPage : ContentPage
    {
        // Class-level variables
        private string lblNameTag = "";

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

            // Check if user input should be readonly (VB.NET Line 22)
            // txtEENo.IsReadOnly = AppGlobal._CheckOption_User();
        }

        // ====================================================================
        // UPDATE CURRENT USER LABEL
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
        // ENTRY COMPLETED (ENTER KEY PRESSED) - VB.NET txtEENo_KeyPress
        // ====================================================================
        private async void TxtEENo_Completed(object sender, EventArgs e)
        {
            await GetUserNameAsync();

            // If user found, trigger apply (VB.NET behavior)
            if (!string.IsNullOrEmpty(lblNameTag))
            {
                BtnApply_Clicked(btnApply, e);
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
            // Only proceed if user was found (lblName.Tag <> "")
            if (string.IsNullOrEmpty(lblNameTag))
            {
                await DisplayAlert("Error", "Please enter a valid User ID first.", "OK");
                return;
            }

            try
            {
                // Set global variables (VB.NET Lines 62-64)
                AppGlobal.sEENo = lblNameTag;
                AppGlobal.sUserName = lblName.Text.Replace("( ", "").Replace(" )", "").Trim();

                UpdateCurrentUserLabel();

                // Show success (VB.NET Line 66)
                await DisplayAlert("Welcome!", "User accepted!", "OK");

                // Go back (VB.NET Line 67: Me.Close())
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
            try
            {
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to go back.\n{ex.Message}", "OK");
            }
        }

        // ====================================================================
        // GET USER NAME (VB.NET GetUserName() Sub)
        // ====================================================================
        public async Task GetUserNameAsync()
        {
            if (string.IsNullOrWhiteSpace(txtEENo.Text))
                return;

            var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
                return;

            try
            {
                // ✅ FIXED: Parameterized query to prevent SQL injection
                using var sqlCmd = new SqlCommand(
                    "SELECT ID, (LName + ', ' + FName + ' ' + MI) AS FullName, " +
                    "ID_SumHdr, isStocker, isChecker " +
                    "FROM tblUsers WHERE isActive=1 AND EENo=@EENo",
                    conn
                );

                sqlCmd.Parameters.Add("@EENo", System.Data.SqlDbType.VarChar, 50).Value = txtEENo.Text.Trim();

                using var reader = await sqlCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    // VB.NET Lines 79-83
                    AppGlobal.ID_User = Convert.ToInt32(reader["ID"]);
                    lblName.Text = $"( {reader["FullName"].ToString().Trim()} )";
                    lblNameTag = txtEENo.Text.Trim();  // VB.NET: lblName.Tag = txtEENo.Text

                    // ✅ CORRECT (writable integer fields):
                    AppGlobal.isStocker = Convert.ToInt32(reader["isStocker"]);
                    AppGlobal.isChecker = Convert.ToInt32(reader["isChecker"]);

                    // VB.NET Line 84: Focus on Apply button
                    btnApply.Focus();
                }
                else
                {
                    // VB.NET Lines 86-90
                    await DisplayAlert("Not Found!", "User ID not found!", "OK");
                    lblName.Text = "( Name )";
                    lblNameTag = "";

                    txtEENo.Focus();
                    txtEENo.CursorPosition = 0;
                    txtEENo.SelectionLength = txtEENo.Text?.Length ?? 0;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to get user info.\n{ex.Message}", "OK");
            }
            finally
            {
                if (conn?.State == System.Data.ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }
    }
}