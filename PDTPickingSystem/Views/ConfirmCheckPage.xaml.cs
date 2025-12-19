using Android.OS;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlTypes;
using Microsoft.Maui.Controls;
using PDTPickingSystem.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Data;
using static PDTPickingSystem.Views.CheckingPage;

namespace PDTPickingSystem.Views
{
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
    public partial class ConfirmCheckPage : ContentPage
    {
        // 🔁 VB: Dim txtboxFocus As New TextBox
        // MAUI does not use focus TextBox objects directly,
        // but we keep it as a reference holder (DO NOT REMOVE)
        private Entry txtboxFocus = new Entry();
        // 🔁 VB: Dim iSKU As Integer, sSKU As String = "", ID_Stocker As Integer = 0
        private int iSKU;
        private string sSKU = "";
        private int ID_Stocker = 0;

        // 🔁 VB: Dim isStarted As Boolean = False
        private bool isStarted = false;

        // 🔁 VB: Dim dtSet As DataSet
        private DataSet dtSet;

        // 🔁 VB: Dim sqlAdp As SqlDataAdapter
        private SqlDataAdapter sqlAdp;

        // 🔁 VB: Dim sqlCmd As SqlCommand
        private SqlCommand sqlCmd;

        // 🔁 VB: Dim skuArr() As Integer
        // MAUI/C#: dynamic array → List<int>
        private List<int> skuArr = new List<int>();

        // 🔁 VB: Dim sumhdr As Integer
        private int sumhdr;

        // 🔁 VB: Dim trfNo As String = ""
        // ❗ IMPORTANT: stays PAGE-LOCAL (NOT AppGlobal)
        private string trfNo = "";

        // 🔁 VB: Dim isConfirmed As Integer = 0
        private int isConfirmed = 0;

        // -------------------------------
        // 📌 NEW: ObservableCollections for binding
        // -------------------------------
        public ObservableCollection<SKUItem> lvSKUCollection = new();
        public ObservableCollection<TransferItem> lvSKU2Collection = new();

        public ConfirmCheckPage()
        {
            InitializeComponent();
            BindingContext = this; // Required for XAML bindings
            // In your page constructor, subscribe to Focused/Unfocused events
            txtCase.Unfocused += TxtCaseOrEach_Unfocused;
            txtEach.Unfocused += TxtCaseOrEach_Unfocused;
        }
        protected override void OnAppearing()
        {
            base.OnAppearing();

            // ------------------------------------
            // Signal image (equivalent to frmMenu.pbSignal.Image)
            // ------------------------------------
            pbSignal.Source = AppGlobal.MenuSignalImage;
            // ↑ ImageSource instead of Image

            // ------------------------------------
            // Set user label
            // ------------------------------------
            AppGlobal._SetUser(lblUser);

            // ------------------------------------
            // Layout handling (Dock / Size not used in MAUI)
            // ------------------------------------
            pnlMain.IsVisible = true;

            pnlDetails.IsVisible = false;
            pnlSelectTrf.IsVisible = false;

            // Layouts already stretch automatically in MAUI
            // (DockStyle.Fill is implicit)

            // ------------------------------------
            // Barcode initialization
            // ------------------------------------
            AppGlobal.isBarcode = true;
            txtBarcode.Text = string.Empty;

            // Focus barcode entry
            Dispatcher.Dispatch(() =>
            {
                txtBarcode.Focus();
            });
        }

