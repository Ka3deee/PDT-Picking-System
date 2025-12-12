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
        // --- Store equivalents of VB Tag properties ---
        private string _currentRefNumber;
        private string _lblNameTag;

        public SetRefPage()
        {
            InitializeComponent();

            lblRefDisplay.Text = string.IsNullOrEmpty(AppGlobal.PickNo)
                ? "(Reference #)"
                : $"Reference #: {AppGlobal.PickNo}";

            // Set the signal image (equivalent to pbfrmSignal.Image = frmMenu.pbSignal.Image)
            if (AppGlobal.MenuSignalImage != null)
            {
                pbfrmSignal.Source = AppGlobal.MenuSignalImage;
            }

            // Optional: Attach TextChanged for numeric-only validation
            txtRefNo.TextChanged += TxtRefNo_TextChanged;
        }

        // ------------------------------------
        // SAVE BUTTON CLICK (equivalent to btnApply_Click)
        // ------------------------------------
        private async void btnApply_Clicked(object sender, EventArgs e)
        {
            string refNumber = txtRefNo.Text?.Trim();
            if (string.IsNullOrWhiteSpace(refNumber))
                return;

            if (!long.TryParse(refNumber, out long refLong))
            {
                await DisplayAlert("Error!", "Reference must be numeric.", "OK");
                return;
            }

            btnApply.IsEnabled = false;

            bool success = await GetRefNumberAsync(refNumber, refLong);

            if (success)
            {
                AppGlobal.PickNo = _currentRefNumber;
                lblRefDisplay.Text = $"Reference #: {_currentRefNumber}";
                await DisplayAlert("OK", "Ref Reference # Set!", "OK");
                await Shell.Current.GoToAsync(".."); // Close page
            }
            else
            {
                await DisplayAlert("Not Found!", "Picking Reference Number not found!", "OK");
                MoveCursorToEntry();
            }

            btnApply.IsEnabled = true;
        }

        // ------------------------------------
        // MAIN PROCESS (Equivalent to VB GetRefNumber)
        // ------------------------------------
        private async Task<bool> GetRefNumberAsync(string refNumber, long refLong)
        {
            try
            {
                bool connected = await AppGlobal.ConnectSqlAsync();
                if (!connected) return false;

                bool updateUser = false;

                // --- Check if reference exists ---
                const string checkQuery = "SELECT SetRef FROM tblOptions WHERE SetRef = @Ref";
                using (var cmd = new SqlCommand(checkQuery, AppGlobal.SqlCon))
                {
                    cmd.Parameters.Add("@Ref", System.Data.SqlDbType.BigInt).Value = refLong;
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        updateUser = true;
                        _currentRefNumber = result.ToString();
                        _lblNameTag = refNumber;
                        btnApply.Focus();
                    }
                    else
                    {
                        updateUser = false;
                        MoveCursorToEntry();
                        return false;
                    }
                }

                // --- Update tblUsers.PickRef ---
                if (updateUser)
                {
                    const string updateQuery = "UPDATE tblUsers SET PickRef = @Ref WHERE ID = @UserID";
                    using (var cmd = new SqlCommand(updateQuery, AppGlobal.SqlCon))
                    {
                        cmd.Parameters.Add("@Ref", System.Data.SqlDbType.BigInt).Value = refLong;
                        cmd.Parameters.Add("@UserID", System.Data.SqlDbType.Int).Value = Convert.ToInt32(AppGlobal.ID_User);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                return true;
            }
            catch
            {
                return false;
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
                txtRefNo.Focus();
                txtRefNo.CursorPosition = 0;
                txtRefNo.SelectionLength = txtRefNo.Text?.Length ?? 0;
            });
        }

        // ------------------------------------
        // CANCEL BUTTON CLICK (Back)
        // ------------------------------------
        public async void btnBack_Clicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        // ------------------------------------
        // ENTER KEY PRESSED ON ENTRY
        // ------------------------------------
        private void TxtRefNo_Completed(object sender, EventArgs e)
        {
            btnApply_Clicked(btnApply, e);
        }

        // ------------------------------------
        // PARENT CHANGED LOGIC (from VB Label2_ParentChanged)
        // ------------------------------------
        protected override void OnParentSet()
        {
            base.OnParentSet();
            if (lblInputPickRef != null)
            {
                // Add logic when lblInputPickRef is added to a parent
            }
        }

        // ------------------------------------
        // EXTERNAL CALL FROM MAINACTIVITY (F1 = Escape)
        // ------------------------------------
        public void OnF1Pressed()
        {
            // Treat F1 as Escape
            btnBack_Clicked(null, null);
        }

        // ------------------------------------
        // TEXTCHANGED HANDLER (optional numeric validation)
        // ------------------------------------
        private void TxtRefNo_TextChanged(object sender, TextChangedEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;

            // Allow only numeric input
            if (!string.IsNullOrEmpty(e.NewTextValue))
            {
                string numeric = "";
                foreach (char c in e.NewTextValue)
                {
                    if (char.IsDigit(c)) numeric += c;
                }
                if (numeric != e.NewTextValue)
                {
                    entry.Text = numeric;
                }
            }
        }
    }
}