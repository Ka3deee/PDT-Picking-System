using Android.OS;
using Android.Telephony;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlTypes;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Dispatching;
using PDTPickingSystem.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

//PART 1//
namespace PDTPickingSystem.Views
{
    public partial class PickingPage : ContentPage
    {
        // Tracks which Entry currently has focus (TextBox → Entry in MAUI)
        private Entry txtboxFocus; // Converted from TextBox
        // SKU and stocker tracking
        private int sSKU = -1; // Converted from Integer
        private int ID_Stocker = 0; // Converted from Integer
        // Track whether picking has started (converted from WinForms isStarted)
        private bool isStarted = false;
        // Replaces isStarted logic to track barcode focus
        private bool isBarcode = true; // Replaces isStarted logic
        // Track summary mode (converted from WinForms isSummary)
        private int isSummary = 0;

        // Pick number
        private string pPickNo = ""; // <-- this replaces your WinForms pPickNo

        private long ID_SumHdr = 0; // Converted from Long

        private IDispatcherTimer tmrRequest;
        private int iRetry = 0;

        private List<PickQtyItem> pickList = new List<PickQtyItem>();

        private string txtpSKU_UPC = "";
        private string txtpSlot_Value = "";

        // Replaces txtStocker.Tag
        private string txtStockerTag = "";

        private object txtpCaseTag;  // replaces txtpCase.Tag
        private object txtpEachTag;  // replaces txtpEach.Tag
        private string txtpSlotTag; // replaces txtpSlot.Tag

        private bool _requestAlreadyShown = false;
        private bool _requestFailedShown = false;

        public ObservableCollection<BarcodeItem> barcodeList = new ObservableCollection<BarcodeItem>();
        public PickingPage()
        {
            InitializeComponent();// MAUI equivalent of VB InitializeComponent()
            NavigationPage.SetHasNavigationBar(this, false);

            // ===== Initialize the focus tracker =====
            txtboxFocus = txtBarcode; // Initially focus on barcode Entry

            lvBarcodes.ItemsSource = barcodeList;

            // ===== Timer for requests (replaces VB Timer) =====
            tmrRequest = Dispatcher.CreateTimer();
            tmrRequest.Interval = TimeSpan.FromSeconds(1);
            tmrRequest.Tick += TmrRequest_Tick;

            // ===== Page events =====
            Appearing += PickingPage_Appearing;
            Disappearing += PickingPage_Disappearing;

            // ===== Entry focus events (New TxtOther_GotFocus) =====
            txtpSKU.Focused += TxtOther_GotFocus;
            txtpDescr.Focused += TxtOther_GotFocus;
            txtpSlot.Focused += TxtOther_GotFocus;
            txtpEach.Focused += TxtOther_GotFocus;
            txtpCase.Focused += TxtOther_GotFocus;
            txtSKU.Focused += TxtOther_GotFocus;
            txtDone.Focused += TxtOther_GotFocus;
            txtDeptStore.Focused += TxtOther_GotFocus;

            // ===== Entry focus events (replaces VB GotFocus/LostFocus) =====
            txtBarcode.Focused += Entry_GotFocus;
            txtBarcode.Unfocused += Entry_LostFocus;

            txtSKU.Focused += Entry_GotFocus;
            txtSKU.Unfocused += Entry_LostFocus;

            txtCase.Focused += Entry_GotFocus;
            txtCase.Unfocused += Entry_LostFocus;

            txtEach.Focused += Entry_GotFocus;
            txtEach.Unfocused += Entry_LostFocus;

            txtpCase.Focused += Entry_GotFocus;
            txtpCase.Unfocused += Entry_LostFocus;

            txtpEach.Focused += Entry_GotFocus;
            txtpEach.Unfocused += Entry_LostFocus;

            txtStocker.Focused += Entry_GotFocus;
            txtStocker.Unfocused += Entry_LostFocus;

            txtDone.Focused += Entry_GotFocus;
            txtDone.Unfocused += Entry_LostFocus;

            // ===== TextChanged events (replaces VB TextChanged handlers) =====
            txtBarcode.TextChanged += TxtBarcode_TextChanged;
            txtDeptStore.TextChanged += TxtDeptStore_TextChanged;
            txtpSKU.TextChanged += TxtpSKU_TextChanged;
            txtpCase.TextChanged += TxtpCase_TextChanged;
            txtpEach.TextChanged += TxtpEach_TextChanged;
            txtEach.TextChanged += TxtEach_TextChanged;
            txtSKU.TextChanged += TxtSKU_TextChanged;
            txtCase.TextChanged += TxtCase_TextChanged;
            txtpSlot.TextChanged += TxtpSlot_TextChanged;
            txtDone.TextChanged += TxtDone_TextChanged;
            txtpDescr.TextChanged += TxtpDescr_TextChanged;

            // ===== Loaded events (replaces VB ParentChanged or Form Load) =====
            lblCase.Loaded += LblCase_Loaded;
            lblEach.Loaded += LblEach_Loaded;
            lblEach2.Loaded += LblEach2_Loaded;
            lblCase2.Loaded += LblCase2_Loaded;
            lblBarcode.Loaded += LblBarcode_Loaded;
            Gotolbl.Loaded += Gotolbl_Loaded;
            lblLineNo.Loaded += LblLineNo_Loaded;
            lblSKU.Loaded += LblSKU_Loaded;
            lblDone.Loaded += LblDone_Loaded;
            lblInput.Loaded += LblInput_Loaded;     // NEW: pnlConfirm inner label

            // --- llblDescr TapGestureRecognizer ---
            var descrTap = new TapGestureRecognizer();
            descrTap.Tapped += LlblDescr_Tapped; // Event handler with sender, e
            llblDescr.GestureRecognizers.Add(descrTap);

            // --- pnlSlots TapGestureRecognizer ---
            var slotsTap = new TapGestureRecognizer();
            slotsTap.Tapped += PnlSlots_Tapped;
            pnlSlots.GestureRecognizers.Add(slotsTap);

            // ===== Button click events (replaces VB Click handlers) =====
            btnCloseGoto.Clicked += BtnCloseGoto_Clicked;
            pbScanned.Clicked += PbScanned_Clicked;
            btnAccept.Clicked += BtnAccept_Clicked;
            btnCloseSlot.Clicked += BtnCloseSlot_Clicked;
            btnConfirm.Clicked += BtnConfirm_Clicked;
            btnCancel.Clicked += BtnCancel_Clicked;

            // ===== Navigation Buttons =====
            btnGoto.Clicked += BtnGoto_Clicked;
            btnPrev.Clicked += BtnPrev_Clicked;
            btnNext.Clicked += BtnNext_Clicked;

            txtBarcode.Completed += Entry_BarcodeAndQty_Completed;
            txtCase.Completed += Entry_BarcodeAndQty_Completed;
            txtEach.Completed += Entry_BarcodeAndQty_Completed;
            txtStocker.Completed += TxtStocker_Completed;

            //PART 2//

            // ===== Focus panel events =====
            pnlBarcodes.Focused += PnlBarcodes_Focused;
        }

