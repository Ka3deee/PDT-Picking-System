using Microsoft.Maui.Controls;
using PDTPickingSystem.Helpers;
using System;
using System.Threading.Tasks;

namespace PDTPickingSystem.Views
{
    public partial class SetUserPage : ContentPage
    {
        // Class-level variables
        private int iSKU;
        private string sSKU = "";
        private bool isKeyPress = false;

        // Replacement for lblName.Tag and txtEENo.Tag
        private string lblNameTag;
        private string txtEENoTag;

        // Store user roles
        private bool isStocker;
        private bool isChecker;

        public TaskCompletionSource<string> TaskCompletion { get; } = new TaskCompletionSource<string>();

        public SetUserPage()
        {
            InitializeComponent();

            // Hide loader at start
            actLoading.IsRunning = false;
            actLoading.IsVisible = false;

            // Update user label
            UpdateCurrentUserLabel();

            // Subscribe to events
            lblInputUserID.Loaded += LblInputUserID_Loaded;
            lblName.Loaded += LblName_Loaded;
            lblUser.Loaded += LblUser_Loaded;

            txtEENo.TextChanged += TxtEENo_TextChanged;
            txtEENo.Focused += TxtEENo_Focused;
            txtEENo.Unfocused += TxtEENo_Unfocused;

            btnApply.Clicked += BtnApply_Clicked;
            btnBack.Clicked += BtnBack_Clicked;
            btnApply.Focused += BtnButtons_Focused;
            btnBack.Focused += BtnButtons_Focused;
            btnApply.Unfocused += BtnButtons_Unfocused;
            btnBack.Unfocused += BtnButtons_Unfocused;
        }

        private void LblInputUserID_Loaded(object sender, EventArgs e) { }
        private void LblName_Loaded(object sender, EventArgs e) { }
        private void LblUser_Loaded(object sender, EventArgs e) { }

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

        private async void BtnApply_Clicked(object sender, EventArgs e)
        {
            await GetUserNameAsync();

            if (!string.IsNullOrEmpty(lblNameTag))
            {
                // Assign global variables
                AppGlobal.ID_User = Convert.ToInt32(txtEENoTag);
                AppGlobal.sEENo = lblNameTag;
                AppGlobal.sUserName = lblName.Text.Replace("( ", "").Replace(" )", "").Trim();

                // Mimic VB.NET _SetUser(lblUser)
                AppGlobal._SetUser(lblUser);

                await DisplayAlert("OK", "User accepted!", "OK");

                await Navigation.PopModalAsync();
            }

            isKeyPress = false;
        }

        private async void BtnBack_Clicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        public async Task GetUserNameAsync()
        {
            if (string.IsNullOrWhiteSpace(txtEENo.Text))
                return;

            var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
                return;

            try
            {
                using var sqlCmd = conn.CreateCommand();
                sqlCmd.CommandText = $"SELECT ID, (LName + ', ' + FName + ' ' + MI) AS FullName, ID_SumHdr, isStocker, isChecker " +
                                     $"FROM tblUsers WHERE isActive=1 AND EENo={txtEENo.Text}";

                using var reader = await sqlCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    txtEENoTag = reader["ID"].ToString().Trim();
                    lblName.Text = $"( {reader["FullName"].ToString().Trim()} )";
                    lblNameTag = txtEENoTag; // Use the correct ID

                    isStocker = Convert.ToBoolean(reader["isStocker"]);
                    isChecker = Convert.ToBoolean(reader["isChecker"]);

                    btnApply.Focus();
                }
                else
                {
                    await DisplayAlert("Not Found!", "User ID not found!", "OK");
                    lblName.Text = "( Name )";
                    lblNameTag = "";

                    txtEENo.Focus();
                    txtEENo.CursorPosition = 0;
                    txtEENo.SelectionLength = txtEENo.Text.Length;
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