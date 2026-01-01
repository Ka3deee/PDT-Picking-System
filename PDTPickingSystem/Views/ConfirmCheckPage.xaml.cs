using Android.OS;
using Microsoft.Data.SqlClient;
using Microsoft.Maui.Controls;
using PDTPickingSystem.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;

namespace PDTPickingSystem.Views
{
    // ================== DATA CLASSES ==================

    public class SKUItem
    {
        public string ID { get; set; }           // lv.Text
        public string TransferNo { get; set; }   // lv.Tag
        public string ID2 { get; set; }
        public string Slot { get; set; }
        public string SKU { get; set; }
        public string Descr { get; set; }
        public string Qty { get; set; }
        public string SortQty { get; set; }
        public string CSortQty { get; set; }
        public string IsSorted { get; set; }
        public string IsCsorted { get; set; }
        public string PickBy { get; set; }
        public string CheckBy { get; set; }
        public string IsConfirmed { get; set; }
    }

    public class TransferItem
    {
        public string ID { get; set; }
        public string TransferNo { get; set; }
    }

    // ================== MAIN PAGE CLASS ==================

    public partial class ConfirmCheckPage : ContentPage
    {
        // ================== PRIVATE FIELDS ==================

        // Focus tracking
        private Entry _focusedEntry;

        // SKU and stocker tracking
        private int sSKU = -1;
        private int ID_Stocker = 0;

        // State
        private bool isStarted = false;

        // Data management
        private DataSet dtSet = new DataSet();

        // SKU tracking
        private List<int> skuArr = new List<int>();
        private int sumhdr;
        private string trfNo = "";
        private int isConfirmed = 0;

        // ObservableCollections for binding
        public ObservableCollection<SKUItem> lvSKUCollection { get; set; } = new();
        public ObservableCollection<TransferItem> lvSKU2Collection { get; set; } = new();

        // ================== CONSTRUCTOR ==================

        public ConfirmCheckPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            BindingContext = this;

            // Entry Completed handlers
            txtBarcode.Completed += Entry_Completed;
            txtCase.Completed += Entry_Completed;
            txtEach.Completed += Entry_Completed;

            // TextChanged validation
            txtBarcode.TextChanged += Entry_TextChanged;
            txtCase.TextChanged += Entry_TextChanged;
            txtEach.TextChanged += Entry_TextChanged;

            // Focus events
            txtBarcode.Focused += TxtBarcode_GotFocus;
            txtCase.Focused += TxtBarcode_GotFocus;
            txtEach.Focused += TxtBarcode_GotFocus;

            txtBarcode.Unfocused += TxtBarcode_Unfocused;
            txtCase.Unfocused += TxtCaseOrEach_Unfocused;
            txtEach.Unfocused += TxtCaseOrEach_Unfocused;

            // Bind CollectionViews
            lvSKU.ItemsSource = lvSKUCollection;
            lvSKU2.ItemsSource = lvSKU2Collection;
        }

        // ================== PAGE LIFECYCLE ==================

        protected override void OnAppearing()
        {
            base.OnAppearing();

            AppGlobal._SetUser(lblUser);

            pnlMain.IsVisible = true;
            pnlDetails.IsVisible = false;
            pnlSelectTrf.IsVisible = false;

            AppGlobal.isBarcode = true;
            txtBarcode.Text = string.Empty;

            Dispatcher.Dispatch(() => txtBarcode.Focus());
        }

        // ================== ENTRY VALIDATION & COMPLETION ==================