        // ================== Form Properties ==================
        // Equivalent of frmPicking_Closing in MAUI
        private async void PickingPage_Appearing(object sender, EventArgs e)
        {
            // ------------------------------
            // Set user label using MAUI-compatible method
            // ------------------------------
            AppGlobal._SetUser(lblUser);

            // ------------------------------
            // Track barcode mode
            // ------------------------------
            isBarcode = true;

            // ------------------------------
            // Hide panels at start (equivalent to setting Location + Dock in WinForms)
            // In MAUI, XAML layout handles positioning. Just ensure visibility is correct
            // ------------------------------
            pnlConfirm.IsVisible = false;
            pnlBarcodes.IsVisible = false;
            pnlSlots.IsVisible = false;
            pnlGoto.IsVisible = false;

            // ------------------------------
            // Hide Finish button (WinForms: btnFinished.Hide())
            // ------------------------------
            btnFinished.IsVisible = false;

            // ------------------------------
            // Load current Pick Number
            // Equivalent of _GetSetPickNo() in VB.NET
            // Make it async if it involves database calls
            // ------------------------------
            await _GetSetPickNoAsync(); // Make sure _GetSetPickNoAsync is defined as Task-returning
        }

        // Equivalent of frmPicking_Closing in MAUI
        private void PickingPage_Disappearing(object sender, EventArgs e)
        {
            // Stop the timer if it exists
            tmrRequest?.Stop();
        }

        private void Entry_GotFocus(object sender, FocusEventArgs e)
        {
            txtboxFocus = sender as Entry;
            if (txtboxFocus == null) return;

            // If barcode or stocker Entry
            if (txtboxFocus == txtBarcode || txtboxFocus == txtStocker)
            {
                isBarcode = txtboxFocus == txtBarcode;
                txtboxFocus.BackgroundColor = Colors.PaleGreen; // Highlight focused Entry
            }
            else
            {
                // Select all text for other Entries
                txtboxFocus.CursorPosition = 0;
                txtboxFocus.SelectionLength = txtboxFocus.Text?.Length ?? 0;
            }
        }

        private void Entry_LostFocus(object sender, FocusEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;

            // Clear selection
            entry.SelectionLength = 0;

            // Reset background color for barcode or stocker Entry
            if (entry == txtBarcode || entry == txtStocker)
            {
                entry.BackgroundColor = Colors.WhiteSmoke;
            }
        }

        // This method ensures that focus is always returned to txtboxFocus
        private void TxtOther_GotFocus(object sender, FocusEventArgs e)
        {
            // Force focus back to the tracked Entry (barcode / qty entry)
            txtboxFocus?.Focus();
        }

        // ✅ Per-character numeric validation (like KeyPress in WinForms)
        private void Entry_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not Entry entry) return;

