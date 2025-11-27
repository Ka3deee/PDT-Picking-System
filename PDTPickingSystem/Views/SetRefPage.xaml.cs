using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Data.SqlClient;
using PDTPickingSystem.Helpers;
using System;
using System.Threading.Tasks;

namespace PDTPickingSystem.Views
{
    public partial class SetRefPage : ContentPage
    {
        public SetRefPage()
        {
            InitializeComponent();

            lblRefDisplay.Text = string.IsNullOrEmpty(AppGlobal.PickNo)
                ? "(Reference #)"
                : $"Reference #: {AppGlobal.PickNo}";
        }

        // ------------------------------------
        // SAVE BUTTON CLICK
        // ------------------------------------
        private async void OnSaveClicked(object sender, EventArgs e)
        {
            string refNumber = txtRefNumber.Text?.Trim();

            if (string.IsNullOrWhiteSpace(refNumber))
            {
                await DisplayAlert("Error!", "Please enter a picking reference number.", "OK");
                return;
            }

            if (!long.TryParse(refNumber, out long refLong))
            {
                await DisplayAlert("Error!", "Reference must be numeric.", "OK");
                return;
            }

            btnApply.IsEnabled = false;
            await ProcessReferenceAsync(refNumber, refLong);
            btnApply.IsEnabled = true;
        }

        // ------------------------------------
        // MAIN PROCESS (Matches Old VB Logic)
        // ------------------------------------
        private async Task ProcessReferenceAsync(string refNumber, long refLong)
        {
            try
            {
                bool connected = await AppGlobal.ConnectSqlAsync();
                if (!connected)
                {
                    await DisplayAlert("Connection Error", "Cannot connect to SQL Server.", "OK");
                    MoveCursorToEntry();
                    return;
                }

                // ------------------------------------
                // 1) MATCH OLD PDT LOGIC:
                //    SELECT * FROM tblOptions WHERE SetRef = <ref>
                // ------------------------------------
                const string checkQuery =
                    "SELECT SetRef FROM tblOptions WHERE SetRef = @Ref";

                bool found = false;

                using (var cmd = new SqlCommand(checkQuery, AppGlobal.SqlCon))
                {
                    cmd.Parameters.Add("@Ref", System.Data.SqlDbType.BigInt).Value = refLong;
                    var result = await cmd.ExecuteScalarAsync();
                    found = (result != null && result != DBNull.Value);
                }

                if (!found)
                {
                    await DisplayAlert("Not Found!",
                        "Picking Reference Number not found!\n",
                        "OK");

                    MoveCursorToEntry();
                    return;
                }

                // ------------------------------------
                // 2) UPDATE tblUsers.PickRef
                // ------------------------------------
                const string updateQuery =
                    "UPDATE tblUsers SET PickRef = @Ref WHERE ID = @UserID";

                using (var cmd = new SqlCommand(updateQuery, AppGlobal.SqlCon))
                {
                    cmd.Parameters.Add("@Ref", System.Data.SqlDbType.BigInt).Value = refLong;
                    cmd.Parameters.Add("@UserID", System.Data.SqlDbType.Int)
                        .Value = Convert.ToInt32(AppGlobal.ID_User);

                    await cmd.ExecuteNonQueryAsync();
                }

                // ------------------------------------
                // 3) UPDATE GLOBAL + UI
                // ------------------------------------
                AppGlobal.PickNo = refNumber;
                lblRefDisplay.Text = $"Reference #: {refNumber}";

                await DisplayAlert("Success!",
                    $"Reference '{refNumber}' set successfully!",
                    "OK");

                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error!", ex.Message, "OK");
            }
            finally
            {
                if (AppGlobal.SqlCon?.State == System.Data.ConnectionState.Open)
                    await AppGlobal.SqlCon.CloseAsync();
            }
        }

        // ------------------------------------
        // FOCUS ENTRY AGAIN AFTER ERROR
        // ------------------------------------
        private void MoveCursorToEntry()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                txtRefNumber.Focus();
                txtRefNumber.CursorPosition = 0;
                txtRefNumber.SelectionLength = txtRefNumber.Text?.Length ?? 0;
            });
        }

        // ------------------------------------
        // CANCEL
        // ------------------------------------
        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        // ------------------------------------
        // ENTER KEY PRESSED
        // ------------------------------------
        private void TxtRefNumber_Completed(object sender, EventArgs e)
        {
            OnSaveClicked(btnApply, e);
        }
    }
}