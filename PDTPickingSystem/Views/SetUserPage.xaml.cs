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
        private string lblNameTag = "";
        private int _storedUserID = 0;

        // Constructor
        public SetUserPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            actLoading.IsRunning = false;
            actLoading.IsVisible = false;

            if (!string.IsNullOrEmpty(AppGlobal.sEENo))
            {
                txtEENo.Text = AppGlobal.sEENo;
                lblName.Text = $"( {AppGlobal.sUserName} )";
            }

            UpdateCurrentUserLabel();

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

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            Debug.WriteLine($"[SetUserPage] OnAppearing - sServer: '{AppGlobal.sServer}'");

            bool isReadOnly = await AppGlobal._CheckOption_User();
            txtEENo.IsReadOnly = isReadOnly;

            if (!isReadOnly)
            {
                Dispatcher.Dispatch(() => txtEENo.Focus());
            }
        }

        // Update Current User Label
        private void UpdateCurrentUserLabel()
        {
            lblUser.Text = string.IsNullOrEmpty(AppGlobal.sEENo)
                ? "User: (none)"
                : $"User: {AppGlobal.sUserName}";
        }
        private void TxtEENo_TextChanged(object sender, TextChangedEventArgs e)
        {
            btnApply.IsEnabled = !string.IsNullOrWhiteSpace(e.NewTextValue);
        }
        private async void TxtEENo_Completed(object sender, EventArgs e)
        {
            await GetUserNameAsync();

            if (!string.IsNullOrEmpty(lblNameTag))
            {
                btnApply.Focus();
            }
        }
        private void TxtEENo_Focused(object sender, FocusEventArgs e)
        {
            txtEENo.BackgroundColor = Colors.PaleGreen;
        }

        private void TxtEENo_Unfocused(object sender, FocusEventArgs e)
        {
            txtEENo.BackgroundColor = Colors.WhiteSmoke;
        }

        private void BtnButtons_Focused(object sender, FocusEventArgs e)
        {
            if (sender is Button btn) btn.TextColor = Colors.OrangeRed;
        }

        private void BtnButtons_Unfocused(object sender, FocusEventArgs e)
        {
            if (sender is Button btn) btn.TextColor = Colors.Black;
        }

        // Apply Button
        private async void BtnApply_Clicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(lblNameTag))
                await GetUserNameAsync();

            if (string.IsNullOrEmpty(lblNameTag))
                return;

            try
            {
                AppGlobal.ID_User = _storedUserID;
                AppGlobal.sEENo = lblNameTag;

                AppGlobal.sUserName = lblName.Text
                    .Replace("( ", "")
                    .Replace(" )", "")
                    .Trim()
                    .ToUpper();

                UpdateCurrentUserLabel();

                await SignalRService.ConnectAsync(AppGlobal.sUserName);

                await DisplayAlert("Welcome!", $"User accepted: {AppGlobal.sUserName}", "OK");

                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to apply user.\n{ex.Message}", "OK");
            }
        }

        // Back Button
        private async void BtnBack_Clicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        // Get User Name
        public async Task GetUserNameAsync()
        {
            if (string.IsNullOrWhiteSpace(txtEENo.Text))
                return;

            Debug.WriteLine($"[GetUserNameAsync] Starting - EENo: {txtEENo.Text}");
            Debug.WriteLine($"[GetUserNameAsync] Server configured: '{AppGlobal.sServer}'");

            SqlConnection? conn = null;

            try
            {
                actLoading.IsVisible = true;
                actLoading.IsRunning = true;

                Debug.WriteLine("[GetUserNameAsync] Calling _SQL_Connect...");
                conn = await AppGlobal._SQL_Connect();
                Debug.WriteLine($"[GetUserNameAsync] Connection returned: {(conn != null ? "SUCCESS" : "NULL")}");

                if (conn == null)
                {
                    Debug.WriteLine("[GetUserNameAsync] ERROR: Connection is null");
                    await DisplayAlert("Error", "Cannot connect to database!\n\nPlease check server configuration.", "OK");
                    return;
                }
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

                    _storedUserID = Convert.ToInt32(reader["ID"]);

                    lblName.Text = $"( {reader["FullName"].ToString()?.Trim()} )";
                    lblNameTag = txtEENo.Text.Trim();

                    AppGlobal.isStocker = Convert.ToInt32(reader["isStocker"]);
                    AppGlobal.isChecker = Convert.ToInt32(reader["isChecker"]);

                    Debug.WriteLine($"[GetUserNameAsync] User details - ID: {_storedUserID}, Name: {lblName.Text}");

                    Dispatcher.Dispatch(() => btnApply.Focus());
                }
                else
                {
                    Debug.WriteLine("[GetUserNameAsync] User NOT found in database");
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
                actLoading.IsRunning = false;
                actLoading.IsVisible = false;

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
        public void OnEscapePressed()
        {
            _ = Shell.Current.GoToAsync("..");
        }
        protected override bool OnBackButtonPressed()
        {
            _ = Shell.Current.GoToAsync("..");
            return true;
        }
    }
}