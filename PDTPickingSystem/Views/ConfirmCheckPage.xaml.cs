using Android.OS;
using Java.Time;
using Java.Util;
using Microsoft.Data.SqlClient;
using Microsoft.Maui.Controls;
using PDTPickingSystem.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading;

namespace PDTPickingSystem.Views
{
    // ================== DATA CLASSES ==================

    public class SKUItem
    {
        public string FromSlot { get; set; }
        public string ID { get; set; }           // lv.Text
        public string TransferNo { get; set; }   // lv.Tag
        public string ID2 { get; set; }
        public string Slot { get; set; }
        public string SKU { get; set; }
        public string Descr { get; set; }
        public string Qty { get; set; }
        public int Picked { get; set; }
        public string UPC { get; set; }
        public int ChkQty { get; set; }
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
        private bool _isScanning = false;

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

        private bool _isBusy = false;

        // ================== CONSTRUCTOR ==================

        public ConfirmCheckPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            BindingContext = this;

            System.Diagnostics.Debug.WriteLine("🔷 ConfirmCheckPage Constructor");

            // Entry Completed handlers
            txtCase.Completed += Entry_Completed;
            txtEach.Completed += Entry_Completed;

            // ✅ SCANNER SUPPORT - TextChanged for barcode scanner
            txtBarcode.TextChanged += TxtBarcode_TextChanged;

            // TextChanged validation for numeric fields only
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

            System.Diagnostics.Debug.WriteLine("✅ Event handlers attached");
        }

        // ================== PAGE LIFECYCLE ==================