        // Equivalent of pnlMain_GotFocus
        private void pnlMain_Focused(object sender, FocusEventArgs e)
        {
            // Your original code was empty
        }
        private void txtBarcode_TextChanged(object sender, TextChangedEventArgs e)
        {
            // sender is the Entry control
            var entry = sender as Entry;

            // New text value: e.NewTextValue
            // Old text value: e.OldTextValue

            // Your code here
        }
        private void TxtDesc_Tapped(object sender, EventArgs e)
        {
            // Your logic here
        }
        private void PbScanned_Tapped(object sender, EventArgs e)
        {
            // Logic for when pbScanned is tapped
        }
        private void LblSKU_Loaded(object sender, EventArgs e)
        {
            // Your logic here
        }
        private void TxtSKU_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Your logic here
        }
        private void LblCase_Loaded(object sender, EventArgs e)
        {
            // Your logic here
        }
        private void TxtCase_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Your logic here
        }
        private void LblEach_Loaded(object sender, EventArgs e)
        {
            // Your logic here
        }
        private void TxtEach_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Your logic here
            // e.NewTextValue contains the new text
            // e.OldTextValue contains the old text
        }
        private void LblBUM_Loaded(object sender, EventArgs e)
        {
            // Your logic here
        }
        private void TxtBum_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Your logic here
        }
        private void PnlDetails_Loaded(object sender, EventArgs e)
        {
            // This replaces pnlDetails.GotFocus
            // Add your logic here
        }
        private void lvSKU_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Get the newly selected item
            var selected = e.CurrentSelection.FirstOrDefault() as SKUItem;
            if (selected != null)
            {
                // Your logic here, e.g., show details
                txtSKU.Text = selected.SKU;
                txtDesc.Text = selected.Descr;
                trfNo = selected.TransferNo;
            }
        }
        private void LblUser_Loaded(object sender, EventArgs e)
        {
            // Your logic here
        }
        private async void BtnAccept_Clicked(object sender, EventArgs e)
        {
            // Check if quantities are filled and valid
            if (!string.IsNullOrEmpty(txtEach.Text) &&
                !string.IsNullOrEmpty(txtCase.Text) &&
                double.TryParse(txtEach.Text, out double eachVal) && eachVal >= 0 &&
                double.TryParse(txtCase.Text, out double caseVal) && caseVal >= 0)
            {
                // Check if SKU and items exist
                if (!string.IsNullOrWhiteSpace(txtSKU.Text) && lvSKUCollection.Count > 0)
                {
                    // Ask user confirmation
                    bool accept = await DisplayAlert("Accept?", "Accept quantity?", "Yes", "No");
                    if (accept)
                    {
                        // Call the async accept method
                        await _AcceptItemAsync();
                    }
                }
                else
                {
                    await DisplayAlert("System Says!", "No Item to Update!", "OK");
                }
            }
        }
        private async Task _AcceptItemAsync()
        {
            var conn = await AppGlobal._SQL_Connect();
            if (conn == null) return;

            using var txn = conn.BeginTransaction();
            using var sqlCmd = conn.CreateCommand();
            sqlCmd.Transaction = txn;
            sqlCmd.CommandType = CommandType.Text;

            try
            {
                // Parse quantities safely
                double bum = double.TryParse(txtBum.Text, out double tmpBum) ? tmpBum : 0;
                double caseQty = double.TryParse(txtCase.Text, out double tmpCase) ? tmpCase : 0;
                double each = double.TryParse(txtEach.Text, out double tmpEach) ? tmpEach : 0;

                double dQty = (bum * caseQty) + each;
                double totQty = 0;

                // Update PickHdr
                sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickHdr " +
                                     $"SET cnfrmDate='{AppGlobal._GetDateTime(true)}', isUpdate=1 " +
                                     $"WHERE ID={AppGlobal.ID_SumHdr}";
                await sqlCmd.ExecuteNonQueryAsync();

                // Get PickDtl data
                var dsData = new DataSet();
                await AppGlobal._WorkQueryAsync(
                    $"SELECT ID, Qty FROM tbl{AppGlobal.pPickNo}PickDtl " +
                    $"WHERE SKU={txtSKU.Text.Trim()} AND ID_SumHdr={AppGlobal.ID_SumHdr} " +
                    $"ORDER BY slot, sku", dsData, "DATA");

                var rows = dsData.Tables[0].Rows;
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    double dNeedQty = Convert.ToDouble(row["Qty"]);

                    string whereClause = string.IsNullOrEmpty(trfNo)
                        ? $"ID={row["ID"]}"
                        : $"tranNo='{trfNo}' AND sku={txtSKU.Text.Trim()}";

                    if (i == rows.Count - 1)
                    {
                        sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl " +
                                             $"SET isCnfrmSorted=1, cnfrmSortQty={dQty}, sortQty={dQty}, cSortQty={dQty}, isUpdate=1 " +
                                             $"WHERE {whereClause}";
                        await sqlCmd.ExecuteNonQueryAsync();
                        totQty += dQty;

                        // Update ObservableCollection
                        if (lvSKUCollection.Count > i)
                        {
                            lvSKUCollection[i].SortQty = dQty.ToString("N2");
                        }
                    }
                    else
                    {
                        if (dQty >= dNeedQty)
                        {
                            sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl " +
                                                 $"SET isCnfrmSorted=1, cnfrmSortQty=Qty, sortQty=Qty, cSortQty=Qty, isUpdate=1 " +
                                                 $"WHERE {whereClause}";
                            await sqlCmd.ExecuteNonQueryAsync();
                            totQty += dNeedQty;
                            dQty -= dNeedQty;

                            if (lvSKUCollection.Count > i)
                                lvSKUCollection[i].SortQty = dNeedQty.ToString("N2");
                        }
                        else
                        {
                            sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl " +
                                                 $"SET isCnfrmSorted=1, cnfrmSortQty={dQty}, sortQty={dQty}, cSortQty={dQty}, isUpdate=1 " +
                                                 $"WHERE {whereClause}";
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
                                     $"SET isConfirmed=1, cnfrmQty={totQty} " +
                                     $"WHERE ID_sumhdr={AppGlobal.ID_SumHdr} AND sku={txtSKU.Text.Trim()}";
                await sqlCmd.ExecuteNonQueryAsync();

                txn.Commit();

                // UI updates on main thread
                Dispatcher.Dispatch(() =>
                {
                    pbScanned.IsVisible = true;
                    _ClearScan();
                });

                await DisplayAlert("System Says", "Pick Qty Updated!", "OK");
                dsData.Tables.Clear();
            }
            catch (Exception ex)
            {
                txn.Rollback();
                await DisplayAlert("Transaction Error", $"Please retry.\n{ex.Message}", "OK");
            }
        }
        // ------------------------------------
        // Handle LostFocus for txtCase and txtEach
        // Equivalent to VB.NET LostFocus event
        // ------------------------------------
        private void TxtCaseOrEach_Unfocused(object sender, FocusEventArgs e)
        {
            // In MAUI, Entry does not have SelectionLength property directly like WinForms TextBox
            // So we reset the cursor position to the end to mimic "deselect text"
            if (sender is Entry entry)
            {
                entry.CursorPosition = entry.Text?.Length ?? 0;
                entry.SelectionLength = 0; // MAUI Entry supports SelectionLength
            }
        }
        private void BtnDetails_Clicked(object sender, EventArgs e)
        {
            pnlMain.IsVisible = false;
            pnlDetails.IsVisible = true;
        }
        private void Button2_Clicked(object sender, EventArgs e)
        {
            try
            {
                if (lvSKU2.SelectedItem is SKUItem selected)
                {
                    // Parse and assign
                    if (!int.TryParse(selected.ID.ToString(), out int sumhdr))
                    {
                        DisplayAlert("Error", "Invalid ID format.", "OK");
                        return;
                    }

                    AppGlobal.ID_SumHdr = sumhdr;
                    trfNo = selected.TransferNo?.Trim() ?? "";

                    _hideShow(1); // Show the main panel
                }
                else
                {
                    DisplayAlert("Notice", "Please select a Transfer to Edit", "OK");
                }
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
            }
        }
        private bool _isInList(int col, string strTrf)
        {
            // MAUI ListView doesn't have ListViewItem; use the bound collection instead
            foreach (var item in lvSKU2.ItemsSource) // Assuming lvSKU2.ItemsSource is your bound collection
            {
                if (item is SKUItem lv) // Replace SKUItem with your actual bound item class
                {
                    // Simulate SubItems(col).Text.Trim using your class properties
                    string value = col switch
                    {
                        0 => lv.ID.ToString(),
                        1 => lv.TransferNo?.Trim(),
                        2 => lv.SKU?.Trim(),
                        // add more mappings if needed for other columns
                        _ => ""
                    };

                    if (value == strTrf)
                        return true;
                }
            }
            return false;
        }
        private void Button1_Click(object sender, EventArgs e)
        {
            try
            {
                pnlDetails.IsVisible = false;  // Hide details panel
                pnlMain.IsVisible = true;      // Show main panel
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
            }
        }
        private void pnlSelectTrf_Loaded(object sender, EventArgs e)
        {
            try
            {
                // Your logic here
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
            }
        }
        private void lvSKU2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Get the newly selected TransferItem
                var selected = e.CurrentSelection.FirstOrDefault() as TransferItem;
                if (selected != null)
                {
                    // Example logic: assign to page-level variables
                    AppGlobal.ID_SumHdr = int.TryParse(selected.ID, out int sumhdr) ? sumhdr : 0;
                    trfNo = selected.TransferNo?.Trim() ?? "";

                    // Show the main panel after selection
                    _hideShow(1);
                }
            }
            catch (Exception ex)
            {
                _ = DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
            }
        }
        private void lvSKU_Unfocused(object sender, FocusEventArgs e)
        {
            // Converted from VB.NET lvSKU.Validated
            // Place your validation or post-selection logic here
        }
        private void TxtBarcode_GotFocus(object sender, FocusEventArgs e)
        {
            // ------------------------------------
            // Equivalent of: txtboxFocus = sender
            // ------------------------------------
            txtboxFocus = (Entry)sender;

            // ------------------------------------
            // Check if focused control is txtBarcode
            // ------------------------------------
            if (txtboxFocus == txtBarcode)
            {
                // Same duplicated logic preserved
                if (txtboxFocus == txtBarcode)
                {
                    AppGlobal.isBarcode = true;
                }
                else
                {
                    AppGlobal.isBarcode = false;
                }

                // ------------------------------------
                // WinForms BackColor → MAUI BackgroundColor
                // ------------------------------------
                txtboxFocus.BackgroundColor = Colors.PaleGreen;
            }
            else
            {
                // ------------------------------------
                // SelectAll equivalent in MAUI
                // ------------------------------------
                txtboxFocus.CursorPosition = 0;
                txtboxFocus.SelectionLength = txtboxFocus.Text?.Length ?? 0;
            }
        }
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
                dtSet?.Tables.Clear();

                string cmdText = $"exec spTransfer {AppGlobal.pPickNo},'{txtBarcode.Text.Trim()}',{AppGlobal.ID_User}";

                using var sqlAdp = new SqlDataAdapter(cmdText, conn);
                sqlAdp.SelectCommand.CommandTimeout = 0;

                dtSet.Clear();
                await Task.Run(() => sqlAdp.Fill(dtSet, "trfs")); // FillAsync not available, wrap in Task.Run

                isConfirmed = 0;

                if (dtSet.Tables[0].Rows.Count > 0)
                {
                    _loadlv();

                    if (isConfirmed == 1)
                    {
                        pbScanned.IsVisible = true;
                        dtSet.Tables.Clear();
                        _ClearScan();
                        await DisplayAlert("System Says", "Item Already Confirmed!", "OK");
                        return false;
                    }
                    return true;
                }

                dtSet.Dispose();
                return false;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"SQL execution failed: {ex.Message}", "OK");
                return false;
            }
        }
        private void TxtBarcode_Unfocused(object sender, FocusEventArgs e)
        {
            if (sender is Entry entry)
            {
                // Reset background color to WhiteSmoke
                entry.BackgroundColor = Colors.WhiteSmoke;

                // Optional: reset cursor and selection
                entry.CursorPosition = entry.Text?.Length ?? 0;
                entry.SelectionLength = 0;
            }
        }

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
                dtSet?.Tables.Clear();

                string cmdText = $"exec spTransfer {AppGlobal.pPickNo},'{txtBarcode.Text.Trim()}',{AppGlobal.ID_User}";

                using var sqlAdp = new SqlDataAdapter(cmdText, conn);
                sqlAdp.SelectCommand.CommandTimeout = 0;

                dtSet.Clear();
                await Task.Run(() => sqlAdp.Fill(dtSet, "trfs")); // FillAsync workaround

                if (dtSet.Tables[0].Rows.Count > 0)
                {
                    _loadlv();
                    pbScanned.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"SQL execution failed: {ex.Message}", "OK");
            }
        }

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

                // Call details loader
                _loadDetails(
                    drow["sku"].ToString(),
                    Convert.ToDouble(drow["bum"]),
                    Convert.ToInt32(drow["qty"]),
                    drow["descr"].ToString()
                );

                cnt++;
            }

            // Bind collections to your MAUI ListViews
            lvSKU.ItemsSource = lvSKUCollection;
            lvSKU2.ItemsSource = lvSKU2Collection;

            AppGlobal.ID_SumHdr = sumhdr;

            if (lvSKU2Collection.Count > 1)
            {
                _hideShow(3);
            }

            pnlDetails.IsVisible = true; // keep panel visible like in VB.NET
        }

        private void _hideShow(int toShow)
        {
            pnlDetails.IsVisible = false;
            pnlSelectTrf.IsVisible = false;
            pnlMain.IsVisible = false;

            if (toShow == 1)
            {
                pnlMain.IsVisible = true;
            }
            else if (toShow == 2)
            {
                pnlDetails.IsVisible = true;
            }
            else
            {
                pnlSelectTrf.IsVisible = true;
            }
        }
        private void _loadDetails(string sku, double cse, int qty, string skuDesc)
        {
            double dSetQty = cse; // Val(txtpCase.Tag)
            txtCase.IsEnabled = true;
            if (dSetQty == 1)
                txtCase.IsEnabled = false;

            if (dSetQty == 1 || qty < dSetQty)
            {
                txtCase.Text = "0";
                txtEach.Text = qty.ToString("N2"); // assuming fmtNumber2 = "N2"
            }
            else
            {
                double dQty = qty; // Val(txtpEach.Tag)
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
        private void _ClearScan(bool bWithBarcode = true)
        {
            if (bWithBarcode)
                txtBarcode.Text = "";

            txtSKU.Text = "";
            txtEach.Text = "0";
            txtCase.Text = "0";

            // Reset cursor / selection
            txtCase.CursorPosition = 0;
            txtCase.SelectionLength = 0;

            txtEach.CursorPosition = 0;
            txtEach.SelectionLength = 0;

            // Focus the barcode entry and select all text
            Dispatcher.Dispatch(() =>
            {
                txtBarcode.Focus();
                txtBarcode.CursorPosition = 0;
                txtBarcode.SelectionLength = txtBarcode.Text?.Length ?? 0;
            });
        }

    }
}