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
        private string _currentRefNumber;
        private string _lblNameTag;

        public SetRefPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            lblRefDisplay.Text = string.IsNullOrEmpty(AppGlobal.pPickNo)
                ? "(Reference #)"
                : $"Reference #: {AppGlobal.pPickNo}";
            txtRefNo.TextChanged += TxtRefNo_TextChanged;
        }

        // Apply Button
        private async void btnApply_Clicked(object sender, EventArgs e)
        {
            string refNumber = txtRefNo.Text?.Trim();
            System.Diagnostics.Debug.WriteLine($"========================================");
            System.Diagnostics.Debug.WriteLine($"🔍 btnApply_Clicked START");
            System.Diagnostics.Debug.WriteLine($"📝 User Input: '{refNumber}'");
            System.Diagnostics.Debug.WriteLine($"📝 User ID: {AppGlobal.ID_User}");
            System.Diagnostics.Debug.WriteLine($"📝 Server: {AppGlobal.sServer}");

            if (string.IsNullOrWhiteSpace(refNumber))
            {
                System.Diagnostics.Debug.WriteLine("❌ Input is empty");
                return;
            }

            if (!long.TryParse(refNumber, out long refLong))
            {
                System.Diagnostics.Debug.WriteLine($"❌ Cannot parse '{refNumber}' as number");
                await DisplayAlert("Error!", "Reference must be numeric.", "OK");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"✅ Parsed as: {refLong}");
            btnApply.IsEnabled = false;

            bool success = await GetRefNumberAsync(refNumber, refLong);

            System.Diagnostics.Debug.WriteLine($"📊 GetRefNumberAsync returned: {success}");
            System.Diagnostics.Debug.WriteLine($"📊 _currentRefNumber: '{_currentRefNumber}'");
            System.Diagnostics.Debug.WriteLine($"📊 _lblNameTag: '{_lblNameTag}'");

            if (success && !string.IsNullOrEmpty(_currentRefNumber))
            {
                System.Diagnostics.Debug.WriteLine("✅ SUCCESS PATH");
                AppGlobal.pPickNo = _currentRefNumber;
                lblRefDisplay.Text = $"Reference #: {_currentRefNumber}";
                await DisplayAlert("Success!", "Picking Reference No. Set!", "OK");
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("❌ FAILURE PATH");
                await DisplayAlert("Not Found!", "Picking Reference Number not found!", "OK");
                MoveCursorToEntry();
            }

            btnApply.IsEnabled = true;
            System.Diagnostics.Debug.WriteLine($"========================================");
        }

        // Get Reference Number
        private async Task<bool> GetRefNumberAsync(string refNumber, long refLong)
        {
            System.Diagnostics.Debug.WriteLine($"🔍 GetRefNumberAsync START - refNumber: '{refNumber}', refLong: {refLong}");

            _currentRefNumber = "";
            _lblNameTag = "";

            SqlConnection conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                System.Diagnostics.Debug.WriteLine("❌ Connection FAILED");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"✅ Connection SUCCESS");

            try
            {
                bool updateUser = false;

                const string checkQuery = "SELECT * FROM tblOptions WHERE SetRef = @Ref";

                using (var cmd = new SqlCommand(checkQuery, conn))
                {
                    cmd.Parameters.Add("@Ref", System.Data.SqlDbType.BigInt).Value = refLong;

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            updateUser = true;
                            _currentRefNumber = reader["SetRef"].ToString().Trim();
                            _lblNameTag = refNumber;
                            System.Diagnostics.Debug.WriteLine($"✅ Found! SetRef={_currentRefNumber}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("❌ No record found");
                            return false;
                        }
                    }
                }

                if (updateUser)
                {
                    const string updateQuery = "UPDATE tblUsers SET PickRef = @Ref WHERE ID = @UserID";
                    using (var cmd = new SqlCommand(updateQuery, conn))
                    {
                        cmd.Parameters.Add("@Ref", System.Data.SqlDbType.BigInt).Value = refLong;
                        cmd.Parameters.Add("@UserID", System.Data.SqlDbType.Int).Value = AppGlobal.ID_User;
                        await cmd.ExecuteNonQueryAsync();
                        System.Diagnostics.Debug.WriteLine($"✅ User updated");
                    }
                }

                return updateUser;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Exception: {ex.Message}");
                _currentRefNumber = "";
                return false;
            }
            finally
            {
                if (conn?.State == System.Data.ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }
        private void MoveCursorToEntry()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                txtRefNo.Focus();
                txtRefNo.CursorPosition = 0;
                txtRefNo.SelectionLength = txtRefNo.Text?.Length ?? 0;
            });
        }

        // Cancel Button
        public async void btnBack_Clicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private void TxtRefNo_Completed(object sender, EventArgs e)
        {
            btnApply_Clicked(btnApply, e);
        }

        protected override void OnParentSet()
        {
            base.OnParentSet();
            if (lblInputPickRef != null)
            {
            }
        }
        private void TxtRefNo_TextChanged(object sender, TextChangedEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;

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