        protected override void OnAppearing()
        {
            base.OnAppearing();

            System.Diagnostics.Debug.WriteLine("🔷🔷🔷 ConfirmCheckPage OnAppearing 🔷🔷🔷");
            System.Diagnostics.Debug.WriteLine($"   txtBarcode exists: {txtBarcode != null}");
            System.Diagnostics.Debug.WriteLine($"   txtBarcode is focused: {txtBarcode?.IsFocused}");

            // ✅ CRITICAL: Force focus and start monitoring
            Task.Run(async () =>
            {
                await Task.Delay(500);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    txtBarcode.Focus();
                    System.Diagnostics.Debug.WriteLine("🟢 txtBarcode focused programmatically");

                    // Start polling for text changes (barcode scanner workaround)
                    Device.StartTimer(TimeSpan.FromMilliseconds(100), () =>
                    {
                        if (!string.IsNullOrWhiteSpace(txtBarcode.Text) && txtBarcode.Text.Length >= 8)
                        {
                            System.Diagnostics.Debug.WriteLine($"🟡 TIMER DETECTED BARCODE: {txtBarcode.Text}");
                            _ = ProcessBarcodeAsync(txtBarcode.Text);
                            return false; // Stop timer
                        }
                        return txtBarcode != null; // Continue timer while control exists
                    });
                });
            });
        }

        private async Task ProcessBarcodeAsync(string barcode)
        {
            if (_isScanning) return;
            _isScanning = true;

            try
            {
                System.Diagnostics.Debug.WriteLine($"🔵 ProcessBarcodeAsync: {barcode}");

                bool found = await _isUPCFoundAsync(barcode);

                if (!found)
                {
                    await DisplayAlert("Mismatch!", "Wrong scanned item!", "OK");
                    _ClearScan();
                }
            }
            finally
            {
                _isScanning = false;
            }
        }

        // ================== ENTRY VALIDATION & COMPLETION ==================

        /// <summary>
        /// Entry Completed handler (replaces KeyPress) - handles barcode scanner Enter key
        /// </summary>
        private async void Entry_Completed(object sender, EventArgs e)
        {
            if (sender is not Entry entry)
                return;

            System.Diagnostics.Debug.WriteLine(
                $"🔵 Entry_Completed: {entry.ClassId ?? entry.AutomationId}, Text='{entry.Text}'");

            if (_isScanning)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Already scanning in Entry_Completed, returning");
                return;
            }

            _isScanning = true;

            try
            {
                // Validate numeric fields
                if (!string.IsNullOrWhiteSpace(entry.Text) &&
                    entry.Text.Any(c => AppGlobal._isAllowedNum(c) == '\0'))
                {
                    entry.Text = "";
                    return;
                }

                // ================= BARCODE =================
                if (entry == txtBarcode)
                {
                    string barcode = txtBarcode.Text?.Trim();

                    System.Diagnostics.Debug.WriteLine($"🔵 Entry_Completed for barcode: '{barcode}', Length={barcode?.Length ?? 0}");

                    if (string.IsNullOrEmpty(barcode))
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ Barcode is empty in Entry_Completed");
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine($"🔵 About to call _isUPCFoundAsync from Entry_Completed with: {barcode}");

                    bool found = await _isUPCFoundAsync(barcode);

                    System.Diagnostics.Debug.WriteLine($"🔵 _isUPCFoundAsync returned: {found}");

                    if (!found)
                    {
                        await DisplayAlert("Mismatch!", "Wrong scanned item!", "OK");
                        _ClearScan();
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine("✅ Barcode processing complete from Entry_Completed");
                }

                // ================= CASE =================
                else if (entry == txtCase)
                {
                    txtEach.Focus();
                    txtEach.SelectionLength = txtEach.Text?.Length ?? 0;
                }

                // ================= EACH =================
                else if (entry == txtEach)
                {
                    await AcceptAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Entry_Completed ERROR: {ex}");
            }
            finally
            {
                await Task.Delay(120);
                _isScanning = false;
            }
        }

        private async Task AcceptAsync()
        {
            if (!double.TryParse(txtEach.Text, out double eachVal) || eachVal < 0 ||
                !double.TryParse(txtCase.Text, out double caseVal) || caseVal < 0)
            {
                await DisplayAlert("Invalid Input", "Case and Each must be valid numbers.", "OK");
                txtEach.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtSKU.Text) || lvSKUCollection.Count == 0)
            {
                await DisplayAlert("System Says!", "No item to update.", "OK");
                txtBarcode.Focus();
                return;
            }

            bool accept = await DisplayAlert("Accept?", "Accept quantity?", "Yes", "No");
            if (!accept)
                return;

            await _AcceptItemAsync();
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

        CancellationTokenSource _barcodeCts;

        /// <summary>
        /// ✅ SCANNER SUPPORT: TextChanged handler for barcode scanner
        /// </summary>
        /// <summary>
        /// ✅ SCANNER SUPPORT: TextChanged handler for barcode scanner
        /// </summary>
        private async void TxtBarcode_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender != txtBarcode) return;

            System.Diagnostics.Debug.WriteLine($"🔵 TxtBarcode_TextChanged fired! NewText='{e.NewTextValue}', Length={e.NewTextValue?.Length ?? 0}");

            _barcodeCts?.Cancel();
            _barcodeCts = new CancellationTokenSource();
            var token = _barcodeCts.Token;

            try
            {
                await Task.Delay(150, token);

                string barcode = txtBarcode.Text?.Trim();

                System.Diagnostics.Debug.WriteLine($"🔵 After delay - barcode='{barcode}', Length={barcode?.Length ?? 0}");

                if (string.IsNullOrEmpty(barcode))
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Barcode is empty, returning");
                    return;
                }

                // ✅ REMOVED the length < 8 check - let ALL barcodes through
                // Different products have different barcode lengths!

                if (_isScanning)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Already scanning, returning");
                    return;
                }

                _isScanning = true;

                System.Diagnostics.Debug.WriteLine($"🔵 About to call _isUPCFoundAsync with barcode: {barcode}");

                bool found = await _isUPCFoundAsync(barcode);

                System.Diagnostics.Debug.WriteLine($"🔵 _isUPCFoundAsync returned: {found}");

                if (!found)
                {
                    await DisplayAlert("Mismatch!", "Wrong scanned item!", "OK");
                    _ClearScan();
                    return;
                }

                System.Diagnostics.Debug.WriteLine("✅ Barcode processing complete - fields should be populated");
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("⏸️ Barcode input cancelled (still typing)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ TxtBarcode_TextChanged ERROR: {ex}");
            }
            finally
            {
                _isScanning = false;
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
            await AcceptAsync();
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
        /// Check if UPC is found and return the item data
        /// </summary>
        private async Task<bool> _isUPCFoundAsync(string upc)
        {
            System.Diagnostics.Debug.WriteLine($"🟢 ========== _isUPCFoundAsync START ==========");
            System.Diagnostics.Debug.WriteLine($"   UPC: '{upc}'");
            System.Diagnostics.Debug.WriteLine($"   _isBusy: {_isBusy}");

            if (_isBusy)
            {
                System.Diagnostics.Debug.WriteLine("❌ _isBusy is true, returning false");
                return false;
            }

            _isBusy = true;

            try
            {
                System.Diagnostics.Debug.WriteLine("🔵 Calling _SQL_Connect...");
                using var conn = await AppGlobal._SQL_Connect();
                if (conn == null)
                {
                    System.Diagnostics.Debug.WriteLine("❌ Connection is null");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("✅ Connection successful");

                using var cmd = new SqlCommand("spTransfer", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@ref", AppGlobal.pPickNo);
                cmd.Parameters.AddWithValue("@upc", upc.Trim());
                cmd.Parameters.AddWithValue("@user", AppGlobal.ID_User);

                System.Diagnostics.Debug.WriteLine($"🔵 Executing spTransfer with: ref={AppGlobal.pPickNo}, upc={upc}, user={AppGlobal.ID_User}");

                using var reader = await cmd.ExecuteReaderAsync();

                // Clear previous list
                await MainThread.InvokeOnMainThreadAsync(() => lvSKUCollection.Clear());

                // ✅ Update user label with current logged-in user
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    lblUser.Text = string.IsNullOrEmpty(AppGlobal.sUserName)
                        ? "User: (none)"
                        : $"User: {AppGlobal.sUserName}";
                });

                bool hasRows = false;

                // Read all rows from reader
                while (await reader.ReadAsync())
                {
                    hasRows = true;

                    int isConfirmedValue = reader["isConfirmed"] != DBNull.Value
                        ? Convert.ToInt32(reader["isConfirmed"])
                        : 0;

                    if (isConfirmedValue == 1)
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            pbScanned.IsVisible = true;
                            _ClearScan();
                            await DisplayAlert("System Says", "Item Already Confirmed!", "OK");
                        });
                        return false;
                    }

                    // Extract data safely
                    string sku = reader["sku"]?.ToString() ?? "";
                    string descr = reader["descr"]?.ToString() ?? "";
                    int qty = reader["qty"] != DBNull.Value ? Convert.ToInt32(reader["qty"]) : 0;
                    double bum = reader["bum"] != DBNull.Value ? Convert.ToDouble(reader["bum"]) : 1;
                    string transferNo = reader["tranNo"]?.ToString()?.Trim() ?? "";
                    int sumhdrId = reader["id_sumhdr"] != DBNull.Value ? Convert.ToInt32(reader["id_sumhdr"]) : 0;
                    string slot = reader["slot"]?.ToString() ?? "";
                    string pickBy = reader["pickby"]?.ToString() ?? "";
                    string checkBy = reader["checkBy"]?.ToString() ?? "";

                    var lvItem = new SKUItem
                    {
                        ID = sumhdrId.ToString(),
                        TransferNo = transferNo,
                        Slot = slot,
                        SKU = sku,
                        Descr = descr,
                        Qty = qty.ToString(),
                        UPC = upc,
                        ChkQty = 0,
                        Picked = 0,
                        SortQty = "0",
                        CSortQty = "0",
                        IsSorted = "No",
                        IsCsorted = "No",
                        PickBy = pickBy,
                        CheckBy = checkBy,
                        IsConfirmed = isConfirmedValue.ToString()
                    };

                    await MainThread.InvokeOnMainThreadAsync(() => lvSKUCollection.Add(lvItem));

                    // Populate UI fields from the first row only
                    if (lvSKUCollection.Count == 1)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            txtSKU.Text = sku;
                            txtDesc.Text = descr;
                            txtBum.Text = bum.ToString();

                            if (bum <= 0 || qty < bum)
                            {
                                txtCase.Text = "0";
                                txtEach.Text = qty.ToString();
                            }
                            else
                            {
                                txtCase.Text = Math.Floor(qty / bum).ToString();
                                txtEach.Text = (qty % bum).ToString();
                            }

                            AppGlobal.ID_SumHdr = sumhdrId;
                            trfNo = transferNo;
                            pbScanned.IsVisible = true;

                            if (Convert.ToInt32(txtCase.Text) == 0)
                            {
                                txtEach.Focus();
                                txtEach.SelectionLength = txtEach.Text?.Length ?? 0;
                            }
                            else
                            {
                                txtCase.Focus();
                                txtCase.SelectionLength = txtCase.Text?.Length ?? 0;
                            }
                        });
                    }
                }

                if (!hasRows)
                {
                    System.Diagnostics.Debug.WriteLine("❌ No data returned from spTransfer");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("🟢 ========== _isUPCFoundAsync END - SUCCESS ==========");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ _isUPCFoundAsync ERROR: {ex}");
                System.Diagnostics.Debug.WriteLine($"   Stack trace: {ex.StackTrace}");
                return false;
            }
            finally
            {
                _isBusy = false;
            }
        }

        /// <summary>
        /// Load list views from dataset and populate UI fields (only first row)
        /// </summary>
        private void _loadlv()
        {
            System.Diagnostics.Debug.WriteLine("🟢 ========== _loadlv START ==========");
            System.Diagnostics.Debug.WriteLine($"   Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
            System.Diagnostics.Debug.WriteLine($"   Is Main Thread: {MainThread.IsMainThread}");

            lvSKUCollection.Clear();
            lvSKU2Collection.Clear();

            if (dtSet.Tables.Count == 0 || dtSet.Tables[0].Rows.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("❌ No rows in dataset");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"✅ Dataset has {dtSet.Tables[0].Rows.Count} rows");

            // ================= Load CollectionView =================
            foreach (DataRow drow in dtSet.Tables[0].Rows)
            {
                string id = drow["id_sumhdr"]?.ToString() ?? "0";
                string trfNoRow = drow["tranNo"]?.ToString()?.Trim() ?? "";
                string id2 = drow["id2"]?.ToString() ?? "";
                string slot = drow["slot"]?.ToString() ?? "";
                string sku = drow["sku"]?.ToString() ?? "";
                string descr = drow["descr"]?.ToString() ?? "";
                double bum = drow["bum"] != DBNull.Value ? Convert.ToDouble(drow["bum"]) : 1;
                int qty = drow["qty"] != DBNull.Value ? Convert.ToInt32(drow["qty"]) : 0;
                string sortQty = drow["sortQty"]?.ToString() ?? "0";
                string cSortQty = drow["cSortQty"]?.ToString() ?? "0";
                string isSorted = drow["isSorted"]?.ToString() ?? "No";
                string isCsorted = drow["isCsorted"]?.ToString() ?? "No";
                string pickBy = drow["pickby"]?.ToString() ?? "";
                string checkBy = drow["checkBy"]?.ToString() ?? "";
                int confirmed = drow["isConfirmed"] != DBNull.Value ? Convert.ToInt32(drow["isConfirmed"]) : 0;

                System.Diagnostics.Debug.WriteLine($"   Row data: SKU={sku}, Descr={descr}, Qty={qty}, BUM={bum}");

                var lvItem = new SKUItem
                {
                    ID = id,
                    TransferNo = trfNoRow,
                    ID2 = id2,
                    Slot = slot,
                    SKU = sku,
                    Descr = descr,
                    Qty = qty.ToString(),
                    SortQty = sortQty,
                    CSortQty = cSortQty,
                    IsSorted = isSorted,
                    IsCsorted = isCsorted,
                    PickBy = pickBy,
                    CheckBy = checkBy,
                    IsConfirmed = confirmed.ToString()
                };

                lvSKUCollection.Add(lvItem);

                // Add unique transfer numbers
                if (!_isInList(1, trfNoRow))
                {
                    lvSKU2Collection.Add(new TransferItem
                    {
                        ID = id,
                        TransferNo = trfNoRow
                    });
                }

                sumhdr = int.TryParse(id, out int tmpSum) ? tmpSum : sumhdr;
                trfNo = trfNoRow;  // ✅ FIXED: Set trfNo
                isConfirmed = confirmed;
            }

            AppGlobal.ID_SumHdr = sumhdr;

            // ================= Populate UI fields (only first row) =================
            var firstRow = dtSet.Tables[0].Rows[0];
            string firstSku = firstRow["sku"]?.ToString() ?? "";
            string firstDescr = firstRow["descr"]?.ToString() ?? "";
            int firstQty = firstRow["qty"] != DBNull.Value ? Convert.ToInt32(firstRow["qty"]) : 0;
            double firstBum = firstRow["bum"] != DBNull.Value ? Convert.ToDouble(firstRow["bum"]) : 1;

            System.Diagnostics.Debug.WriteLine($"🔵 Extracted first row: SKU={firstSku}, Descr={firstDescr}, Qty={firstQty}, BUM={firstBum}");
            System.Diagnostics.Debug.WriteLine($"🔵 About to call _loadDetails...");

            // Call _loadDetails synchronously on UI thread
            _loadDetails(firstSku, firstBum, firstQty, firstDescr);

            System.Diagnostics.Debug.WriteLine($"🔵 After calling _loadDetails");

            // ================= Panel visibility =================
            if (lvSKU2Collection.Count > 1)
                _hideShow(3);
            else
            {
                _hideShow(1);
                pbScanned.IsVisible = true;
            }

            System.Diagnostics.Debug.WriteLine($"🟢 ========== _loadlv END - lvSKUCollection count: {lvSKUCollection.Count} ==========");
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

                // ✅ Update PickHdr
                sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickHdr " +
                                     "SET cnfrmDate=@cnfrmDate, isUpdate=1 WHERE ID=@ID_SumHdr";
                sqlCmd.Parameters.Clear();
                sqlCmd.Parameters.AddWithValue("@cnfrmDate", await AppGlobal._GetDateTime(true));
                sqlCmd.Parameters.AddWithValue("@ID_SumHdr", AppGlobal.ID_SumHdr);
                await sqlCmd.ExecuteNonQueryAsync();

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
        /// Populate SKU details into input fields
        /// </summary>
        private void _loadDetails(string sku, double bum, int qty, string descr)
        {
            System.Diagnostics.Debug.WriteLine($"🔵 ========== _loadDetails START ==========");
            System.Diagnostics.Debug.WriteLine($"   Parameters: SKU={sku}, BUM={bum}, Qty={qty}, Descr={descr}");

            // SKU & Description
            txtSKU.Text = sku;
            txtDesc.Text = descr;

            // BUM
            txtBum.Text = bum.ToString();

            // CASE / EACH
            if (bum <= 0 || qty < bum)
            {
                txtCase.Text = "0";
                txtEach.Text = qty.ToString();
            }
            else
            {
                txtCase.Text = Math.Floor(qty / bum).ToString();
                txtEach.Text = (qty % bum).ToString();
            }

            // Focus logic
            if (Convert.ToInt32(txtCase.Text) == 0)
            {
                txtEach.Focus();
                txtEach.SelectionLength = txtEach.Text.Length;
            }
            else
            {
                txtCase.Focus();
                txtCase.SelectionLength = txtCase.Text.Length;
            }

            // Show scanned icon
            pbScanned.IsVisible = true;

            System.Diagnostics.Debug.WriteLine($"🔵 ========== _loadDetails END ==========");
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
        /// Vibrate device
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

        public void OnF1Pressed()
        {
            if (Navigation.NavigationStack.Count > 0)
            {
                MainThread.BeginInvokeOnMainThread(async () => await Navigation.PopAsync());
            }
        }

        public void OnEscapePressed()
        {
            if (Navigation.NavigationStack.Count > 0)
            {
                MainThread.BeginInvokeOnMainThread(async () => await Navigation.PopAsync());
            }
        }

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

        // ================== EMPTY EVENT HANDLERS ==================

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