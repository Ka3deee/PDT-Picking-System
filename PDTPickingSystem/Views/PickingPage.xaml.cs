using Android.OS;
using Microsoft.Data.SqlTypes;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Dispatching;
using PDTPickingSystem.Helpers;
using Microsoft.Data.SqlClient;
using System.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace PDTPickingSystem.Views
{
    public partial class PickingPage : ContentPage
    {
        // Tracks which Entry currently has focus (TextBox → Entry in MAUI)
        private Entry txtboxFocus; // Converted from TextBox
        // SKU and stocker tracking
        private int sSKU = 0; // Converted from Integer
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
        private int tmrRetryCounter = 0;

        private List<PickQtyItem> pickList = new List<PickQtyItem>();

        private string txtpSKU_UPC = "";
        private string txtpSlot_Value = "";

        // Replaces txtStocker.Tag
        private object txtStockerTag = null;

        private ObservableCollection<BarcodeItem> barcodeList = new ObservableCollection<BarcodeItem>();
        public PickingPage()
        {
            InitializeComponent(); // MAUI equivalent of VB InitializeComponent()

            // ===== Initialize the focus tracker =====
            txtboxFocus = txtBarcode; // Initially focus on barcode Entry

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

            // Tapped events
            llblDescr.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(LlblDescr_Tapped) });
            pnlSlots.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(PnlSlots_Tapped) });

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
            AppGlobal.SetUser(lblUser);

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

            if (txtboxFocus == txtBarcode || txtboxFocus == txtStocker)
            {
                isBarcode = txtboxFocus == txtBarcode;
                txtboxFocus.BackgroundColor = Colors.PaleGreen;
            }
            else
            {
                txtboxFocus.CursorPosition = 0;
                txtboxFocus.SelectionLength = txtboxFocus.Text?.Length ?? 0;
            }
        }

        private void Entry_LostFocus(object sender, FocusEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;

            entry.SelectionLength = 0; // clear text selection
        }

        // This method ensures that focus is always returned to txtboxFocus
        private void TxtOther_GotFocus(object sender, FocusEventArgs e)
        {
            txtboxFocus?.Focus();
        }

        private async void Entry_BarcodeAndQty_Completed(object sender, EventArgs e)
        {
            if (sender is not Entry entry) return;

            // Validate numeric input
            if (!_isAllowedNum(entry.Text))
            {
                entry.Text = "";
                return;
            }

            // Handle Enter key
            if (entry == txtBarcode)
            {
                await GetSKUDescrAsync();
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

        // Optional: handle Escape key if you have hardware keyboard
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
            // Your logic here
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
            // Example: Load current pick number from AppGlobal or database
            if (!await AppGlobal.ConnectSqlAsync()) return;

            // Example logic (replace with your actual query)
            var pickNo = AppGlobal.PickNo;
            if (!string.IsNullOrEmpty(pickNo))
            {
                txtpSKU.Text = pickNo; // or whatever control you want to update
            }
        }

        private async void BtnAccept_Clicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtEach.Text) &&
                !string.IsNullOrWhiteSpace(txtCase.Text) &&
                double.TryParse(txtEach.Text, out double eachValue) &&
                double.TryParse(txtCase.Text, out double caseValue) &&
                eachValue >= 0 && caseValue >= 0)
            {
                // BOTH QUANTITIES ARE ZERO
                if (eachValue == 0 && caseValue == 0)
                {
                    bool acceptZero = await DisplayAlert(
                        "Accept?",
                        "You have entered 0 in quantity.\nMeaning, the item is not available.\n\nAccept 0 quantity?",
                        "Yes",
                        "No");

                    if (acceptZero)
                    {
                        // Hide main panels
                        pnlInput.IsVisible = false;
                        pnlNavigate.IsVisible = false;

                        // Reset stocker
                        txtStocker.Text = "";
                        txtStockerTag = null;
                        txtStocker.IsReadOnly = !_CheckOption_Stocker();

                        // Show confirm panel and focus
                        pnlConfirm.IsVisible = true;

                        // Delay slightly to ensure panel is visible before focus
                        await Task.Delay(100);
                        txtStocker.Focus();
                    }
                }
                // NORMAL ACCEPT
                else if (!string.IsNullOrWhiteSpace(txtSKU.Text) &&
                         txtSKU.Text.Trim() == txtpSKU.Text.Trim())
                {
                    bool acceptQty = await DisplayAlert("Accept?", "Accept quantity?", "Yes", "No");
                    if (acceptQty)
                        _AcceptItem();
                }
            }
        }

        // Confirm button
        private async void BtnConfirm_Clicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtStocker.Text))
            {
                txtStockerTag = txtStocker.Text; // set tag here
                ID_Stocker = Convert.ToInt32(txtStockerTag);

                await DisplayAlert("OK", "Item Confirmed!", "OK");

                _AcceptItem();

                // Hide confirm panel
                pnlConfirm.IsVisible = false;

                // Restore input panels
                pnlNavigate.IsVisible = true;
                pnlInput.IsVisible = true;

                txtBarcode.Focus();
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

        private void TxtStockerConfirm_Completed(object sender, EventArgs e) { }
        private void BtnCloseGoto_Clicked(object sender, EventArgs e) => pnlGoto.IsVisible = false;
        private void TxtLine_Completed(object sender, EventArgs e) { }
        private void TxtLine_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtpDescr_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Your logic here
        }

        private async void TmrRequest_Tick(object sender, EventArgs e)
        {
            tmrRetryCounter++;

            try
            {
                using (var conn = new SqlConnection(AppGlobal.ConnectionString))
                {
                    await conn.OpenAsync();

                    // Check if request has been sent
                    var cmd = new SqlCommand($"SELECT * FROM tblUsers WHERE ID={AppGlobal.ID_User} AND ID_SumHdr<>0", conn);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // Request has been sent
                            tmrRequest.Stop();
                            AppGlobal.ID_SumHdr = Convert.ToInt32(reader["ID_SumHdr"]);

                            // Call your async method to load SKUs
                            await _AddSKUtoListAsync();
                            return;
                        }
                    }

                    // Retry logic after 5 attempts
                    if (tmrRetryCounter >= 5)
                    {
                        tmrRetryCounter = 0;

                        var resetCmd = new SqlCommand($"UPDATE tblUsers SET isRequest=0, isSummary=0 WHERE ID={AppGlobal.ID_User}", conn);
                        await resetCmd.ExecuteNonQueryAsync();

                        await DisplayAlert("Unable to request!", "No picking no. available!", "OK");

                        // Close page
                        await Navigation.PopAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error!", ex.Message, "OK");
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

        private void LlblDescr_Tapped()
        {
            barcodeList.Clear();

            if (!string.IsNullOrEmpty(txtpSKU?.Text))
            {
                // Use txtpSKU_UPC instead of Tag
                var upcs = txtpSKU_UPC?.Split(',', StringSplitOptions.RemoveEmptyEntries);

                if (upcs != null)
                {
                    foreach (var sUPC in upcs)
                    {
                        var cleaned = sUPC.Replace("-", "").Trim();
                        if (!string.IsNullOrEmpty(cleaned))
                            barcodeList.Add(new BarcodeItem { Text = "", UPC = cleaned });
                    }
                }
            }

            pnlBarcodes.IsVisible = true;

            // Focus first entry inside panel
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

        private void LlblSlot_Tapped(object sender, TappedEventArgs e)
        {
            // Use txtpSlot_Value instead of undefined txtpSlotTag
            if (txtpSlot.Text == "<< Multiple Slots >>" && !string.IsNullOrEmpty(txtpSlot_Value))
            {
                // Split slots from stored comma-separated string
                var sSlots = txtpSlot_Value.Split(',', StringSplitOptions.RemoveEmptyEntries);

                // Convert to SlotItem objects for CollectionView
                lvSlots.ItemsSource = sSlots.Select(s => new SlotItem { Slot = s.Trim() }).ToList();

                // Show the panel
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
            if (e.CurrentSelection == null || e.CurrentSelection.Count == 0)
                return;

            var selectedItem = e.CurrentSelection[0];

            // Example: If your SKU model has a property called SKU
            // txtpSKU.Text = ((YourSkuModel)selectedItem).SKU;

            // TODO: Add your logic here
        }

        private void LblMultipleSlots_Loaded(object sender, EventArgs e) { }
        private void PnlSlots_Tapped() { }
        private void BtnCloseSlot_Clicked(object sender, EventArgs e) => pnlSlots.IsVisible = false;

        private void BtnNavigate_Clicked(object sender, EventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            switch (btn.StyleId)   // Use StyleId as the "Name"
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
                    sSKU = sSKU - 1;
                    break;

                case "btnNext":
                    sSKU = sSKU + 1;
                    break;

                case "btnLast":
                    sSKU = pickList.Count - 1;   // MAUI CollectionView items
                    pnlGoto.IsVisible = false;
                    break;

                case "btnUnpick":
                    sSKU = 0;                   // empty string equivalent
                    pnlGoto.IsVisible = false;
                    break;
            }

            _GetSKUtoPick();
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

        // ================= CLASSES ======================
        public class BarcodeItem { public string Text { get; set; } = ""; public string UPC { get; set; } = ""; }
        public class SlotItem { public string Text { get; set; } = ""; public string Slot { get; set; } = ""; }
        public class PickQtyItem
        {
            public int ID { get; set; }
            public string SKU { get; set; } = "";
            public string Descr { get; set; } = "";
            public string Slot { get; set; } = "";
            public double Qty { get; set; }
            public double PickedQty { get; set; }
            public string DisplayQty => PickedQty.ToString("N2");
        }

        // ================= HELPER METHODS =================
        private string _GetDateTime()
        {
            // Return current date/time in format compatible with your SQL server
            // For example: "yyyy-MM-dd HH:mm:ss"
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        // Dummy methods
        private bool _isAllowedNum(string input)
        {
            // Allow digits, dot, or empty
            return string.IsNullOrEmpty(input) || double.TryParse(input, out _);
        }

        private bool _CheckOption_Stocker() => true;

        private async Task _AcceptItem()
        {
            // -----------------------------
            // 1️⃣ Connect to SQL
            // -----------------------------
            if (!await AppGlobal.ConnectSqlAsync()) return;

            using var sqlCmd = AppGlobal.SqlCon.CreateCommand();

            // -----------------------------
            // 2️⃣ Calculate Picked Qty
            // -----------------------------
            double caseTag = Convert.ToDouble(txtpCase.BindingContext ?? 0); // Tag replacement
            double caseQty = Convert.ToDouble(txtCase.Text);
            double eachQty = Convert.ToDouble(txtEach.Text);

            double dQty = (caseTag * caseQty) + eachQty;

            // -----------------------------
            // 3️⃣ Get the selected SKU from CollectionView
            // -----------------------------
            if (sSKU == -1 || lvSKU.SelectedItem == null) return;

            var lvItem = lvSKU.SelectedItem as PickQtyItem;
            if (lvItem == null) return;

            // -----------------------------
            // 4️⃣ Update PickedQty in the list
            // -----------------------------
            lvItem.PickedQty = dQty;

            // -----------------------------
            // 5️⃣ Update Pick Header
            // -----------------------------
            if (!isStarted)
            {
                isStarted = true;
                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickHdr SET isUpdate=1, TimeStart='{_GetDateTime()}' WHERE ID={ID_SumHdr}";
            }
            else
            {
                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickHdr SET isUpdate=1 WHERE ID={ID_SumHdr}";
            }
            await sqlCmd.ExecuteNonQueryAsync();

            // -----------------------------
            // 6️⃣ Prepare UPC update
            // -----------------------------
            string sUPC = "";
            if (!pbScanned.IsVisible && !string.IsNullOrEmpty(txtBarcode.Text))
                sUPC = $"UPC={txtBarcode.Text},";

            if (eachQty == 0 && caseQty == 0)
                sUPC = "";

            // -----------------------------
            // 7️⃣ Update Pick Details
            // -----------------------------
            if (isSummary == 1)
            {
                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET {sUPC}PickBy={AppGlobal.ID_User}, ConfBy={ID_Stocker}, PickTime='{_GetDateTime()}' " +
                                     $"WHERE SKU={lvItem.SKU} AND ID_SumHdr={ID_SumHdr}";
                await sqlCmd.ExecuteNonQueryAsync();
            }
            else
            {
                // Fetch data from PickDtl using async replacement
                var dsData = await _WorkQueryAsync($"SELECT ID, Qty FROM tbl{pPickNo}PickDtl WHERE SKU={lvItem.SKU} AND ID_SumHdr={ID_SumHdr}");

                // Update all PickDtl rows
                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET {sUPC}SortTime='{_GetDateTime()}', SortBy={AppGlobal.ID_User}, PickBy={AppGlobal.ID_User}, ConfBy={ID_Stocker}, PickTime='{_GetDateTime()}' " +
                                     $"WHERE SKU={lvItem.SKU} AND ID_SumHdr={ID_SumHdr}";
                await sqlCmd.ExecuteNonQueryAsync();

                int lCount = dsData.Tables[0].Rows.Count - 1;
                double dNeedQty;

                for (int iCount = 0; iCount <= lCount; iCount++)
                {
                    var dRow = dsData.Tables[0].Rows[iCount];
                    if (iCount == lCount)
                    {
                        // Last item: assign remaining quantity
                        sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET isSorted=1, SortQty={dQty}, isUpdate=1 WHERE ID={dRow["ID"]}";
                        await sqlCmd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        dNeedQty = Convert.ToDouble(dRow["Qty"]);
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

            // -----------------------------
            // 8️⃣ Update PickQty Table
            // -----------------------------
            sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickQty SET isPicked=1, PickQty={lvItem.PickedQty} WHERE ID={lvItem.ID}";
            await sqlCmd.ExecuteNonQueryAsync();

            // -----------------------------
            // 9️⃣ Move to next SKU
            // -----------------------------
            ID_Stocker = 0;
            if (btnNext.IsEnabled)
            {
                BtnNavigate_Clicked(btnNext, null);
            }
            else
            {
                sSKU = 0;
                await _GetSKUtoPick();
            }
        }

        private async Task _GetSKUtoPick()
        {
            // TODO: Implement logic to load the next SKU to pick
            // Example placeholder:
            if (pickList.Count > 0)
            {
                sSKU = 0; // reset index or use appropriate logic
                txtpSKU.Text = pickList[0].SKU; // load first SKU as example
                llblDescr.Text = pickList[0].Descr;
            }
            await Task.CompletedTask; // ensure it returns Task
        }

        private async Task<DataSet> _WorkQueryAsync(string sql)
        {
            var ds = new DataSet();

            if (!await AppGlobal.ConnectSqlAsync())
                return ds; // return empty DataSet if connection fails

            using var cmd = new SqlCommand(sql, AppGlobal.SqlCon);
            using var adapter = new SqlDataAdapter(cmd);
            adapter.Fill(ds);

            return ds;
        }

        private async Task _AddSKUtoListAsync()
        {
            try
            {
                if (!await AppGlobal.ConnectSqlAsync()) return;

                var sql = $"SELECT ID, SKU, Descr, Qty, PickedQty, Slot FROM tblPickItems WHERE PickNo='{AppGlobal.PickNo}'";
                var list = new List<PickingPage.PickQtyItem>();

                using (var conn = new SqlConnection(AppGlobal.ConnectionString))
                {
                    await conn.OpenAsync();

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var item = new PickingPage.PickQtyItem
                            {
                                ID = reader.GetInt32(reader.GetOrdinal("ID")),
                                SKU = reader.GetString(reader.GetOrdinal("SKU")),
                                Descr = reader.GetString(reader.GetOrdinal("Descr")),
                                Slot = reader.GetString(reader.GetOrdinal("Slot")),
                                Qty = reader.GetDouble(reader.GetOrdinal("Qty")),
                                PickedQty = reader.GetDouble(reader.GetOrdinal("PickedQty"))
                            };
                            list.Add(item);
                        }
                    }
                }

                pickList = list; // Update your class-level list

                // Optionally, update UI (CollectionView/ListView) if you have one:
                // lvSKU.ItemsSource = pickList;

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
        public void OnF1Pressed()
        {
            // Equivalent of Escape: close the page
            if (Navigation.NavigationStack.Count > 0)
                MainThread.BeginInvokeOnMainThread(async () => await Navigation.PopAsync());
        }

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
    }
}