        /// <summary>
        /// Entry Completed handler (replaces KeyPress)
        /// </summary>
        private async void Entry_Completed(object sender, EventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;

            // Validate input
            if (entry.Text != null && entry.Text.Any(c => AppGlobal._isAllowedNum(c) == '\0'))
            {
                entry.Text = "";
                return;
            }

            dtSet = new DataSet();

            if (entry == txtBarcode)
            {
                pbScanned.IsVisible = false;

                if (!await _isUPCFoundAsync(txtBarcode.Text.Trim()))
                {
                    pbScanned.IsVisible = false;
                    await DisplayAlert("Mismatch!", "Wrong scanned item!", "OK");
                    _ClearScan();
                }
            }
            else if (entry == txtCase)
            {
                txtCase.CursorPosition = 0;
                txtCase.SelectionLength = 0;
                txtEach.Focus();
                txtEach.CursorPosition = 0;
                txtEach.SelectionLength = txtEach.Text?.Length ?? 0;
            }
            else if (entry == txtEach)
            {
                BtnAccept_Clicked(null, null);
            }
        }

        /// <summary>
        /// TextChanged validation (numeric only)
        /// </summary>
        private void Entry_TextChanged(object sender, TextChangedEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;

            if (e.NewTextValue != null && e.NewTextValue.Any(c => AppGlobal._isAllowedNum(c) == '\0'))
            {
                entry.Text = e.OldTextValue;
            }
        }

        // ================== FOCUS MANAGEMENT ==================

        private void TxtBarcode_GotFocus(object sender, FocusEventArgs e)
        {
            _focusedEntry = (Entry)sender;

            if (_focusedEntry == txtBarcode)
            {
                AppGlobal.isBarcode = true;
                _focusedEntry.BackgroundColor = Colors.PaleGreen;
            }
            else
            {
                _focusedEntry.CursorPosition = 0;
                _focusedEntry.SelectionLength = _focusedEntry.Text?.Length ?? 0;
            }
        }

        private void TxtBarcode_Unfocused(object sender, FocusEventArgs e)
        {
            if (sender is Entry entry)
            {
                entry.BackgroundColor = Colors.WhiteSmoke;
                entry.CursorPosition = entry.Text?.Length ?? 0;
                entry.SelectionLength = 0;
            }
        }

        private void TxtCaseOrEach_Unfocused(object sender, FocusEventArgs e)
        {
            if (sender is Entry entry)
            {
                entry.CursorPosition = entry.Text?.Length ?? 0;
                entry.SelectionLength = 0;
            }
        }

        // ================== BUTTON CLICK HANDLERS ==================

