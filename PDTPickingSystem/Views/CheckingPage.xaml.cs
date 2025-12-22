using Android.Media.TV;
using Android.OS;
using Microsoft.Maui.Controls;
using PDTPickingSystem.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Collections.ObjectModel;
using System.Timers;
using Microsoft.Maui.Dispatching; // For MainThread

namespace PDTPickingSystem.Views
{
    public partial class CheckingPage : ContentPage
    {
        public ObservableCollection<SKUItem> SKUList { get; set; } = new ObservableCollection<SKUItem>();


        public CheckingPage(MainMenuPage mainMenu)
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            // ===== Initialize tmrRequest =====
            tmrRequest = new System.Timers.Timer(1000); // 1-second interval
            tmrRequest.Elapsed += async (s, e) =>
            {
                // Run the tick handler on the main UI thread
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await TmrRequest_Tick(s, e);
                });
            };
            tmrRequest.AutoReset = true;  // Repeat every interval
            tmrRequest.Enabled = false;   // Start disabled; enable when needed

            _mainMenu = mainMenu; // mainMenu reference

            _focusedEntry = null;
            iSKU = 0;
            sSKU = -1;
            ID_Stocker = 0;
            isStarted = false;
            chkqty = 0;
            pickqty = 0;
            Gsku = string.Empty;
            lvCnt = 1;
            skuArr = new List<int>();
            txtEachVal = string.Empty;
            scanCount = 0;

            txtBarcode.Completed += Entry_Completed;
            txtCase.Completed += Entry_Completed;
            txtEach.Completed += Entry_Completed;
            txtStocker.Completed += txtStocker_Completed;

            txtBarcode.TextChanged += Entry_TextChanged;
            txtCase.TextChanged += Entry_TextChanged;
            txtEach.TextChanged += Entry_TextChanged;
            txtStocker.TextChanged += txtStocker_TextChanged;

            txtBarcode.Unfocused += TxtEntry_Unfocused;
            txtStocker.Unfocused += TxtEntry_Unfocused;

            txtBarcode.Focused += TxtEntry_Focused;
            txtStocker.Focused += TxtEntry_Focused;
            txtCase.Focused += TxtEntry_Focused;
            txtEach.Focused += TxtEntry_Focused;
            txtBum.Focused += TxtEntry_Focused;
            txtDone.Focused += TxtEntry_Focused;
            txtSKU.Focused += TxtEntry_Focused;

            txtCase.Unfocused += TxtCaseEach_Unfocused;
            txtEach.Unfocused += TxtCaseEach_Unfocused;

            txtBarcode.Focused += TxtBarcodeQtyFocus_Focused;
            txtStocker.Focused += TxtBarcodeQtyFocus_Focused;
            txtEach.Focused += TxtBarcodeQtyFocus_Focused;
            txtCase.Focused += TxtBarcodeQtyFocus_Focused;

            btnAccept.Clicked += BtnAccept_Clicked;

            txtpSKU.Focused += TxtOther_Focused;
            txtpDescr.Focused += TxtOther_Focused;
            txtpSlot.Focused += TxtOther_Focused;
            txtpEach.Focused += TxtOther_Focused;
            txtpCase.Focused += TxtOther_Focused;
            txtSKU.Focused += TxtOther_Focused;
            txtDone.Focused += TxtOther_Focused;
            txtDeptStore.Focused += TxtOther_Focused;



            lvSKU.ItemsSource = SKUList; // Bind CollectionView
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            DisableKeyboard();

            _SetUser(lblTrf);

            txtBarcode.Text = string.Empty;
            txtEachVal = string.Empty;
            txtBarcode.Focus();

            pnlItems.IsVisible = true;
            pnlitems2.IsVisible = true;
            pbReq.IsVisible = true;

            isBarcode = true;
            btnFinished.IsVisible = false;

            _ = _GetSetPickNo();

            scanCount = 0;
        }

        private async void Entry_Completed(object sender, EventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;

            pbScanned.IsVisible = false;
            txtDesc.Text = string.Empty;

            if (entry == txtBarcode)
            {
                txtBarcode.Text = double.TryParse(txtBarcode.Text.Trim(), out double val) ? val.ToString() : txtBarcode.Text;

                if (!_isUPCFound(txtBarcode.Text.Trim()))
                {
                    await DisplayAlert("Mismatch!", "Wrong scanned item!", "OK");
                    _ClearScan();
                }
            }
            else if (entry == txtCase)
            {
                txtEach.Focus();
                txtEach.CursorPosition = 0;
                txtEach.SelectionLength = txtEach.Text?.Length ?? 0;
            }
            else if (entry == txtEach)
            {
                btnAccept_Clicked(null, null);
            }
        }

        private void Entry_TextChanged(object sender, TextChangedEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;

            if (!_isAllowedNum(e.NewTextValue))
            {
                entry.Text = e.OldTextValue;
            }
        }

        private async Task _GetSetPickNo()
        {
            btnFinished.IsVisible = false;
            pbReq.IsVisible = true;

            string sUserPNo = "";

            // Get a SQL connection
            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                await DisplayAlert("No Connection!", "Cannot connect to server! Please retry or check settings...", "OK");
                await Navigation.PopAsync();
                return;
            }

            // Query user info
            string cmdText = @"
                SELECT a.ID_SumHdr, a.PickRef, b.user_id, a.isChecker
                FROM tblUsers a
                LEFT JOIN tblChkrDept b ON a.id = b.user_id
                WHERE a.ID=@UserID";

            using var sqlCmd = new SqlCommand(cmdText, conn);
            sqlCmd.Parameters.AddWithValue("@UserID", AppGlobal.ID_User);

            using var reader = await sqlCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                AppGlobal.ID_SumHdr = reader["ID_SumHdr"] != DBNull.Value
                    ? Convert.ToInt32(reader["ID_SumHdr"])
                    : 0;

                if (reader["user_id"] == DBNull.Value)
                {
                    await DisplayAlert("System Says", "No dept setup for checker!", "OK");
                    await Navigation.PopAsync();
                    return;
                }

                if (Convert.ToInt32(reader["isChecker"]) != 1)
                {
                    await DisplayAlert("System Says", "Only Checker Can access this Menu!", "OK");
                    await Navigation.PopAsync();
                    return;
                }

                if (reader["PickRef"] != DBNull.Value && Convert.ToInt32(reader["PickRef"]) != 0)
                    sUserPNo = reader["PickRef"].ToString().Trim();
            }

            if (AppGlobal.ID_SumHdr != 0 && sUserPNo == AppGlobal.pPickNo)
            {
                txtSKU.Text = string.Empty;
                await _AddSKUtoList();
                return;
            }
            else
            {
                bool answer = await DisplayAlert("Requesting...", "Request from server?", "Yes", "No");
                if (answer)
                {
                    string updateCmd = @"
                UPDATE tblUsers 
                SET isRequest=1, isSummary=@Summary, PickRef=@PickNo 
                WHERE ID=@UserID";

                    using var updateSqlCmd = new SqlCommand(updateCmd, conn);
                    updateSqlCmd.Parameters.AddWithValue("@Summary", isSummary);
                    updateSqlCmd.Parameters.AddWithValue("@PickNo", AppGlobal.pPickNo);
                    updateSqlCmd.Parameters.AddWithValue("@UserID", AppGlobal.ID_User);

                    await updateSqlCmd.ExecuteNonQueryAsync();

                    tmrRequest.Start();
                }
                else
                {
                    await Navigation.PopAsync();
                }
            }
        }

        // ====== _AddSKUtoList converted to MAUI ======
        private async Task _AddSKUtoList()
        {
            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null) return;

            // Query header info
            string cmdStr = $"SELECT * FROM tbl{AppGlobal.pPickNo}PickHdr WHERE ID=@SumHdr";
            using var sqlCmd = new SqlCommand(cmdStr, conn);
            sqlCmd.Parameters.AddWithValue("@SumHdr", AppGlobal.ID_SumHdr);

            using var reader = await sqlCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                lblDeptStore.Text = isSummary == 1 ? "Department:" : "Store No:";
                txtDeptStore.Text = isSummary == 1
                    ? await _GetDeptNameAsync(reader["iDept"])
                    : await _GetStoreNo();
                lblPicker.Text = " Picker : " + await AppGlobal._GetUserName(reader["User_ID"].ToString());

                if (reader["TimeEnd"].ToString().Trim() == "0" || reader["DateDone"].ToString().Trim() == "")
                {
                    await DisplayAlert("System Says", "Not yet Picked!", "OK");
                    await Navigation.PopAsync();
                    return;
                }

                isStarted = reader["chkStart"].ToString().Trim() != "0";
            }
            reader.Close();

            // Load PickQty data
            var dsData = new System.Data.DataSet();
            string cmdData = $"SELECT a.*, b.toLoc, b.Tranno FROM tbl{AppGlobal.pPickNo}PickQty a " +
                             $"LEFT JOIN (SELECT DISTINCT id_sumhdr, toLoc, Tranno FROM tbl{AppGlobal.pPickNo}PickDtl) b " +
                             $"ON a.id_sumhdr = b.id_sumhdr " +
                             $"WHERE a.ID_SumHdr=@SumHdr ORDER BY a.slot, a.sku";

            await AppGlobal._WorkQueryAsync(cmdData, dsData, "DATA");

            SKUList.Clear();
            lvCnt = 0;

            foreach (System.Data.DataRow row in dsData.Tables[0].Rows)
            {
                _loadlvSKU2(
                    row["ID"].ToString(),
                    row["BUM"].ToString(),
                    row["Slot"].ToString(),
                    row["SKU"].ToString(),
                    row["Descr"].ToString(),
                    row["Qty"].ToString(),
                    row["isPicked"].ToString(),
                    row["PickQty"].ToString(),
                    row["UPC"].ToString(),
                    row["isChecked"].ToString(),
                    row["chkQty"].ToString()
                );
            }

            var firstRow = dsData.Tables[0].Rows[0];
            lblLoc.Text = "Location: " + firstRow["toLoc"].ToString();
            lblTrf.Text = "Transfer # : " + firstRow["Tranno"].ToString();

            dsData.Tables.Clear();
            sSKU = -1;
            _CountPicked();
        }
        private async Task<string> _GetStoreNo()
        {
            string storeNo = "";

            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
                return storeNo;

            string query = $"SELECT ToLoc FROM tbl{AppGlobal.pPickNo}PickHdr WHERE ID=@SumHdr";
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@SumHdr", AppGlobal.ID_SumHdr);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                storeNo = reader["ToLoc"].ToString().Trim();

            return storeNo;
        }

        // ====== _loadlvSKU2 for MAUI CollectionView ======
        private void _loadlvSKU2(string id, string bum, string slot, string sku, string descr, string qty,
                                 string isPck, string pckqty, string upc, string isChecked, string chkQty)
        {
            // Check duplicate SKU: sum checked qty instead of adding new item
            var existingItem = SKUList.FirstOrDefault(x => x.SKU == sku && x.Slot == slot);
            if (existingItem != null)
            {
                if (int.TryParse(existingItem.ChkQty, out int existingChk) && int.TryParse(chkQty, out int addChk))
                {
                    existingItem.ChkQty = (existingChk + addChk).ToString();
                }
                return; // exit after updating
            }

            var item = new SKUItem
            {
                ID = id,
                BUM = bum,
                Slot = slot,
                SKU = sku,
                Descr = descr,
                Qty = qty,
                PickQty = isPck.Trim() == "0" ? "" : pckqty,
                UPC = upc.Trim(),
                ChkQty = isChecked.Trim() == "0" ? "" : chkQty
            };

            SKUList.Add(item);
        }
        private void _CountPicked()
        {
            // Count how many items have a non-empty ChkQty
            int iPicked = SKUList.Count(item => !string.IsNullOrWhiteSpace(item.ChkQty));

            // Update txtDone text
            txtDone.Text = $"{iPicked}/{SKUList.Count}";

            // Enable/Show btnFinished based on count
            // In your VB code, it always shows btnFinished
            btnFinished.IsVisible = true;

            // Hide pbReq
            pbReq.IsVisible = false;
        }
        private async Task TmrRequest_Tick(object sender, EventArgs e)
        {
            iRetry++; // increment retry counter

            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                await DisplayAlert("No Connection!", "Cannot connect to server!", "OK");
                return;
            }

            try
            {
                string cmdText = "SELECT * FROM tblUsers WHERE ID=@UserID AND ID_SumHdr <> 0";
                using var sqlCmd = new SqlCommand(cmdText, conn);
                sqlCmd.Parameters.AddWithValue("@UserID", AppGlobal.ID_User);

                using var reader = await sqlCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    tmrRequest.Stop();
                    AppGlobal.ID_SumHdr = reader["ID_SumHdr"] != DBNull.Value
                        ? Convert.ToInt32(reader["ID_SumHdr"])
                        : 0;

                    reader.Close();

                    await _AddSKUtoList(); // Populate SKU list
                    return;
                }

                reader.Close();

                // Retry logic after 5 attempts
                if (iRetry >= 5)
                {
                    iRetry = 0;
                    string resetCmd = "UPDATE tblUsers SET isRequest=0, isSummary=0 WHERE ID=@UserID";
                    using var updateCmd = new SqlCommand(resetCmd, conn);
                    updateCmd.Parameters.AddWithValue("@UserID", AppGlobal.ID_User);
                    await updateCmd.ExecuteNonQueryAsync();

                    await DisplayAlert("Unable to request!", "No picking no. available!", "OK");
                    await Navigation.PopAsync(); // Close page
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error!", ex.Message, "OK");
            }
        }
        private void _ClearScan(bool bWithBarcode = true)
        {
            if (bWithBarcode)
                txtBarcode.Text = string.Empty;

            txtSKU.Text = string.Empty;
            txtEach.Text = "0";
            txtCase.Text = "0";

            // Reset selection (CursorPosition and SelectionLength)
            txtCase.CursorPosition = 0;
            txtCase.SelectionLength = 0;

            txtEach.CursorPosition = 0;
            txtEach.SelectionLength = 0;

            // Focus the barcode Entry
            txtBarcode.Focus();
            txtBarcode.CursorPosition = 0;
            txtBarcode.SelectionLength = txtBarcode.Text?.Length ?? 0;
        }
        private async void btnFinished_Clicked(object sender, EventArgs e)
        {
            bool answer = await DisplayAlert("System Says", "Done Checking? Please Verify", "Yes", "No");
            if (!answer) return;

            answer = await DisplayAlert("Finish?", "Finish Checking?", "Yes", "No");
            if (!answer) return;

            // Get a connection
            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                await DisplayAlert("No Connection!", "Cannot connect to server!", "OK");
                return;
            }

            try
            {
                using var sqlCmd = conn.CreateCommand();

                foreach (var skuItem in SKUList)
                {
                    if (int.TryParse(skuItem.ChkQty, out int chkQtyVal) && chkQtyVal == 0)
                    {
                        sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                                             $"isUpdate=1, " +
                                             $"checkTime='00:00:00', " +
                                             $"isCSorted=1, " +
                                             $"CheckBy=@UserID " +
                                             $"WHERE SKU=@SKU AND ID_SumHdr=@ID_SumHdr";
                        sqlCmd.Parameters.Clear();
                        sqlCmd.Parameters.AddWithValue("@UserID", AppGlobal.ID_User);
                        sqlCmd.Parameters.AddWithValue("@SKU", skuItem.SKU);
                        sqlCmd.Parameters.AddWithValue("@ID_SumHdr", AppGlobal.ID_SumHdr);

                        await sqlCmd.ExecuteNonQueryAsync();
                    }

                    if (!string.IsNullOrEmpty(skuItem.Slot) && skuItem.Slot.Split(',').Length > 1)
                    {
                        sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                                             $"isCSorted=1, " +
                                             $"CheckBy=@UserID " +
                                             $"WHERE SKU=@SKU AND ID_SumHdr=@ID_SumHdr";
                        sqlCmd.Parameters.Clear();
                        sqlCmd.Parameters.AddWithValue("@UserID", AppGlobal.ID_User);
                        sqlCmd.Parameters.AddWithValue("@SKU", skuItem.SKU);
                        sqlCmd.Parameters.AddWithValue("@ID_SumHdr", AppGlobal.ID_SumHdr);

                        await sqlCmd.ExecuteNonQueryAsync();
                    }
                }

                // Update PickQty table
                sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickQty SET isChecked=1 WHERE id_sumhdr=@ID_SumHdr";
                sqlCmd.Parameters.Clear();
                sqlCmd.Parameters.AddWithValue("@ID_SumHdr", AppGlobal.ID_SumHdr);
                await sqlCmd.ExecuteNonQueryAsync();

                // Update PickHdr table
                string updateChkStart = scanCount < 1 ? $"chkStart='{await AppGlobal._GetDateTime()}'," : "";
                sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickHdr SET {updateChkStart}isUpdate=1, chkEnd='{await AppGlobal._GetDateTime()}', chkDateDone='{await AppGlobal._GetDateTime(true)}' " +
                                     $"WHERE ID=@ID_SumHdr";
                sqlCmd.Parameters.Clear();
                sqlCmd.Parameters.AddWithValue("@ID_SumHdr", AppGlobal.ID_SumHdr);
                await sqlCmd.ExecuteNonQueryAsync();

                // Reset user sum header
                sqlCmd.CommandText = "UPDATE tblUsers SET ID_SumHdr=0 WHERE ID=@UserID";
                sqlCmd.Parameters.Clear();
                sqlCmd.Parameters.AddWithValue("@UserID", AppGlobal.ID_User);
                await sqlCmd.ExecuteNonQueryAsync();

                scanCount = 0;

                // Load next pick no
                await _GetSetPickNo();
            }
            catch (SqlException ex)
            {
                await DisplayAlert("Error!", ex.Message, "OK");
            }
        }

        private void DisableKeyboard()
        {
            txtBarcode.IsEnabled = false;
            txtCase.IsEnabled = false;
            txtEach.IsEnabled = false;
        }
        private void TxtEntry_Unfocused(object sender, FocusEventArgs e)
        {
            if (sender is Entry entry)
            {
                entry.BackgroundColor = Colors.WhiteSmoke;
            }
        }
        private void TxtEntry_Focused(object sender, FocusEventArgs e)
        {
            if (sender is Entry entry)
            {
                _focusedEntry = entry; // equivalent of txtboxFocus

                if (entry == txtBarcode || entry == txtStocker)
                {
                    isBarcode = entry == txtBarcode;
                    entry.BackgroundColor = Colors.PaleGreen;
                }
                else
                {
                    // Select all text for other entries
                    entry.CursorPosition = 0;
                    entry.SelectionLength = entry.Text?.Length ?? 0;
                }
            }
        }
        private void TxtCaseEach_Unfocused(object sender, FocusEventArgs e)
        {
            if (sender is Entry entry)
            {
                // Clear any text selection
                entry.SelectionLength = 0;
                entry.CursorPosition = 0;
            }
        }

        private void TxtBarcodeQtyFocus_Focused(object sender, FocusEventArgs e)
        {
            if (sender is Entry entry)
            {
                _focusedEntry = entry; // equivalent of txtboxFocus

                if (entry == txtBarcode || entry == txtStocker)
                {
                    isBarcode = entry == txtBarcode;
                    entry.BackgroundColor = Colors.PaleGreen;
                }
                else
                {
                    // Select all text for other entries
                    entry.CursorPosition = 0;
                    entry.SelectionLength = entry.Text?.Length ?? 0;
                }
            }
        }
        private async void BtnAccept_Clicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSKU.Text))
                return;

            if (!string.IsNullOrWhiteSpace(txtEach.Text) &&
                !string.IsNullOrWhiteSpace(txtCase.Text) &&
                double.TryParse(txtEach.Text, out double eachVal) && eachVal >= 0 &&
                double.TryParse(txtCase.Text, out double caseVal) && caseVal >= 0)
            {
                if (!string.IsNullOrWhiteSpace(txtSKU.Text))
                {
                    bool answer = await DisplayAlert("Accept?", "Accept quantity?", "Yes", "No");
                    if (answer)
                    {
                        _AcceptItemAsync();
                    }
                }
            }
        }
        private void TxtOther_Focused(object sender, FocusEventArgs e)
        {
            // Focus the previously focused entry (_focusedEntry)
            _focusedEntry?.Focus();
        }
        private async Task _AcceptItemAsync()
        {
            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                await DisplayAlert("No Connection!", "Cannot connect to server!", "OK");
                return;
            }

            using var txn = conn.BeginTransaction();
            using var sqlCmd = conn.CreateCommand();
            sqlCmd.Transaction = txn;

            try
            {
                double dBum = ParseEntry(txtBum);
                double dCase = ParseEntry(txtCase);
                double dEach = ParseEntry(txtEach);

                double dQty = (dBum * dCase) + dEach;
                double totQty = 0.0;

                var lvI = SKUList.ElementAt(sSKU);

                // Update PickHdr
                if (!isStarted)
                {
                    isStarted = true;
                    sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickHdr SET isUpdate=1, chkStart=@chkStart WHERE ID=@ID_SumHdr";
                    sqlCmd.Parameters.Clear();
                    sqlCmd.Parameters.AddWithValue("@chkStart", await AppGlobal._GetDateTime());
                    sqlCmd.Parameters.AddWithValue("@ID_SumHdr", AppGlobal.ID_SumHdr);
                }
                else
                {
                    sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickHdr SET isUpdate=1 WHERE ID=@ID_SumHdr";
                    sqlCmd.Parameters.Clear();
                    sqlCmd.Parameters.AddWithValue("@ID_SumHdr", AppGlobal.ID_SumHdr);
                }
                await sqlCmd.ExecuteNonQueryAsync();

                // Prepare UPC
                string sUPC = (!pbScanned.IsVisible && !string.IsNullOrWhiteSpace(txtBarcode.Text))
                                ? $"UPC='{txtBarcode.Text}',"
                                : "";
                if (dEach == 0 && dCase == 0)
                    sUPC = "";

                if (isSummary == 1)
                {
                    sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET {sUPC}PickBy=@UserID, ConfBy=@ID_Stocker, PickTime=@PickTime " +
                                         $"WHERE SKU=@SKU AND ID_SumHdr=@ID_SumHdr";
                    sqlCmd.Parameters.Clear();
                    sqlCmd.Parameters.AddWithValue("@UserID", AppGlobal.ID_User);
                    sqlCmd.Parameters.AddWithValue("@ID_Stocker", ID_Stocker);
                    sqlCmd.Parameters.AddWithValue("@PickTime", await AppGlobal._GetDateTime());
                    sqlCmd.Parameters.AddWithValue("@SKU", lvI.SKU);
                    sqlCmd.Parameters.AddWithValue("@ID_SumHdr", AppGlobal.ID_SumHdr);
                    await sqlCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    var dsData = new DataSet();
                    await AppGlobal._WorkQueryAsync($"SELECT ID, Qty FROM tbl{AppGlobal.pPickNo}PickDtl WHERE SKU='{lvI.SKU}' AND ID_SumHdr={AppGlobal.ID_SumHdr} ORDER BY slot,sku", dsData, "DATA");

                    int lCount = dsData.Tables[0].Rows.Count - 1;
                    double dQtyBak = dQty;

                    for (int iCount = 0; iCount <= lCount; iCount++)
                    {
                        var dRow = dsData.Tables[0].Rows[iCount];
                        double dNeedQty = double.TryParse(dRow["Qty"].ToString(), out double temp) ? temp : 0;

                        if (iCount == lCount)
                        {
                            sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET isCSorted=1, cSortQty=@cSortQty, isUpdate=1 WHERE ID=@ID";
                            sqlCmd.Parameters.Clear();
                            sqlCmd.Parameters.AddWithValue("@cSortQty", dQty);
                            sqlCmd.Parameters.AddWithValue("@ID", dRow["ID"]);
                            await sqlCmd.ExecuteNonQueryAsync();
                            totQty += dQty;
                        }
                        else if (dQty >= dNeedQty)
                        {
                            sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET isCSorted=1, cSortQty=Qty, isUpdate=1 WHERE ID=@ID";
                            sqlCmd.Parameters.Clear();
                            sqlCmd.Parameters.AddWithValue("@ID", dRow["ID"]);
                            await sqlCmd.ExecuteNonQueryAsync();
                            dQty -= dNeedQty;
                            totQty += dNeedQty;
                        }
                        else
                        {
                            sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET isCSorted=1, cSortQty=@cSortQty, isUpdate=1 WHERE ID=@ID";
                            sqlCmd.Parameters.Clear();
                            sqlCmd.Parameters.AddWithValue("@cSortQty", dQty);
                            sqlCmd.Parameters.AddWithValue("@ID", dRow["ID"]);
                            await sqlCmd.ExecuteNonQueryAsync();
                            totQty += dQty;
                            dQty = 0;
                            break;
                        }
                    }

                    lvI.ChkQty = totQty.ToString("N2");
                    dsData.Tables.Clear();
                }

                // Update PickQty
                sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickQty SET isChecked=1, chkQty=@chkQty WHERE ID=@ID";
                sqlCmd.Parameters.Clear();
                sqlCmd.Parameters.AddWithValue("@chkQty", totQty);
                sqlCmd.Parameters.AddWithValue("@ID", lvI.ID);
                await sqlCmd.ExecuteNonQueryAsync();

                txn.Commit();
                scanCount++;
                pbScanned.IsVisible = true;

                _CountPicked();
                _ClearScan();
            }
            catch (Exception ex)
            {
                txn.Rollback();
                await DisplayAlert("Transaction Error", $"Please Retry.\n{ex.Message}", "OK");
            }
        }
        private async void btnConfirm_Clicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtStockerTag))
            {
                await DisplayAlert("OK", "Item Confirmed!", "OK");

                ID_Stocker = int.Parse(txtStockerTag);

                // Call cancel logic
                btnCancel_Clicked(null, null);

                // Call the MAUI _AcceptItemAsync method
                await _AcceptItemAsync();
            }
        }
        private void btnCancel_Clicked(object sender, EventArgs e)
        {
            // Show/hide the MAUI StackLayouts / Panels
            pnlNavigate.IsVisible = true;
            pnlInput.IsVisible = true;
            pnlConfirm.IsVisible = false;

            // Focus the barcode Entry
            txtBarcode.Focus();
            txtBarcode.CursorPosition = 0;
            txtBarcode.SelectionLength = txtBarcode.Text?.Length ?? 0;
        }
        private async void txtStocker_Completed(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtStocker.Text))
            {
                await ConfirmStockerAsync();
            }
        }

        private void txtStocker_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Allow only numeric input
            if (!_isAllowedNum(e.NewTextValue))
            {
                txtStocker.Text = e.OldTextValue;
            }
        }

        // Optional: Handle Escape key (platform-specific)
        private void txtStocker_HandledEscape(object sender, EventArgs e)
        {
            // Call Cancel logic
            btnCancel_Clicked(null, null);
        }
        private async Task ConfirmStockerAsync()
        {
            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                await DisplayAlert("No Connection!", "Cannot connect to server!", "OK");
                return;
            }

            string stockerEENo = txtStocker.Text.Trim();
            if (string.IsNullOrEmpty(stockerEENo))
                return;

            string query = "SELECT ID, (LName + ', ' + FName + ' ' + MI) AS FullName " +
                           "FROM tblUsers " +
                           "WHERE EENo = @EENo AND isStocker = 1 AND isActive = 1";

            using var sqlCmd = new SqlCommand(query, conn);
            sqlCmd.Parameters.AddWithValue("@EENo", stockerEENo);

            using var reader = await sqlCmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                // Save ID in the private variable instead of Tag
                txtStockerTag = reader["ID"].ToString().Trim();

                await DisplayAlert("Stocker Name:", reader["FullName"].ToString(), "OK");

                btnConfirm.Focus();
                btnConfirm.IsEnabled = true;
            }
            else
            {
                txtStockerTag = null;

                await DisplayAlert("Not Found!", "Stocker ID not found!", "OK");

                txtStocker.CursorPosition = 0;
                txtStocker.SelectionLength = txtStocker.Text?.Length ?? 0;
                txtStocker.Focus();
            }
        }
        private bool _isUPCFound(string upc)
        {
            int cnt = 1;

            foreach (var item in SKUList)
            {
                // In your original code, SubItems(6) = UPC, SubItems(7) = ChkQty, SubItems(4) = Qty
                // Mapping to SKUItem properties:
                // UPC = item.UPC
                // ChkQty = item.ChkQty
                // Qty = item.Qty

                if (!string.IsNullOrWhiteSpace(item.UPC) && item.UPC.Contains("-" + upc.Trim() + ","))
                {
                    if (int.TryParse(item.ChkQty, out int chkQty) && chkQty > 0)
                    {
                        // Already checked
                        Application.Current.Dispatcher.Dispatch(() =>
                        {
                            DisplayAlert("Checked!", $"SKU: {item.SKU} Checked already", "OK");
                        });
                        break;
                    }
                    else if (double.TryParse(item.Qty, out double qty) &&
                             double.TryParse(item.ChkQty, out double chkQty2) &&
                             qty == chkQty2)
                    {
                        // Already checked, skip
                        continue;
                    }

                    // Set current SKU index for other functions
                    sSKU = SKUList.IndexOf(item);

                    // Call your existing functions (make sure these are converted to MAUI)
                    _getDuplicateSKUIndex(item.SKU.Trim());
                    double bumVal = double.TryParse(item.BUM, out double b) ? b : 0;
                    double qtyVal = double.TryParse(item.Qty, out double q) ? q : 0;

                    _loadDetails(item.SKU, bumVal, qtyVal, item.Descr);

                    return true;
                }

                cnt++;
            }

            return false;
        }
        private void _getDuplicateSKUIndex(string sku)
        {
            skuArr = new List<int>(); // Reset list

            for (int i = 0; i < SKUList.Count; i++)
            {
                var item = SKUList[i];
                if (string.IsNullOrWhiteSpace(item.SKU))
                    continue;

                if (item.SKU.Trim() == sku)
                {
                    skuArr.Add(i); // Add the index of the matching SKU
                }
            }

            // Optional: Debug
            // foreach (var index in skuArr)
            // {
            //     Console.WriteLine(index);
            // }
        }
        private void _loadDetails(string sku, double cse, double qty, string skuDesc)
        {
            txtCase.IsEnabled = cse != 1;

            if (cse == 1 || qty < cse)
            {
                txtCase.Text = "0";
                txtEach.Text = qty.ToString("N2");
            }
            else
            {
                txtCase.Text = Math.Floor(qty / cse).ToString("N2");
                txtEach.Text = (qty % cse).ToString("N2");
            }

            txtEachVal = txtEach.Text;
            txtEach.Text = "";

            txtBum.Text = cse.ToString("N2");
            txtSKU.Text = sku;
            txtDesc.Text = skuDesc;

            if (double.TryParse(txtCase.Text, out var cv) && cv == 0)
                txtEach.Focus();
            else
                txtCase.Focus();
        }
        private bool _CheckD(double cse, int qty)
        {
            // Hide the scanned indicator
            pbScanned.IsVisible = false;

            // Example logic for picked items (commented out in VB)
            // If you have a CollectionView item equivalent to lvSKUtoPick, you can implement like this:
            /*
            if (!string.IsNullOrEmpty(lvSKUtoPick.PickedQty)) // Replace lvSKUtoPick.PickedQty with actual property
            {
                pbScanned.IsVisible = true;
                txtSKU.Text = txtpSKU.Text;

                double dSetQty = cse;
                double pickedQty = double.Parse(lvSKUtoPick.PickedQty); // Replace with actual value

                if (dSetQty == 1 || pickedQty < dSetQty)
                {
                    txtCase.Text = "0";
                    txtEach.Text = pickedQty.ToString("N2"); // 2-decimal format
                }
                else
                {
                    txtCase.Text = Math.Floor(pickedQty / dSetQty).ToString("N2");
                    txtEach.Text = (pickedQty % dSetQty).ToString("N2");
                }
            }
            */

            return true; // You can modify return logic based on actual use
        }
        private void Button1_Clicked(object sender, EventArgs e)
        {
            // Hide the panel
            pnlItems.IsVisible = false;

            // Set focus to the barcode entry
            txtBarcode.Focus();
        }
        private void Button3_Clicked(object sender, EventArgs e)
        {
            // Hide the second items panel
            pnlitems2.IsVisible = false;

            // Focus the barcode entry
            txtBarcode.Focus();
        }
        private void btnConso_Clicked(object sender, EventArgs e)
        {
            // Show the second items panel
            pnlitems2.IsVisible = true;

            // Update the count label
            lblCnt2.Text = $"Count: {lvSKU2.ItemsSource.Cast<object>().Count()}";
        }
        private async void LvSKU_SelectionConfirmed(object sender, EventArgs e)
        {
            if (lvSKU.SelectedItem is SKUItem selectedItem)
            {
                // Check if ChkQty (equivalent to SubItems(7)) is empty
                if (string.IsNullOrWhiteSpace(selectedItem.ChkQty))
                {
                    bool answer = await DisplayAlert("System Says", "Receive as OS?", "Yes", "No");
                    if (answer)
                    {
                        // Replace "-", "," in UPC (equivalent to SubItems(6))
                        txtBarcode.Text = selectedItem.UPC?.Replace("-", "").Replace(",", "").Trim();

                        // Call your existing Button1_Click logic
                        Button1_Clicked(null, null);

                        // Example: show how many UPCs are in the list
                        var upcCount = selectedItem.UPC?.Split(',').Length ?? 0;
                        await DisplayAlert("Info", $"UPC count: {upcCount}", "OK");

                        // Optional: add any logic for length check
                        if (upcCount == 0)
                        {
                            // Handle empty UPC case
                        }
                    }
                }
            }
        }
        private int _getIndexLV()
        {
            for (int i = 0; i < SKUList.Count; i++)
            {
                var item = SKUList[i];
                if (item.Descr?.Trim() == txtSKU.Text.Trim()) // SubItems(2) maps to Descr
                {
                    return i; // MAUI index starts at 0, no need to subtract 1
                }
            }
            return -1; // Not found
        }
        private void TmrRequest_Elapsed(object sender, ElapsedEventArgs e)
        {
            // Run on UI thread
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await TmrRequest_Tick(sender, e); // Your async tick logic
            });
        }
        private void _SetUser(Label lbl)
        {
            if (lbl != null)
                lbl.Text = $"User : {AppGlobal.sUserName}";
        }
        private async Task<string> _GetDeptNameAsync(object deptID)
        {
            if (deptID == null || deptID == DBNull.Value)
                return "";

            string deptName = "";

            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
                return deptName;

            string query = "SELECT DeptName FROM tblDepartments WHERE ID=@ID";
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@ID", deptID);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                deptName = reader["DeptName"].ToString().Trim();

            return deptName;
        }



        // ====== Supporting SKUItem class ======
        public class SKUItem
        {
            public string ID { get; set; }
            public string BUM { get; set; }
            public string Slot { get; set; }
            public string SKU { get; set; }
            public string Descr { get; set; }
            public string Qty { get; set; }
            public string PickQty { get; set; }
            public string UPC { get; set; }
            public string ChkQty { get; set; }
        }

        // ====== Remaining event handlers ======
        private void lblDeptStore_Loaded(object sender, EventArgs e) { }
        private void txtDeptStore_TextChanged(object sender, TextChangedEventArgs e) { }
        private void PnlInput_Tapped(object sender, EventArgs e)
        {
            // Equivalent to pnlInput_GotFocus in WinForms
            // Set focus to the first entry inside the panel, e.g., txtBarcode
            txtBarcode.Focus();

            // Optional: select all text if needed
            txtBarcode.CursorPosition = 0;
            txtBarcode.SelectionLength = txtBarcode.Text?.Length ?? 0;
        }
        private void lblBarcode_Loaded(object sender, EventArgs e) { }
        private void txtBarcode_TextChanged(object sender, TextChangedEventArgs e) { }
        private void txtDesc_TextChanged(object sender, TextChangedEventArgs e) { }
        private void pbScanned_Clicked(object sender, EventArgs e) { }
        private void pbReq_Clicked(object sender, EventArgs e) { }
        private void lblSKU_Loaded(object sender, EventArgs e) { }
        private void txtSKU_TextChanged(object sender, TextChangedEventArgs e) { }
        private void lblCase_Loaded(object sender, EventArgs e) { }
        private void txtCase_TextChanged(object sender, TextChangedEventArgs e) { }
        private void lblEach_Loaded(object sender, EventArgs e) { }
        private void txtEach_TextChanged(object sender, TextChangedEventArgs e) { }
        private void lblBUM_Loaded(object sender, EventArgs e) { }
        private void txtBum_TextChanged(object sender, TextChangedEventArgs e) { }
        private void lblDone_Loaded(object sender, EventArgs e) { }
        private void txtDone_TextChanged(object sender, TextChangedEventArgs e) { }
        private void btnViewItems_Clicked(object sender, EventArgs e)
        {
            // Show the items panel
            pnlItems.IsVisible = true;

            // Update the count label
            lblCnt.Text = $"Count: {lvSKU.ItemsSource?.Cast<object>().Count() ?? 0}";

            // Focus the CollectionView
            lvSKU.Focus();

            // Select the current item if SKU is not empty and there are items
            if (!string.IsNullOrWhiteSpace(txtSKU.Text) && lvSKU.ItemsSource != null)
            {
                int index = _getIndexLV(); // assuming _getIndexLV() returns int index
                if (index >= 0)
                {
                    // Scroll to item and select it
                    var items = lvSKU.ItemsSource.Cast<CheckingPage.SKUItem>().ToList();
                    if (index < items.Count)
                    {
                        lvSKU.ScrollTo(items[index], position: Microsoft.Maui.Controls.ScrollToPosition.MakeVisible, animate: true);
                        // Optionally highlight item if needed (MAUI CollectionView does not have direct .Selected property)
                    }
                }
            }
        }
        private async void btnAccept_Clicked(object sender, EventArgs e) { }
        private async void GetSKUDescr() { }
        private double ParseEntry(Entry entry)
        {
            return double.TryParse(entry.Text, out double val) ? val : 0;
        }
        public void OnEscapePressed()
        {
            // Equivalent of Escape key
            Navigation.PopAsync(); // Close current page
        }
        public void FocusBarcode()
        {
            // Equivalent of Tab key to move focus
            txtBarcode.Focus();
            txtBarcode.CursorPosition = 0;
            txtBarcode.SelectionLength = txtBarcode.Text?.Length ?? 0;
        }
        private bool _isAllowedNum(string text)
        {
            // Allow empty string
            if (string.IsNullOrEmpty(text))
                return true;

            // Check if all characters are digits
            return text.All(char.IsDigit);
        }
        private async Task _WorkQueryAsync(string sqlQuery, DataSet ds, string tableName)
        {
            try
            {
                using var con = await AppGlobal._SQL_Connect();
                if (con == null) return;

                using var cmd = new SqlCommand(sqlQuery, con);
                using var adapter = new SqlDataAdapter(cmd);

                if (ds.Tables.Contains(tableName))
                    ds.Tables[tableName].Clear();

                adapter.Fill(ds, tableName);
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Application.Current?.MainPage?
                        .DisplayAlert("SQL Error", ex.Message, "OK");
                });
            }
        }
        private string _GetDateTime(bool onlyDate = false)
        {
            // If onlyDate is true, return date only (yyyy-MM-dd)
            if (onlyDate)
                return DateTime.Now.ToString("yyyy-MM-dd");

            // Otherwise return date + time
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
        private async Task<string> _GetUserNameAsync(string userID)
        {
            if (string.IsNullOrWhiteSpace(userID))
                return "";

            string fullName = "";

            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
                return fullName;

            string query = "SELECT LName + ', ' + FName + ' ' + MI AS FullName FROM tblUsers WHERE ID=@ID";
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@ID", userID);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                fullName = reader["FullName"].ToString().Trim();

            return fullName;
        }
        private void lblTransfer_Loaded(object sender, EventArgs e)
        {
            // Safe place to initialize label logic
            // Example:
            // lblTransfer.Text = "Transfer:";
        }
        private void lblLoc_Loaded(object sender, EventArgs e)
        {
        }
        private void lblPicker_Loaded(object sender, EventArgs e)
        {
        }
        private void lblTrf_Loaded(object sender, EventArgs e)
        {
        }
        public void OnF1Pressed() { /* F1 logic */ }
        public void OnF2Pressed() { /* F2 logic */ }

        // ====== CheckerPage class-level variables ======
        private Entry _focusedEntry;
        private int iSKU;
        private int sSKU;
        private int ID_Stocker;
        private bool isStarted;
        private int chkqty;
        private int pickqty;
        private string Gsku;
        private int lvCnt;
        private List<int> skuArr;
        private string txtEachVal;
        private int scanCount;
        private int iRetry = 0;
        private MainMenuPage _mainMenu;  // MainMenuPage reference
        private bool isBarcode = true;
        private int isSummary = 0;      // Tracks whether the current operation is a summary (0 = not summary, 1 = summary)
        private System.Timers.Timer tmrRequest;  // <--- add this
        private string txtStockerTag; // old txtStocker.Tag
    }
}