            foreach (char c in e.NewTextValue)
            {
                if (AppGlobal._isAllowedNum(c) == '\0') // ❌ invalid char
                {
                    entry.Text = e.OldTextValue; // revert to old value
                    break;
                }
            }
        }

        // ✅ Handle Enter key / Completed event
        private async void Entry_BarcodeAndQty_Completed(object sender, EventArgs e)
            {
                if (sender is not Entry entry) return;

                // Validate that all characters are allowed
                if (entry.Text != null && entry.Text.Any(c => AppGlobal._isAllowedNum(c) == '\0'))
                {
                    entry.Text = "";
                    return;
                }

                if (entry == txtBarcode)
                {
                    await GetSKUDescrAsync(); // async SKU lookup
                }
                else if (entry == txtCase)
                {
                    txtEach.Focus();
                    txtEach.CursorPosition = 0;
                    txtEach.SelectionLength = txtEach.Text?.Length ?? 0;
                }
                else if (entry == txtEach)
                {
                    BtnAccept_Clicked(null, null);
                }
            }

    // Optional: Escape key (hardware keyboards)
    private void HandleEscapeKey()
        {
            if (Navigation.NavigationStack.Count > 1)
                Navigation.PopAsync(); // Close current page
            else
                DisplayAlert("Exit", "Cannot close page in navigation stack.", "OK");
        }

        // Focus logic for pnlBarcodes (converted GotFocus)
        private void PnlBarcodes_Focused(object sender, FocusEventArgs e)
        {
            // Focus the first Entry inside the panel, if any
            if (pnlBarcodes.Content is Microsoft.Maui.Controls.Layout layout && layout.Children.FirstOrDefault() is Entry firstEntry)
            {
                firstEntry.Focus();
            }
        }

        private void TxtBarcode_TextChanged(object sender, TextChangedEventArgs e) { }
        private void LblBarcode_ParentChanged(object sender, EventArgs e) { }
        private void LblDone_ParentChanged(object sender, EventArgs e) { }
        private void TxtpSlot_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtDone_TextChanged(object sender, TextChangedEventArgs e) { }

        private void TxtStocker_TextChanged(object sender, TextChangedEventArgs e)
        {
            var txt = txtStocker.Text;
            if (!string.IsNullOrEmpty(txt))
            {
                // Keep only allowed numeric characters
                var filtered = new string(txt.Where(c => AppGlobal._isAllowedNum(c) != '\0').ToArray());
                if (txt != filtered)
                {
                    txtStocker.Text = filtered;
                    txtStocker.CursorPosition = txtStocker.Text.Length;
                }
            }
        }


        private async Task GetSKUDescrAsync()
        {
            var currentSKU = txtpSKU.Text?.Trim();
            if (!string.IsNullOrEmpty(currentSKU))
            {
                var item = pickList.Find(p => p.SKU == currentSKU);
                llblDescr.Text = item?.Descr ?? "";

                if (item != null && item.Slot.Contains(","))
                {
                    txtpSlot.Text = "<< Multiple Slots >>";
                    txtpSlot_Value = item.Slot;
                }
                else
                {
                    txtpSlot.Text = item?.Slot ?? "";
                    txtpSlot_Value = "";
                }

                llblSlot.Text = txtpSlot.Text;
            }
            else
            {
                llblDescr.Text = "";
                txtpSlot.Text = "";
                txtpSlot_Value = "";
                llblSlot.Text = "";
            }
        }

        private async Task _GetSetPickNoAsync()
        {
            // ------------------- UI updates -------------------
            btnFinished.IsVisible = false;
            pbReq.IsVisible = true;

            int hasUnfinishedTrf = 0;
            string sUserPNo = "";

            // ------------------- SQL Connection -------------------
            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                await DisplayAlert("No Connection!", "Cannot connect to server! Please retry or check settings...", "OK");
                await Navigation.PopAsync();
                return;
            }

            try
            {
                // ------------------- Get user info -------------------
                using (var sqlCmd = new SqlCommand($"SELECT ID_SumHdr, PickRef FROM tblUsers WHERE ID={AppGlobal.ID_User}", conn))
                using (var reader = await sqlCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        AppGlobal.ID_SumHdr = reader["ID_SumHdr"] != DBNull.Value
                            ? Convert.ToInt32(reader["ID_SumHdr"])
                            : 0;
                        // PickRef
                        if (reader["PickRef"] != DBNull.Value)
                        {
                            if (Convert.ToInt64(reader["PickRef"]) != 0)
                            {
                                sUserPNo = reader["PickRef"].ToString().Trim();
                            }
                        }
                    }
                }
                // ================= UNFINISHED PICKING (VB COMMENTED – KEEP COMMENTED) =================
                /*
                HasUnfinishedTrf = await AppGlobal._CheckUnfinishedPicking(
                    AppGlobal.ID_User,
                    Convert.ToInt32(sUserPNo)
                );

                if (HasUnfinishedTrf != 0)
                {
                    AppGlobal.ID_SumHdr = HasUnfinishedTrf;
                }
                */

                // ------------------- Picking status check -------------------
                if (AppGlobal.ID_SumHdr != 0 && sUserPNo == AppGlobal.pPickNo)
                {
                    await _AddSKUtoListAsync(); // Load SKUs
                    return;
                }

                // ------------------- Request from server (SHOW ONCE ONLY) -------------------
                if (!_requestAlreadyShown)
                {
                    _requestAlreadyShown = true;

                    bool requestFromServer = await DisplayAlert(
                        "Requesting...",
                        "Request from server?",
                        "Yes",
                        "No");

                    if (requestFromServer)
                    {
                        using (var updateCmd = new SqlCommand(
                            $"UPDATE tblUsers SET isRequest=1, isSummary={AppGlobal.isSummary}, PickRef={AppGlobal.pPickNo} WHERE ID={AppGlobal.ID_User}",
                            conn))
                        {
                            await updateCmd.ExecuteNonQueryAsync();
                        }

                        tmrRequest.Start();
                    }
                    else
                    {
                        await Navigation.PopAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
                await Navigation.PopAsync();
            }
            finally
            {
                pbReq.IsVisible = false;
            }
        }

        /// <summary>
        /// Reads the barcode and checks it against the database (bigint UPC).
        /// Mirrors the original VB.NET GetSKUDescr_WIFI.
        /// </summary>
        private async Task GetSKUDescr_WIFIAsync()
        {
            // 1️⃣ Exit early if barcode is empty
            if (string.IsNullOrWhiteSpace(txtBarcode.Text))
                return;

            // 2️⃣ Parse barcode as long (bigint)
            if (!long.TryParse(txtBarcode.Text.Trim(), out long barcodeValue))
            {
                await DisplayAlert("Error", "Invalid barcode number", "OK");
                txtBarcode.Focus();
                txtBarcode.SelectionLength = txtBarcode.Text?.Length ?? 0;
                return;
            }

            // 3️⃣ Ensure SQL connection is ready
            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                await DisplayAlert("Error", "Cannot connect to server!", "OK");
                return;
            }

            // 4️⃣ Clear previous scan (like VB _ClearScan(False))
            _ClearScan(false);

            try
            {
                using var sqlCmd = conn.CreateCommand();
                // 5️⃣ Parameterized query for bigint UPC
                sqlCmd.CommandText = "SELECT CSKU FROM invMST WHERE UPC = @UPC";
                sqlCmd.Parameters.AddWithValue("@UPC", barcodeValue);

                using var reader = await sqlCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync()) // If barcode exists
                {
                    string sSKU = reader["CSKU"].ToString().Trim();

                    if (sSKU == txtpSKU.Text.Trim()) // Match with expected SKU
                    {
                        // Populate fields
                        txtSKU.Text = txtpSKU.Text;
                        txtCase.Text = txtpCase.Text;
                        txtEach.Text = txtpEach.Text;
                        txtBarcode.SelectionLength = 0;

                        // Decide which Entry to focus
                        if (!double.TryParse(txtCase.Text, out double caseVal) || caseVal == 0)
                        {
                            txtEach.Focus();
                            txtEach.CursorPosition = 0;
                            txtEach.SelectionLength = txtEach.Text?.Length ?? 0;
                        }
                        else
                        {
                            txtCase.Focus();
                            txtCase.CursorPosition = 0;
                            txtCase.SelectionLength = txtCase.Text?.Length ?? 0;
                        }
                    }
                    else
                    {
                        await DisplayAlert("Mismatch!", "Wrong scanned item!", "OK");
                        _ClearScan(); // clears all fields
                    }
                }
                else // Item not found
                {
                    await DisplayAlert("Error!", "Item not found!", "OK");
                    txtBarcode.Focus();
                    txtBarcode.SelectionLength = txtBarcode.Text?.Length ?? 0;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Database Error", ex.Message, "OK");
                txtBarcode.Focus();
                txtBarcode.SelectionLength = txtBarcode.Text?.Length ?? 0;
            }
        }


        /// <summary>
        /// Clears scanned input and optionally clears all related fields.
        /// </summary>
        /// <param name="clearAll">If true, clears all fields; otherwise clears only barcode.</param>
        private void _ClearScan(bool bWithBarcode = true)
        {
            // Clear barcode input if requested
            if (bWithBarcode)
                txtBarcode.Text = string.Empty;

            // Clear other fields
            txtSKU.Text = string.Empty;
            txtEach.Text = "0";
            txtCase.Text = "0";

            // Reset cursor position (selection is not supported)
            txtEach.CursorPosition = 0;
            txtCase.CursorPosition = 0;

            // Focus the barcode entry
            _ = txtBarcode.Focus();

            // Simulate SelectAll for MAUI Entry
            txtBarcode.CursorPosition = 0;
            txtBarcode.SelectionLength = txtBarcode.Text?.Length ?? 0;

            txtEach.CursorPosition = 0;
            txtEach.SelectionLength = txtEach.Text?.Length ?? 0;

            txtCase.CursorPosition = 0;
            txtCase.SelectionLength = txtCase.Text?.Length ?? 0;
        }

        private async void BtnAccept_Clicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtEach.Text) && !string.IsNullOrEmpty(txtCase.Text) &&
                double.TryParse(txtEach.Text, out double eachQty) && eachQty >= 0 &&
                double.TryParse(txtCase.Text, out double caseQty) && caseQty >= 0)
            {
                if (eachQty == 0 && caseQty == 0)
                {
                    bool acceptZero = await DisplayAlert("Accept?",
                        "You have entered 0 in quantity.\nMeaning, the item is not available.\n\nAccept 0 quantity?",
                        "Yes", "No");

                    if (acceptZero)
                    {
                        pnlNavigate.IsVisible = false;
                        pnlInput.IsVisible = false;

                        txtStocker.Text = "";
                        txtStockerTag = ""; // replacing .Tag

                        txtStocker.IsReadOnly = await AppGlobal._CheckOption_StockerAsync();
                        pnlConfirm.IsVisible = true;
                        txtStocker.Focus();
                    }
                }
                else if (txtSKU.Text.Trim() != "" && txtSKU.Text == txtpSKU.Text)
                {
                    bool acceptQty = await DisplayAlert("Accept?", "Accept quantity?", "Yes", "No");
                    if (acceptQty)
                    {
                        _AcceptItemAsync();
                    }
                }
            }
        }

        // Confirm button
        private async void BtnConfirm_Clicked(object sender, EventArgs e)
        {
            if (txtStockerTag != null)
            {
                // Cast object to string
                string stockerText = txtStockerTag.ToString();

                if (int.TryParse(stockerText, out int stockerId))
                {
                    ID_Stocker = stockerId;
                    await DisplayAlert("Item Confirmed!", "OK", "OK");

                    BtnCancel_Clicked(null, null); // exactly like VB.NET
                    await _AcceptItemAsync();
                }
            }
        }

        // Cancel button
        private void BtnCancel_Clicked(object sender, EventArgs e)
        {
            pnlConfirm.IsVisible = false; // only hide confirm
            pnlNavigate.IsVisible = true;
            pnlInput.IsVisible = true;

            txtBarcode.Focus();
        }
        private void TxtStocker_Completed(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtStocker.Text))
            {
                ConfirmStockerAsync(); 
            }
        }
        private async void BtnFinished_Clicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Finish?", "Finish Picking?", "Yes", "No");
            if (!confirm) return;

            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                await DisplayAlert("Error", "Cannot connect to server!", "OK");
                return;
            }

            using var sqlCmd = conn.CreateCommand();

            // Loop through your pick list
            foreach (var lvItem in pickList)
            {
                if (lvItem.PickedQty == 0)
                {
                    sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET " +
                                         "SortQty=0, " +
                                         "isSorted=1, " +
                                         "isUpdate=1, " +
                                         $"pickTime='00:00:00', " +
                                         $"TSort_Start='00:00:00', " +
                                         $"TSort_End='00:00:00' " +
                                         $"WHERE SKU='{lvItem.SKU}' AND ID_SumHdr={ID_SumHdr}";
                    await sqlCmd.ExecuteNonQueryAsync();
                }
            }

            // Update PickHdr table
            sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickHdr SET " +
                                 $"isUpdate=1, " +
                                 $"TimeEnd='{await AppGlobal._GetDateTime()}', " +
                                 $"DateDone='{await AppGlobal._GetDateTime(true)}' " +
                                 $"WHERE ID={ID_SumHdr}";
            await sqlCmd.ExecuteNonQueryAsync();

            // Reset user summary
            sqlCmd.CommandText = $"UPDATE tblUsers SET ID_SumHdr=0 WHERE ID={AppGlobal.ID_User}";
            await sqlCmd.ExecuteNonQueryAsync();

            // Refresh PickNo
            await _GetSetPickNoAsync();
        }

        private void TxtStockerConfirm_Completed(object sender, EventArgs e) { }
        private void BtnCloseGoto_Clicked(object sender, EventArgs e)
        {
            pnlGoto.IsVisible = false;
        }


        // Event: TextChanged (optional) or Completed for Enter
        private void TxtLine_Completed(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtLine.Text) || !double.TryParse(txtLine.Text, out double lineNum) || lineNum == 0)
                return;

            if (lineNum > pickList.Count)
            {
                DisplayAlert("! ! !", "Line Number out of range!", "OK");
                txtLine.Focus();
                txtLine.CursorPosition = 0;
                txtLine.SelectionLength = txtLine.Text.Length;
                return;
            }

            pnlGoto.IsVisible = false;
            sSKU = (int)lineNum - 1;
            _GetSKUtoPickAsync();
        }

        public void OnF1Pressed()
        {
            // If GoTo line is focused
            if (txtLine.IsFocused)
            {
                pnlGoto.IsVisible = false;
                txtBarcode.Focus();
                return;
            }

            // If stocker input is focused
            if (txtStocker.IsFocused)
            {
                BtnCancel_Clicked(null, null); // Cancel stocker input
                return;
            }

            // F1 pressed anywhere else → exit page
            if (Navigation.NavigationStack.Count > 0)
            {
                MainThread.BeginInvokeOnMainThread(async () => await Navigation.PopAsync());
            }
        }


        // Optional: Numeric-only input (per character)
        private void TxtLine_TextChanged(object sender, TextChangedEventArgs e)
        {
            var txt = txtLine.Text;
            if (!string.IsNullOrEmpty(txt))
            {
                // Keep only allowed characters
                txtLine.Text = new string(txt.Where(c => AppGlobal._isAllowedNum(c) != '\0').ToArray());
                txtLine.CursorPosition = txtLine.Text.Length;
            }
        }
        private void TxtpDescr_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Your logic here
        }

        private void InitRequestTimer()
        {
            tmrRequest = Dispatcher.CreateTimer();
            tmrRequest.Interval = TimeSpan.FromSeconds(5); // EXACT VB.NET behavior
            tmrRequest.Tick += TmrRequest_Tick;
            tmrRequest.Start();
        }

        private async void TmrRequest_Tick(object sender, EventArgs e)
        {
            try
            {
                using var conn = await AppGlobal._SQL_Connect();
                if (conn == null) return;

                // 1️⃣ Check if request has been sent
                using (var cmd = new SqlCommand(
                    "SELECT * FROM tblUsers WHERE ID=@ID AND ID_SumHdr<>0", conn))
                {
                    cmd.Parameters.AddWithValue("@ID", AppGlobal.ID_User);

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        tmrRequest.Stop();
                        AppGlobal.ID_SumHdr = Convert.ToInt32(reader["ID_SumHdr"]);
                        await _AddSKUtoListAsync();
                        return;
                    }
                }

                // 2️⃣ Retry logic (5 seconds interval)
                if (iRetry >= 5)
                {
                    tmrRequest.Stop();
                    iRetry = 0;

                    if (!_requestFailedShown)
                    {
                        _requestFailedShown = true;

                        using (var resetCmd = new SqlCommand(
                        "UPDATE tblUsers SET isRequest=0, isSummary=0 WHERE ID=@ID", conn))
                        {
                            resetCmd.Parameters.AddWithValue("@ID", AppGlobal.ID_User);
                            await resetCmd.ExecuteNonQueryAsync();
                        }

                        await DisplayAlert("Unable to request!", "No Picking No. available!", "OK");
                        await Navigation.PopAsync();
                    }
                }
                else
                {
                    iRetry++;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error!", ex.Message, "OK");
            }
        }

        private async Task ConfirmStockerAsync()
        {
            // 1️⃣ Check if the Stocker textbox is empty
            if (string.IsNullOrWhiteSpace(txtStocker.Text))
                return;

            // 2️⃣ Ensure SQL connection
            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                await DisplayAlert("Error", "Cannot connect to server!", "OK");
                return;
            }

            try
            {
                // 3️⃣ Query stocker info
                using var sqlCmd = conn.CreateCommand();
                sqlCmd.CommandText = @"
            SELECT ID, (LName + ', ' + FName + ' ' + MI) AS FullName
            FROM tblUsers
            WHERE EENo = @EENo AND isStocker = 1 AND isActive = 1";
                sqlCmd.Parameters.AddWithValue("@EENo", txtStocker.Text.Trim());

                using var reader = await sqlCmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    // 4️⃣ Assign stocker ID and tag
                    ID_Stocker = Convert.ToInt32(reader["ID"]);
                    txtStockerTag = reader["ID"].ToString().Trim();

                    // 5️⃣ Show full name
                    await DisplayAlert("Stocker Name:", reader["FullName"].ToString().Trim(), "OK");

                    // 6️⃣ Focus confirm button
                    btnConfirm.Focus();
                }
                else
                {
                    // 7️⃣ Stocker not found
                    txtStockerTag = "";
                    await DisplayAlert("Not Found!", "Stocker ID not found!", "OK");

                    // 8️⃣ Reset focus and select all text
                    txtStocker.Focus();
                    txtStocker.CursorPosition = 0;
                    txtStocker.SelectionLength = txtStocker.Text?.Length ?? 0;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private void TxtDeptStore_TextChanged(object sender, TextChangedEventArgs e)
        {
            AppGlobal.DeptStore = txtDeptStore.Text;
        }

        private void TxtpSKU_TextChanged(object sender, TextChangedEventArgs e)
        {
            var currentSKU = txtpSKU.Text?.Trim();
            llblDescr.Text = pickList.Find(p => p.SKU == currentSKU)?.Descr ?? "";
        }

        private void TxtpCase_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtpEach_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtEach_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtSKU_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtCase_TextChanged(object sender, TextChangedEventArgs e) { }

        private void PbScanned_Clicked(object sender, EventArgs e)
        {
            DisplayAlert("Scanned", "pbScanned clicked!", "OK");
        }

        private void LblCase_Loaded(object sender, EventArgs e) { }
        private void LblEach_Loaded(object sender, EventArgs e) { }
        private void LblEach2_Loaded(object sender, EventArgs e) { }
        private void LblCase2_Loaded(object sender, EventArgs e) { }
        private void LblBarcode_Loaded(object sender, EventArgs e) { }
        private void LblSKU_Loaded(object sender, EventArgs e) { }
        private void LblDone_Loaded(object sender, EventArgs e) { }
        private void Gotolbl_Loaded(object sender, EventArgs e) { }
        private void LblLineNo_Loaded(object sender, EventArgs e) { }
        private void LblBarcodeTitle_Loaded(object sender, EventArgs e)
        {
            // Code that should run when the label is added to the visual tree
        }

        private void LlblDescr_Tapped(object sender, EventArgs e)
        {
            // Clear previous barcode items
            barcodeList.Clear();

            // --- Local UPCs (from txtpSKU_UPC) ---
            if (!string.IsNullOrWhiteSpace(txtpSKU?.Text) && !string.IsNullOrWhiteSpace(txtpSKU_UPC))
            {
                var upcs = txtpSKU_UPC.Split(',', StringSplitOptions.RemoveEmptyEntries);

                foreach (var sUPC in upcs)
                {
                    var cleaned = sUPC.Replace("-", "").Trim();
                    if (!string.IsNullOrEmpty(cleaned))
                    {
                        barcodeList.Add(new BarcodeItem
                        {
                            Text = "",    // placeholder like SubItem in WinForms
                            UPC = cleaned
                        });
                    }
                }
            }

            // --- Optional: For Online UPC fetch (commented) ---
            /*
            Task.Run(async () =>
            {
                if (!await AppGlobal.ConnectSqlAsync()) return;

                var dsData = await _WorkQueryAsync($"SELECT UPC FROM invMST WHERE CSKU='{txtpSKU.Text}'");

                foreach (DataRow dRow in dsData.Tables[0].Rows)
                {
                    var upc = dRow["UPC"].ToString().Trim();
                    if (!string.IsNullOrEmpty(upc))
                        barcodeList.Add(new BarcodeItem { Text = "", UPC = upc });
                }

                dsData.Tables.Clear();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    pnlBarcodes.IsVisible = true;
                });
            });
            */

            // Show barcode panel
            pnlBarcodes.IsVisible = true;

            // Focus the first Entry inside the panel if it exists
            if (pnlBarcodes.Content is Microsoft.Maui.Controls.Layout layout && layout.Children.FirstOrDefault() is Entry firstEntry)
            {
                firstEntry.Focus();
            }
        }
        private void SetUPC_FromPickItem(PickQtyItem item)
        {
            if (item == null)
            {
                txtpSKU_UPC = "";
                return;
            }

            // If your database stores UPCs separated by commas
            txtpSKU_UPC = item.Descr.Contains(",") ? item.Descr : item.Slot;
        }

        private void LlblSlot_Tapped(object sender, EventArgs e)
        {
            // Only proceed if multiple slots exist
            if (txtpSlot.Text == "<< Multiple Slots >>" && !string.IsNullOrEmpty(txtpSlot_Value))
            {
                // Split slots from stored comma-separated string
                var sSlots = txtpSlot_Value.Split(',', StringSplitOptions.RemoveEmptyEntries);

                // Convert to SlotItem objects for CollectionView/ListView binding
                lvSlots.ItemsSource = sSlots.Select(s => new SlotItem { Slot = s.Trim() }).ToList();

                // Show the slots panel
                pnlSlots.IsVisible = true;
            }
        }

        // When a slot is selected from CollectionView
        private void LvSlots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedSlot = e.CurrentSelection.FirstOrDefault() as SlotItem;
            if (selectedSlot != null)
            {
                txtpSlot.Text = selectedSlot.Slot;
                pnlSlots.IsVisible = false;

                // Clear selection for next use
                lvSlots.SelectedItem = null;
            }
        }

        private void LvSKU_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // e.CurrentSelection gives you the selected item(s)
            if (e.CurrentSelection != null && e.CurrentSelection.Count > 0)
            {
                var selectedItem = e.CurrentSelection[0] as PickQtyItem;
                if (selectedItem != null)
                {
                    // Do something with the selected SKU item
                    sSKU = pickList.IndexOf(selectedItem); // update sSKU index if needed
                    _GetSKUtoPickAsync(); // refresh the fields for the selected item
                }
            }
            else
            {
                // No selection, reset sSKU if needed
                sSKU = -1;
            }
        }

        private void LblMultipleSlots_Loaded(object sender, EventArgs e) { }
        private void PnlSlots_Tapped(object sender, TappedEventArgs e) { }
        private void BtnCloseSlot_Clicked(object sender, EventArgs e)
        {
            pnlSlots.IsVisible = false;
        }

        private async void BtnNavigate_Clicked(object sender, EventArgs e)
        {
            if (sender is not Button btn) return;

            switch (btn.StyleId)
            {
                case "btnGoto":
                    txtLine.Text = string.Empty;
                    pnlGoto.IsVisible = true;
                    btnFirst.Focus();
                    return;

                case "btnFirst":
                    sSKU = 0;
                    pnlGoto.IsVisible = false;
                    break;

                case "btnPrev":
                    sSKU = Math.Max(0, sSKU - 1);
                    break;

                case "btnNext":
                    sSKU = Math.Min(pickList.Count - 1, sSKU + 1);
                    break;

                case "btnLast":
                    sSKU = pickList.Count - 1;
                    pnlGoto.IsVisible = false;
                    break;

                case "btnUnpick":
                    sSKU = -1; // ✅ correct replacement for VB empty string
                    pnlGoto.IsVisible = false;
                    break;
            }
            await _GetSKUtoPickAsync();
        }

        private void BtnGoto_Clicked(object sender, EventArgs e) => pnlGoto.IsVisible = true;
        private void BtnPrev_Clicked(object sender, EventArgs e) { }
        private void BtnNext_Clicked(object sender, EventArgs e) { }
        private void PnlGoto_Focused(object sender, FocusEventArgs e) { }

        private void BtnClose_Clicked(object sender, EventArgs e)
        {
            pnlBarcodes.IsVisible = false;
            txtboxFocus.Focus();
        }
        private void LblUser_Loaded(object sender, EventArgs e)
        {
            // This is similar to ParentChanged in WinForms
            // Add any initialization or logic that needs the label to be in the visual tree
        }

        //PART 3//

        // ================= CLASSES ======================
        public class BarcodeItem { public string Text { get; set; } = ""; public string UPC { get; set; } = ""; }
        public class SlotItem { public string Text { get; set; } = ""; public string Slot { get; set; } = ""; }
        public class PickQtyItem
        {
            public int ID { get; set; }
            public string SKU { get; set; } = "";
            public string Descr { get; set; } = "";
            public string Slot { get; set; } = "";
            public string BUM { get; set; } = "";
            public string UPC { get; set; } = "";
            public double Qty { get; set; }
            public double PickedQty { get; set; }
            // For UI to show empty string when not picked (matches your VB behavior)
            public string PickedQtyDisplay { get; set; } = "";
            public string DisplayQty => PickedQty.ToString("N2");
        }

        private async Task _AcceptItemAsync()
        {
            // 1️⃣ Connect to SQL
            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null) return;

            using var sqlCmd = conn.CreateCommand();

            // 2️⃣ Calculate picked quantity using txtpCaseTag and txtpEachTag
            double caseTag = Convert.ToDouble(txtpCaseTag ?? 0);  // replaces txtpCase.Tag
            double caseQty = double.TryParse(txtCase.Text, out double cQty) ? cQty : 0;
            double eachQty = double.TryParse(txtEach.Text, out double eQty) ? eQty : 0;
            double dQty = (caseTag * caseQty) + eachQty;

            // 3️⃣ Get selected SKU item
            if (sSKU == -1 || lvSKU.SelectedItem == null) return;
            var lvItem = lvSKU.SelectedItem as PickQtyItem;
            if (lvItem == null) return;

            lvItem.PickedQty = dQty;

            // 4️⃣ Update PickHdr
            if (!isStarted)
            {
                isStarted = true;
                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickHdr SET isUpdate=1, TimeStart='{await AppGlobal._GetDateTime()}' WHERE ID={ID_SumHdr}";
            }
            else
            {
                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickHdr SET isUpdate=1 WHERE ID={ID_SumHdr}";
            }
            await sqlCmd.ExecuteNonQueryAsync();

            // 5️⃣ Prepare UPC update
            string sUPC = "";
            if (!pbScanned.IsVisible && !string.IsNullOrEmpty(txtBarcode.Text))
                sUPC = $"UPC={txtBarcode.Text},";
            if (eachQty == 0 && caseQty == 0)
                sUPC = "";

            // 6️⃣ Update PickDtl
            if (isSummary == 1)
            {
                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET {sUPC}PickBy={AppGlobal.ID_User}, ConfBy={ID_Stocker}, PickTime='{await AppGlobal._GetDateTime()}' " +
                                     $"WHERE SKU='{lvItem.SKU}' AND ID_SumHdr={ID_SumHdr}";
                await sqlCmd.ExecuteNonQueryAsync();
            }
            else
            {
                var dsData = await _WorkQueryAsync($"SELECT ID, Qty FROM tbl{pPickNo}PickDtl WHERE SKU='{lvItem.SKU}' AND ID_SumHdr={ID_SumHdr}");

                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET {sUPC}SortTime='{await AppGlobal._GetDateTime()}', SortBy={AppGlobal.ID_User}, PickBy={AppGlobal.ID_User}, ConfBy={ID_Stocker}, PickTime='{await AppGlobal._GetDateTime()}' " +
                                     $"WHERE SKU='{lvItem.SKU}' AND ID_SumHdr={ID_SumHdr}";
                await sqlCmd.ExecuteNonQueryAsync();

                int lCount = dsData.Tables[0].Rows.Count - 1;

                for (int iCount = 0; iCount <= lCount; iCount++)
                {
                    var dRow = dsData.Tables[0].Rows[iCount];
                    double dNeedQty = Convert.ToDouble(dRow["Qty"]);

                    if (iCount == lCount)
                    {
                        sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET isSorted=1, SortQty={dQty}, isUpdate=1 WHERE ID={dRow["ID"]}";
                        await sqlCmd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        if (dQty >= dNeedQty)
                        {
                            sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET isSorted=1, SortQty=Qty, isUpdate=1 WHERE ID={dRow["ID"]}";
                            await sqlCmd.ExecuteNonQueryAsync();
                            dQty -= dNeedQty;
                        }
                        else
                        {
                            sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET isSorted=1, SortQty={dQty}, isUpdate=1 WHERE ID={dRow["ID"]}";
                            await sqlCmd.ExecuteNonQueryAsync();
                            dQty = 0;
                        }
                    }
                }
            }

            // 7️⃣ Update PickQty
            sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickQty SET isPicked=1, PickQty={lvItem.PickedQty} WHERE ID={lvItem.ID}";
            await sqlCmd.ExecuteNonQueryAsync();

            // 8️⃣ Move to next SKU
            ID_Stocker = 0;
            if (btnNext.IsEnabled)
            {
                BtnNavigate_Clicked(btnNext, null);
            }
            else
            {
                sSKU = -1;
                await _GetSKUtoPickAsync(); // find next unpicked SKU like in VB.NET
            }
        }

        private async Task _GetSKUtoPickAsync()
        {
            _ClearScan();

            if (pickList == null || pickList.Count == 0) return;

            PickQtyItem currentItem = null;

            // Find first unpicked SKU if sSKU = -1
            if (sSKU == -1)
            {
                for (int i = 0; i < pickList.Count; i++)
                {
                    if (pickList[i].PickedQty == 0)
                    {
                        sSKU = i;
                        break;
                    }
                }
            }

            if (sSKU < 0 || sSKU >= pickList.Count) return;

            currentItem = pickList[sSKU];

            // Slots
            if (currentItem.Slot.Contains(","))
            {
                txtpSlot.Text = "<< Multiple Slots >>";
                txtpSlotTag = currentItem.Slot; // Replacing Tag
            }
            else
            {
                txtpSlot.Text = currentItem.Slot;
            }
            llblSlot.Text = txtpSlot.Text;

            // SKU
            txtpSKU.Text = currentItem.SKU;
            txtpSKU_UPC = currentItem.UPC;

            // Description
            txtpDescr.Text = currentItem.Descr;
            llblDescr.Text = txtpDescr.Text;

            // Quantities
            txtpEachTag = currentItem.Qty;  // replaces Tag
            txtpCaseTag = currentItem.BUM;  // replaces Tag

            double dSetQty = Convert.ToDouble(txtpCaseTag ?? 0);
            double eachQty = Convert.ToDouble(txtpEachTag ?? 0);

            txtCase.IsEnabled = dSetQty != 1;

            if (dSetQty == 1 || eachQty < dSetQty)
            {
                txtpCase.Text = "0";
                txtpEach.Text = eachQty.ToString("N2");
            }
            else
            {
                txtpCase.Text = Math.Floor(eachQty / dSetQty).ToString("N2");
                txtpEach.Text = (eachQty % dSetQty).ToString("N2");
            }

            // Picked Item
            pbScanned.IsVisible = currentItem.PickedQty > 0;
            if (currentItem.PickedQty > 0)
            {
                txtSKU.Text = txtpSKU.Text;
                double pickedQty = currentItem.PickedQty;

                if (dSetQty == 1 || pickedQty < dSetQty)
                {
                    txtCase.Text = "0";
                    txtEach.Text = pickedQty.ToString("N2");
                }
                else
                {
                    txtCase.Text = Math.Floor(pickedQty / dSetQty).ToString("N2");
                    txtEach.Text = (pickedQty % dSetQty).ToString("N2");
                }
            }

            // Navigation Buttons
            btnFirst.IsEnabled = btnPrev.IsEnabled = btnNext.IsEnabled = btnLast.IsEnabled = false;

            if (sSKU > 0)
            {
                btnPrev.IsEnabled = true;
                btnFirst.IsEnabled = true;
            }
            if (sSKU < pickList.Count - 1)
            {
                btnNext.IsEnabled = true;
                btnLast.IsEnabled = true;
            }

            // Optional: If you have any async logic inside _CountPicked, await it
            await Task.Run(() => _CountPicked());
        }

        private void _CountPicked()
        {
            int iPicked = 0;

            if (pickList != null)
            {
                foreach (var item in pickList)
                {
                    if (item.PickedQty > 0) // equivalent of SubItems(5).Text.Trim <> ""
                        iPicked++;
                }

                txtDone.Text = $"{iPicked}/{pickList.Count}";

                // If all SKU have been picked
                if (iPicked == pickList.Count)
                {
                    btnFinished.IsVisible = true;
                }
            }

            pbReq.IsVisible = false;
        }


        private async Task<DataSet> _WorkQueryAsync(string sql)
        {
            var ds = new DataSet();

            // 1️⃣ Connect to SQL
            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
                return ds; // return empty DataSet if connection fails

            // 2️⃣ Execute query
            using var cmd = new SqlCommand(sql, conn);
            using var adapter = new SqlDataAdapter(cmd);

            System.Diagnostics.Debug.WriteLine("SQL2: " + sql);
            adapter.Fill(ds);

            return ds;
        }

        private async Task _AddSKUtoListAsync()
        {
            // 1️⃣ Connect to SQL
            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null) return; // connection failed

            try
            {
                // 2️⃣ Load PickHdr
                using (var cmdHdr = new SqlCommand($"SELECT * FROM tbl{pPickNo}PickHdr WHERE ID={ID_SumHdr} AND TimeEnd='0'", conn))
                using (var readerHdr = await cmdHdr.ExecuteReaderAsync())
                {
                    if (await readerHdr.ReadAsync())
                    {
                        isStarted = readerHdr["TimeStart"].ToString().Trim() != "0";

                        // Get department/store info **after closing the reader**
                        var deptId = Convert.ToInt32(readerHdr["iDept"]);
                        readerHdr.Close(); // ✅ close before next SQL

                        if (isSummary == 1)
                        {
                            lblDeptStore.Text = "Department:";
                            txtDeptStore.Text = await AppGlobal._GetDeptName(deptId); // async call safe
                        }
                        else
                        {
                            lblDeptStore.Text = "Store No:";
                            txtDeptStore.Text = await AppGlobal._GetStoreNo(); // local method
                        }
                    }
                }

                // 3️⃣ Load PickQty items
                var dsData = await _WorkQueryAsync($"SELECT * FROM tbl{pPickNo}PickQty WHERE ID_SumHdr={ID_SumHdr} ORDER BY Slot, SKU");

                pickList.Clear();
                foreach (DataRow dRow in dsData.Tables[0].Rows)
                {
                    var item = new PickQtyItem
                    {
                        ID = Convert.ToInt32(dRow["ID"]),
                        BUM = dRow["BUM"].ToString().Trim(),
                        Slot = dRow["Slot"].ToString().Trim(),
                        SKU = dRow["SKU"].ToString().Trim(),
                        Descr = dRow["Descr"].ToString().Trim(),
                        Qty = Convert.ToDouble(dRow["Qty"]),
                        UPC = dRow["UPC"].ToString().Trim(),

                        PickedQty = dRow["isPicked"].ToString().Trim() == "0"
                            ? 0
                            : Convert.ToDouble(dRow["PickQty"]),

                        PickedQtyDisplay = dRow["isPicked"].ToString().Trim() == "0"
                            ? ""
                            : dRow["PickQty"].ToString().Trim()
                    };

                    pickList.Add(item);
                }

                // 4️⃣ Bind to UI
                lvSKU.ItemsSource = pickList;

                // 5️⃣ Reset sSKU to -1 to find first unpicked item
                sSKU = -1;

                // 6️⃣ Load next SKU to pick
                await _GetSKUtoPickAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // pnlConfirm label handlers
        private void LblConfirmTitle_Loaded(object sender, EventArgs e) { }
        private void LblConfirm_Loaded(object sender, EventArgs e) { }
        private void LblInput_Loaded(object sender, EventArgs e)
        {
            // Code for lblInput inside pnlConfirm when loaded
        }



        // ================== Hardware key handlers ==================
        public void OnF2Pressed()
        {
            // Equivalent of Tab: focus and select all text in txtBarcode
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
        public void OnEscapePressed()
        {
            if (txtStocker.IsFocused)
            {
                BtnCancel_Clicked(null, null);
            }
        }

    }
}