        private async void BtnAccept_Clicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtEach.Text) &&
                !string.IsNullOrEmpty(txtCase.Text) &&
                double.TryParse(txtEach.Text, out double eachVal) && eachVal >= 0 &&
                double.TryParse(txtCase.Text, out double caseVal) && caseVal >= 0)
            {
                if (!string.IsNullOrWhiteSpace(txtSKU.Text) && lvSKUCollection.Count > 0)
                {
                    bool accept = await DisplayAlert("Accept?", "Accept quantity?", "Yes", "No");
                    if (accept)
                    {
                        await _AcceptItemAsync();
                    }
                }
                else
                {
                    await DisplayAlert("System Says!", "No Item to Update!", "OK");
                }
            }
        }

        private void BtnDetails_Clicked(object sender, EventArgs e)
        {
            pnlMain.IsVisible = false;
            pnlDetails.IsVisible = true;
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            pnlDetails.IsVisible = false;
            pnlMain.IsVisible = true;
        }

        private void Button2_Clicked(object sender, EventArgs e)
        {
            if (lvSKU2.SelectedItem is TransferItem selected)
            {
                if (!int.TryParse(selected.ID, out int sumhdrVal))
                {
                    _ = DisplayAlert("Error", "Invalid ID format.", "OK");
                    return;
                }

                AppGlobal.ID_SumHdr = sumhdrVal;
                trfNo = selected.TransferNo?.Trim() ?? "";

                _hideShow(1);
            }
            else
            {
                _ = DisplayAlert("Notice", "Please select a Transfer to Edit", "OK");
            }
        }

        // ================== COLLECTION VIEW SELECTION ==================

        private void lvSKU_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = e.CurrentSelection.FirstOrDefault() as SKUItem;
            if (selected != null)
            {
                txtSKU.Text = selected.SKU;
                txtDesc.Text = selected.Descr;
                trfNo = selected.TransferNo;
            }
        }

        private void lvSKU2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = e.CurrentSelection.FirstOrDefault() as TransferItem;
            if (selected != null)
            {
                AppGlobal.ID_SumHdr = int.TryParse(selected.ID, out int sumhdrVal) ? sumhdrVal : 0;
                trfNo = selected.TransferNo?.Trim() ?? "";
                _hideShow(1);
            }
        }

        // ================== CORE BUSINESS LOGIC ==================

        /// <summary>
        /// Check if UPC is found - Parameterized stored procedure call
        /// </summary>
        private async Task<bool> _isUPCFoundAsync(string upc)
        {
            pnlDetails.IsVisible = true;

            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                await DisplayAlert("System Says", "Cannot Connect to SQL Server!", "OK");
                txtBarcode.Focus();
                return false;
            }

            try
            {
                lvSKUCollection.Clear();
                lvSKU2Collection.Clear();
                dtSet.Tables.Clear();

                // Parameterized stored procedure call
                using var sqlCmd = new SqlCommand("spTransfer", conn);
                sqlCmd.CommandType = CommandType.StoredProcedure;
                sqlCmd.CommandTimeout = 0;
                sqlCmd.Parameters.AddWithValue("@PickNo", AppGlobal.pPickNo);
                sqlCmd.Parameters.AddWithValue("@Barcode", txtBarcode.Text.Trim());
                sqlCmd.Parameters.AddWithValue("@UserID", AppGlobal.ID_User);

                using var sqlAdp = new SqlDataAdapter(sqlCmd);
                await Task.Run(() => sqlAdp.Fill(dtSet, "trfs"));

                isConfirmed = 0;

                if (dtSet.Tables[0].Rows.Count > 0)
                {
                    _loadlv();

                    if (isConfirmed == 1)
                    {
                        pbScanned.IsVisible = true;
                        dtSet.Tables.Clear();
                        dtSet.Dispose(); // ✅ FIXED: Dispose DataSet
                        _ClearScan();
                        await DisplayAlert("System Says", "Item Already Confirmed!", "OK");
                        return false;
                    }
                    return true;
                }

                dtSet.Dispose(); // ✅ FIXED: Dispose DataSet
                return false;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"SQL execution failed: {ex.Message}", "OK");
                return false;
            }
        }

        /// <summary>
        /// Get details - Parameterized stored procedure call
        /// </summary>
        private async Task _getDetailsAsync()
        {
            pnlDetails.IsVisible = true;

            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                await DisplayAlert("System Says", "Cannot Connect to SQL Server!", "OK");
                txtBarcode.Focus();
                return;
            }

            try
            {
                lvSKUCollection.Clear();
                lvSKU2Collection.Clear();
                dtSet.Tables.Clear();

                // Parameterized stored procedure call
                using var sqlCmd = new SqlCommand("spTransfer", conn);
                sqlCmd.CommandType = CommandType.StoredProcedure;
                sqlCmd.CommandTimeout = 0;
                sqlCmd.Parameters.AddWithValue("@PickNo", AppGlobal.pPickNo);
                sqlCmd.Parameters.AddWithValue("@Barcode", txtBarcode.Text.Trim());
                sqlCmd.Parameters.AddWithValue("@UserID", AppGlobal.ID_User);

                using var sqlAdp = new SqlDataAdapter(sqlCmd);
                await Task.Run(() => sqlAdp.Fill(dtSet, "trfs"));

                if (dtSet.Tables[0].Rows.Count > 0)
                {
                    _loadlv();
                    pbScanned.IsVisible = true;
                }

                dtSet.Dispose(); // ✅ FIXED: Dispose DataSet
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"SQL execution failed: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Load list views from dataset
        /// </summary>
        private void _loadlv()
        {
            string trno = "";
            int cnt = 0;
            sumhdr = 0;

            lvSKUCollection.Clear();
            lvSKU2Collection.Clear();

            foreach (DataRow drow in dtSet.Tables[0].Rows)
            {
                var lv = new SKUItem
                {
                    ID = drow["id_sumhdr"].ToString(),
                    TransferNo = drow["tranNo"].ToString(),
                    ID2 = drow["id2"].ToString(),
                    Slot = drow["slot"].ToString(),
                    SKU = drow["sku"].ToString(),
                    Descr = drow["descr"].ToString(),
                    Qty = drow["qty"].ToString(),
                    SortQty = drow["sortQty"].ToString(),
                    CSortQty = drow["cSortQty"].ToString(),
                    IsSorted = drow["isSorted"].ToString(),
                    IsCsorted = drow["isCsorted"].ToString(),
                    PickBy = drow["pickby"].ToString(),
                    CheckBy = drow["checkBy"].ToString(),
                    IsConfirmed = drow["isConfirmed"].ToString()
                };

                sumhdr = int.Parse(lv.ID);
                isConfirmed = Convert.ToInt32(drow["isConfirmed"]);

                // Populate lvSKU2Collection (unique transfer numbers)
                if (cnt == 0 || !_isInList(1, drow["tranno"].ToString().Trim()))
                {
                    var lv2 = new TransferItem
                    {
                        ID = drow["id_sumhdr"].ToString(),
                        TransferNo = drow["tranno"].ToString().Trim()
                    };
                    lvSKU2Collection.Add(lv2);
                }

                lvSKUCollection.Add(lv);

                _loadDetails(
                    drow["sku"].ToString(),
                    Convert.ToDouble(drow["bum"]),
                    Convert.ToInt32(drow["qty"]),
                    drow["descr"].ToString()
                );

                cnt++;
            }

            AppGlobal.ID_SumHdr = sumhdr;

            if (lvSKU2Collection.Count > 1)
            {
                _hideShow(3);
            }

            pnlDetails.IsVisible = true;
        }

        /// <summary>
        /// Accept item with parameterized queries
        /// </summary>
        private async Task _AcceptItemAsync()
        {
            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null) return;

            SqlTransaction txn = null;

            try
            {
                txn = conn.BeginTransaction();
                using var sqlCmd = conn.CreateCommand();
                sqlCmd.Transaction = txn;

                // Parse quantities
                double bum = double.TryParse(txtBum.Text, out double tmpBum) ? tmpBum : 0;
                double caseQty = double.TryParse(txtCase.Text, out double tmpCase) ? tmpCase : 0;
                double each = double.TryParse(txtEach.Text, out double tmpEach) ? tmpEach : 0;
                double dQty = (bum * caseQty) + each;
                double totQty = 0;

                // ✅ FIXED: Update PickHdr WITHOUT UPC (matching VB original)
                sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickHdr " +
                                     "SET cnfrmDate=@cnfrmDate, isUpdate=1 WHERE ID=@ID_SumHdr";
                sqlCmd.Parameters.Clear();
                sqlCmd.Parameters.AddWithValue("@cnfrmDate", await AppGlobal._GetDateTime(true));
                sqlCmd.Parameters.AddWithValue("@ID_SumHdr", AppGlobal.ID_SumHdr);
                await sqlCmd.ExecuteNonQueryAsync();

                // ✅ FIXED: UPC logic (currently unused in VB, kept for compatibility)
                string sUPC = "";
                if (!pbScanned.IsVisible && !string.IsNullOrWhiteSpace(txtBarcode.Text))
                {
                    sUPC = $"UPC={txtBarcode.Text.Trim()},";
                }
                if (each == 0 && caseQty == 0)
                {
                    sUPC = "";
                }
                // NOTE: sUPC is calculated but NOT used in any UPDATE statements (matching VB behavior)

                // Get PickDtl data with parameterized query
                var dsData = new DataSet();
                using (var selectCmd = conn.CreateCommand())
                {
                    selectCmd.Transaction = txn;
                    selectCmd.CommandText = $"SELECT ID, Qty FROM tbl{AppGlobal.pPickNo}PickDtl " +
                                            "WHERE SKU=@SKU AND ID_SumHdr=@SumHdr ORDER BY slot, sku";
                    selectCmd.Parameters.AddWithValue("@SKU", txtSKU.Text.Trim());
                    selectCmd.Parameters.AddWithValue("@SumHdr", AppGlobal.ID_SumHdr);

                    using var adapter = new SqlDataAdapter(selectCmd);
                    adapter.Fill(dsData, "DATA");
                }

                var rows = dsData.Tables[0].Rows;
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    double dNeedQty = Convert.ToDouble(row["Qty"]);

                    if (i == rows.Count - 1) // Last item
                    {
                        // ✅ FIXED: NO UPC in PickDtl updates
                        if (string.IsNullOrEmpty(trfNo))
                        {
                            sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                                "isCnfrmSorted=1, cnfrmSortQty=@qty, sortQty=@qty, cSortQty=@qty, isUpdate=1 " +
                                "WHERE ID=@ID";
                            sqlCmd.Parameters.Clear();
                            sqlCmd.Parameters.AddWithValue("@qty", dQty);
                            sqlCmd.Parameters.AddWithValue("@ID", row["ID"]);
                        }
                        else
                        {
                            sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                                "isCnfrmSorted=1, cnfrmSortQty=@qty, sortQty=@qty, cSortQty=@qty, isUpdate=1 " +
                                "WHERE tranNo=@tranNo AND sku=@sku";
                            sqlCmd.Parameters.Clear();
                            sqlCmd.Parameters.AddWithValue("@qty", dQty);
                            sqlCmd.Parameters.AddWithValue("@tranNo", trfNo);
                            sqlCmd.Parameters.AddWithValue("@sku", txtSKU.Text.Trim());
                        }
                        await sqlCmd.ExecuteNonQueryAsync();
                        totQty += dQty;

                        if (lvSKUCollection.Count > i)
                            lvSKUCollection[i].SortQty = dQty.ToString("N2");
                    }
                    else
                    {
                        if (dQty >= dNeedQty)
                        {
                            // ✅ FIXED: NO UPC in PickDtl updates
                            if (string.IsNullOrEmpty(trfNo))
                            {
                                sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                                    "isCnfrmSorted=1, cnfrmSortQty=Qty, sortQty=Qty, cSortQty=Qty, isUpdate=1 " +
                                    "WHERE ID=@ID";
                                sqlCmd.Parameters.Clear();
                                sqlCmd.Parameters.AddWithValue("@ID", row["ID"]);
                            }
                            else
                            {
                                sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                                    "isCnfrmSorted=1, cnfrmSortQty=Qty, sortQty=Qty, cSortQty=Qty, isUpdate=1 " +
                                    "WHERE tranNo=@tranNo AND sku=@sku";
                                sqlCmd.Parameters.Clear();
                                sqlCmd.Parameters.AddWithValue("@tranNo", trfNo);
                                sqlCmd.Parameters.AddWithValue("@sku", txtSKU.Text.Trim());
                            }
                            await sqlCmd.ExecuteNonQueryAsync();
                            totQty += dNeedQty;
                            dQty -= dNeedQty;

                            if (lvSKUCollection.Count > i)
                                lvSKUCollection[i].SortQty = dNeedQty.ToString("N2");
                        }
                        else
                        {
                            // ✅ FIXED: NO UPC in PickDtl updates
                            if (string.IsNullOrEmpty(trfNo))
                            {
                                sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                                    "isCnfrmSorted=1, cnfrmSortQty=@qty, sortQty=@qty, cSortQty=@qty, isUpdate=1 " +
                                    "WHERE ID=@ID";
                                sqlCmd.Parameters.Clear();
                                sqlCmd.Parameters.AddWithValue("@qty", dQty);
                                sqlCmd.Parameters.AddWithValue("@ID", row["ID"]);
                            }
                            else
                            {
                                sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                                    "isCnfrmSorted=1, cnfrmSortQty=@qty, sortQty=@qty, cSortQty=@qty, isUpdate=1 " +
                                    "WHERE tranNo=@tranNo AND sku=@sku";
                                sqlCmd.Parameters.Clear();
                                sqlCmd.Parameters.AddWithValue("@qty", dQty);
                                sqlCmd.Parameters.AddWithValue("@tranNo", trfNo);
                                sqlCmd.Parameters.AddWithValue("@sku", txtSKU.Text.Trim());
                            }
                            await sqlCmd.ExecuteNonQueryAsync();
                            totQty += dQty;

                            if (lvSKUCollection.Count > i)
                                lvSKUCollection[i].SortQty = dQty.ToString("N2");

                            dQty = 0;
                            break;
                        }
                    }
                }

                // Update PickQty
                sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickQty " +
                    "SET isConfirmed=1, cnfrmQty=@totQty " +
                    "WHERE ID_sumhdr=@SumHdr AND sku=@sku";
                sqlCmd.Parameters.Clear();
                sqlCmd.Parameters.AddWithValue("@totQty", totQty);
                sqlCmd.Parameters.AddWithValue("@SumHdr", AppGlobal.ID_SumHdr);
                sqlCmd.Parameters.AddWithValue("@sku", txtSKU.Text.Trim());
                await sqlCmd.ExecuteNonQueryAsync();

                txn.Commit();

                Dispatcher.Dispatch(() =>
                {
                    pbScanned.IsVisible = true;
                    _ClearScan();
                });

                await DisplayAlert("System Says", "Pick Qty Updated!", "OK");
                _VibrateDevice(200);

                dsData.Tables.Clear();
            }
            catch (Exception ex)
            {
                txn?.Rollback();
                await DisplayAlert("Transaction Error", $"Please retry.\n{ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Get duplicate SKU indices
        /// </summary>
        private void _getDuplicateSKUIndex(string sku)
        {
            skuArr = new List<int>();

            for (int i = 0; i < lvSKUCollection.Count; i++)
            {
                var item = lvSKUCollection[i];
                if (string.IsNullOrWhiteSpace(item.SKU))
                    continue;

                if (item.SKU.Trim() == sku)
                {
                    skuArr.Add(i);
                }
            }
        }

        /// <summary>
        /// Load SKU details into input fields
        /// </summary>
        private void _loadDetails(string sku, double cse, int qty, string skuDesc)
        {
            double dSetQty = cse;
            txtCase.IsEnabled = dSetQty != 1;

            if (dSetQty == 1 || qty < dSetQty)
            {
                txtCase.Text = "0";
                txtEach.Text = qty.ToString("N2");
            }
            else
            {
                double dQty = qty;
                txtCase.Text = Math.Floor(dQty / dSetQty).ToString("N2");
                txtEach.Text = (dQty % dSetQty).ToString("N2");
            }

            txtBum.Text = cse.ToString();
            txtSKU.Text = sku;
            txtDesc.Text = skuDesc;

            txtBarcode.CursorPosition = 0;
            txtBarcode.SelectionLength = 0;

            if (Convert.ToDouble(txtCase.Text) == 0)
            {
                txtEach.Focus();
                txtEach.CursorPosition = 0;
                txtEach.SelectionLength = txtEach.Text.Length;
            }
            else
            {
                txtCase.Focus();
                txtCase.CursorPosition = 0;
                txtCase.SelectionLength = txtCase.Text.Length;
            }
        }

        /// <summary>
        /// Clear scan fields
        /// </summary>
        private void _ClearScan(bool bWithBarcode = true)
        {
            if (bWithBarcode)
                txtBarcode.Text = "";

            txtSKU.Text = "";
            txtEach.Text = "0";
            txtCase.Text = "0";

            txtCase.CursorPosition = 0;
            txtCase.SelectionLength = 0;

            txtEach.CursorPosition = 0;
            txtEach.SelectionLength = 0;

            Dispatcher.Dispatch(() =>
            {
                txtBarcode.Focus();
                txtBarcode.CursorPosition = 0;
                txtBarcode.SelectionLength = txtBarcode.Text?.Length ?? 0;
            });
        }

        /// <summary>
        /// Show/hide panels
        /// </summary>
        private void _hideShow(int toShow)
        {
            pnlDetails.IsVisible = false;
            pnlSelectTrf.IsVisible = false;
            pnlMain.IsVisible = false;

            if (toShow == 1)
                pnlMain.IsVisible = true;
            else if (toShow == 2)
                pnlDetails.IsVisible = true;
            else
                pnlSelectTrf.IsVisible = true;
        }

        /// <summary>
        /// Check if transfer is in list
        /// </summary>
        private bool _isInList(int col, string strTrf)
        {
            foreach (var item in lvSKU2Collection)
            {
                string value = col switch
                {
                    0 => item.ID,
                    1 => item.TransferNo?.Trim(),
                    _ => ""
                };

                if (value == strTrf)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// ✅ FIXED: Vibrate device
        /// </summary>
        private void _VibrateDevice(int durationMs)
        {
            try
            {
                var duration = TimeSpan.FromMilliseconds(durationMs);
                Vibration.Default.Vibrate(duration);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Vibration failed: {ex.Message}");
            }
        }

        // ================== HARDWARE KEY HANDLERS ==================

        /// <summary>
        /// ✅ FIXED: F1 key handler (Go back/Exit)
        /// </summary>
        public void OnF1Pressed()
        {
            if (Navigation.NavigationStack.Count > 0)
            {
                MainThread.BeginInvokeOnMainThread(async () => await Navigation.PopAsync());
            }
        }

        /// <summary>
        /// Escape key handler
        /// </summary>
        public void OnEscapePressed()
        {
            if (Navigation.NavigationStack.Count > 0)
            {
                MainThread.BeginInvokeOnMainThread(async () => await Navigation.PopAsync());
            }
        }

        /// <summary>
        /// F2 key handler (focus barcode)
        /// </summary>
        public void OnF2Pressed()
        {
            if (txtBarcode != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    txtBarcode.Focus();
                    txtBarcode.CursorPosition = 0;
                    txtBarcode.SelectionLength = txtBarcode.Text?.Length ?? 0;
                });
            }
        }

        // ================== EMPTY EVENT HANDLERS (FOR XAML BINDING) ==================

        private void pnlMain_Focused(object sender, FocusEventArgs e) { }
        private void TxtDesc_Tapped(object sender, EventArgs e) { }
        private void PbScanned_Tapped(object sender, EventArgs e) { }
        private void LblSKU_Loaded(object sender, EventArgs e) { }
        private void TxtSKU_TextChanged(object sender, TextChangedEventArgs e) { }
        private void LblCase_Loaded(object sender, EventArgs e) { }
        private void TxtCase_TextChanged(object sender, TextChangedEventArgs e) { }
        private void LblEach_Loaded(object sender, EventArgs e) { }
        private void TxtEach_TextChanged(object sender, TextChangedEventArgs e) { }
        private void LblBUM_Loaded(object sender, EventArgs e) { }
        private void TxtBum_TextChanged(object sender, TextChangedEventArgs e) { }
        private void PnlDetails_Loaded(object sender, EventArgs e) { }
        private void LblUser_Loaded(object sender, EventArgs e) { }
        private void pnlSelectTrf_Loaded(object sender, EventArgs e) { }
        private void lvSKU_Unfocused(object sender, FocusEventArgs e) { }
    }
}