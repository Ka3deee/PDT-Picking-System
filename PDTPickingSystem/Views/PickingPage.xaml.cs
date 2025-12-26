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

namespace PDTPickingSystem.Views
{
    public partial class PickingPage : ContentPage
    {
        // ================== PRIVATE FIELDS ==================

        // Focus tracking
        private Entry txtboxFocus;

        // SKU and stocker tracking
        private int sSKU = -1;
        private int ID_Stocker = 0;

        // Picking state
        private bool isStarted = false;
        private bool isBarcode = true;
        private int isSummary = 0;
        private string pPickNo = "";
        private long ID_SumHdr = 0;

        // Timer for request checking
        private IDispatcherTimer tmrRequest;
        private int iRetry = 0;

        // Data collections
        private List<PickQtyItem> pickList = new List<PickQtyItem>();
        public ObservableCollection<BarcodeItem> barcodeList = new ObservableCollection<BarcodeItem>();

        // Tag replacements (VB.NET .Tag property)
        private string txtpSKU_UPC = "";
        private string txtpSlot_Value = "";
        private string txtpSlotTag = "";
        private string txtStockerTag = "";
        private object txtpCaseTag;
        private object txtpEachTag;

        // Request tracking flags
        private bool _requestAlreadyShown = false;
        private bool _requestFailedShown = false;

        // ================== CONSTRUCTOR ==================

        public PickingPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            // Initialize from AppGlobal
            pPickNo = AppGlobal.pPickNo;
            isSummary = AppGlobal.isSummary;
            ID_SumHdr = AppGlobal.ID_SumHdr;

            // Initialize focus tracker
            txtboxFocus = txtBarcode;

            // Bind collections
            lvBarcodes.ItemsSource = barcodeList;

            // Initialize timer
            tmrRequest = Dispatcher.CreateTimer();
            tmrRequest.Interval = TimeSpan.FromSeconds(1);
            tmrRequest.Tick += TmrRequest_Tick;

            // Page events
            Appearing += PickingPage_Appearing;
            Disappearing += PickingPage_Disappearing;

            // ===== Focus events - Force focus back to barcode/qty entry =====
            txtpSKU.Focused += TxtOther_GotFocus;
            txtpDescr.Focused += TxtOther_GotFocus;
            txtpSlot.Focused += TxtOther_GotFocus;
            txtpEach.Focused += TxtOther_GotFocus;
            txtpCase.Focused += TxtOther_GotFocus;
            txtSKU.Focused += TxtOther_GotFocus;
            txtDone.Focused += TxtOther_GotFocus;
            txtDeptStore.Focused += TxtOther_GotFocus;

            // ===== Entry focus events (tracking + highlighting) =====
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

            // ===== TextChanged events =====
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
            txtStocker.TextChanged += TxtStocker_TextChanged;
            txtLine.TextChanged += TxtLine_TextChanged;

            // ===== Loaded events =====
            lblCase.Loaded += LblCase_Loaded;
            lblEach.Loaded += LblEach_Loaded;
            lblEach2.Loaded += LblEach2_Loaded;
            lblCase2.Loaded += LblCase2_Loaded;
            lblBarcode.Loaded += LblBarcode_Loaded;
            Gotolbl.Loaded += Gotolbl_Loaded;
            lblLineNo.Loaded += LblLineNo_Loaded;
            lblSKU.Loaded += LblSKU_Loaded;
            lblDone.Loaded += LblDone_Loaded;
            lblInput.Loaded += LblInput_Loaded;
            lblConfirmTitle.Loaded += LblConfirmTitle_Loaded;
            lblUser.Loaded += LblUser_Loaded;
            lblMultipleSlots.Loaded += LblMultipleSlots_Loaded;
            lblBarcodeTitle.Loaded += LblBarcodeTitle_Loaded;

            // ===== Tap gesture recognizers =====
            var descrTap = new TapGestureRecognizer();
            descrTap.Tapped += LlblDescr_Tapped;
            llblDescr.GestureRecognizers.Add(descrTap);

            var slotTap = new TapGestureRecognizer();
            slotTap.Tapped += LlblSlot_Tapped;
            llblSlot.GestureRecognizers.Add(slotTap);

            var slotsPanelTap = new TapGestureRecognizer();
            slotsPanelTap.Tapped += PnlSlots_Tapped;
            pnlSlots.GestureRecognizers.Add(slotsPanelTap);

            // ===== Button events =====
            btnCloseGoto.Clicked += BtnCloseGoto_Clicked;
            pbScanned.Clicked += PbScanned_Clicked;
            btnAccept.Clicked += BtnAccept_Clicked;
            btnCloseSlot.Clicked += BtnCloseSlot_Clicked;
            btnConfirm.Clicked += BtnConfirm_Clicked;
            btnCancel.Clicked += BtnCancel_Clicked;
            btnFinished.Clicked += BtnFinished_Clicked;
            btnClose.Clicked += BtnClose_Clicked;

            // ===== Navigation buttons (unified handler) =====
            btnGoto.Clicked += BtnNavigate_Clicked;
            btnFirst.Clicked += BtnNavigate_Clicked;
            btnPrev.Clicked += BtnNavigate_Clicked;
            btnNext.Clicked += BtnNavigate_Clicked;
            btnLast.Clicked += BtnNavigate_Clicked;
            btnUnpick.Clicked += BtnNavigate_Clicked;

            // ===== Entry Completed events =====
            txtBarcode.Completed += Entry_BarcodeAndQty_Completed;
            txtCase.Completed += Entry_BarcodeAndQty_Completed;
            txtEach.Completed += Entry_BarcodeAndQty_Completed;
            txtStocker.Completed += TxtStocker_Completed;
            txtLine.Completed += TxtLine_Completed;

            // ===== Panel focus =====
            pnlBarcodes.Focused += PnlBarcodes_Focused;
            pnlGoto.Focused += PnlGoto_Focused;

            // ===== CollectionView selection =====
            lvSlots.SelectionChanged += LvSlots_SelectionChanged;
        }

        // ================== PAGE LIFECYCLE ==================

        private async void PickingPage_Appearing(object sender, EventArgs e)
        {
            // Set user label
            AppGlobal._SetUser(lblUser);

            // Set barcode mode
            isBarcode = true;

            // Hide all panels
            pnlConfirm.IsVisible = false;
            pnlBarcodes.IsVisible = false;
            pnlSlots.IsVisible = false;
            pnlGoto.IsVisible = false;
            btnFinished.IsVisible = false;

            // Load picking data
            await _GetSetPickNoAsync();
        }

        private void PickingPage_Disappearing(object sender, EventArgs e)
        {
            tmrRequest?.Stop();
        }

        // ================== FOCUS MANAGEMENT ==================

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

            entry.SelectionLength = 0;

            if (entry == txtBarcode || entry == txtStocker)
            {
                entry.BackgroundColor = Colors.WhiteSmoke;
            }
        }

        private void TxtOther_GotFocus(object sender, FocusEventArgs e)
        {
            txtboxFocus?.Focus();
        }

        private void PnlBarcodes_Focused(object sender, FocusEventArgs e)
        {
            if (pnlBarcodes.Content is Microsoft.Maui.Controls.Layout layout &&
                layout.Children.FirstOrDefault() is Entry firstEntry)
            {
                firstEntry.Focus();
            }
        }

        private void PnlGoto_Focused(object sender, FocusEventArgs e) { }

        // ================== ENTRY VALIDATION & COMPLETION ==================

        private async void Entry_BarcodeAndQty_Completed(object sender, EventArgs e)
        {
            if (sender is not Entry entry) return;

            if (entry.Text != null && entry.Text.Any(c => AppGlobal._isAllowedNum(c) == '\0'))
            {
                entry.Text = "";
                return;
            }

            if (entry == txtBarcode)
            {
                // Check local UPC first, then SQL if not found
                await GetSKUDescrAsync();
            }
            else if (entry == txtCase)
            {
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

        private void TxtStocker_Completed(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtStocker.Text))
            {
                _ = ConfirmStockerAsync();
            }
        }

        private void TxtLine_Completed(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtLine.Text) ||
                !double.TryParse(txtLine.Text, out double lineNum) ||
                lineNum == 0)
                return;

            if (lineNum > pickList.Count)
            {
                _ = DisplayAlert("! ! !", "Line Number out of range!", "OK");
                txtLine.Focus();
                txtLine.CursorPosition = 0;
                txtLine.SelectionLength = txtLine.Text.Length;
                return;
            }

            pnlGoto.IsVisible = false;
            sSKU = (int)lineNum - 1;
            _ = _GetSKUtoPickAsync();
        }

        // ================== TEXT CHANGED HANDLERS ==================

        private void TxtBarcode_TextChanged(object sender, TextChangedEventArgs e) { }

        private void TxtDeptStore_TextChanged(object sender, TextChangedEventArgs e)
        {
            AppGlobal.DeptStore = txtDeptStore.Text;
        }

        private void TxtpSKU_TextChanged(object sender, TextChangedEventArgs e)
        {
            var currentSKU = txtpSKU.Text?.Trim();
            if (!string.IsNullOrEmpty(currentSKU))
            {
                var item = pickList.Find(p => p.SKU == currentSKU);
                llblDescr.Text = item?.Descr ?? "";
            }
        }

        private void TxtpCase_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtpEach_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtEach_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtSKU_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtCase_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtpSlot_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtDone_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtpDescr_TextChanged(object sender, TextChangedEventArgs e) { }

        private void TxtStocker_TextChanged(object sender, TextChangedEventArgs e)
        {
            var txt = txtStocker.Text;
            if (!string.IsNullOrEmpty(txt))
            {
                var filtered = new string(txt.Where(c => AppGlobal._isAllowedNum(c) != '\0').ToArray());
                if (txt != filtered)
                {
                    txtStocker.Text = filtered;
                    txtStocker.CursorPosition = txtStocker.Text.Length;
                }
            }
        }

        private void TxtLine_TextChanged(object sender, TextChangedEventArgs e)
        {
            var txt = txtLine.Text;
            if (!string.IsNullOrEmpty(txt))
            {
                txtLine.Text = new string(txt.Where(c => AppGlobal._isAllowedNum(c) != '\0').ToArray());
                txtLine.CursorPosition = txtLine.Text.Length;
            }
        }

        // ================== BUTTON CLICK HANDLERS ==================

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
                        txtStockerTag = "";
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
                        await _AcceptItemAsync();
                    }
                }
            }
        }

        private async void BtnConfirm_Clicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtStockerTag))
            {
                if (int.TryParse(txtStockerTag, out int stockerId))
                {
                    ID_Stocker = stockerId;
                    await DisplayAlert("OK", "Item Confirmed!", "OK");
                    BtnCancel_Clicked(null, null);
                    await _AcceptItemAsync();
                }
            }
        }

        private void BtnCancel_Clicked(object sender, EventArgs e)
        {
            pnlConfirm.IsVisible = false;
            pnlNavigate.IsVisible = true;
            pnlInput.IsVisible = true;
            txtBarcode.Focus();
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

            foreach (var lvItem in pickList)
            {
                if (lvItem.PickedQty == 0)
                {
                    sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET " +
                                         "SortQty=0, isSorted=1, isUpdate=1, " +
                                         "pickTime='00:00:00', TSort_Start='00:00:00', TSort_End='00:00:00' " +
                                         "WHERE SKU=@SKU AND ID_SumHdr=@SumHdr";
                    sqlCmd.Parameters.Clear();
                    sqlCmd.Parameters.AddWithValue("@SKU", lvItem.SKU);
                    sqlCmd.Parameters.AddWithValue("@SumHdr", ID_SumHdr);
                    await sqlCmd.ExecuteNonQueryAsync();
                }
            }

            sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickHdr SET " +
                                 "isUpdate=1, TimeEnd=@TimeEnd, DateDone=@DateDone " +
                                 "WHERE ID=@ID";
            sqlCmd.Parameters.Clear();
            sqlCmd.Parameters.AddWithValue("@TimeEnd", await AppGlobal._GetDateTime());
            sqlCmd.Parameters.AddWithValue("@DateDone", await AppGlobal._GetDateTime(true));
            sqlCmd.Parameters.AddWithValue("@ID", ID_SumHdr);
            await sqlCmd.ExecuteNonQueryAsync();

            sqlCmd.CommandText = "UPDATE tblUsers SET ID_SumHdr=0 WHERE ID=@UserID";
            sqlCmd.Parameters.Clear();
            sqlCmd.Parameters.AddWithValue("@UserID", AppGlobal.ID_User);
            await sqlCmd.ExecuteNonQueryAsync();

            await _GetSetPickNoAsync();
        }

        private void BtnCloseGoto_Clicked(object sender, EventArgs e)
        {
            pnlGoto.IsVisible = false;
        }

        private void BtnCloseSlot_Clicked(object sender, EventArgs e)
        {
            pnlSlots.IsVisible = false;
        }

        private void BtnClose_Clicked(object sender, EventArgs e)
        {
            pnlBarcodes.IsVisible = false;
            txtboxFocus.Focus();
        }

        private void PbScanned_Clicked(object sender, EventArgs e)
        {
            _ = DisplayAlert("Scanned", "Item already scanned!", "OK");
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
                    sSKU = -1;
                    pnlGoto.IsVisible = false;
                    break;
            }

            await _GetSKUtoPickAsync();
        }

        // ================== TAP GESTURE HANDLERS ==================

        private void LlblDescr_Tapped(object sender, EventArgs e)
        {
            barcodeList.Clear();

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
                            Text = "",
                            UPC = cleaned
                        });
                    }
                }
            }

            pnlBarcodes.IsVisible = true;

            if (pnlBarcodes.Content is Microsoft.Maui.Controls.Layout layout &&
                layout.Children.FirstOrDefault() is Entry firstEntry)
            {
                firstEntry.Focus();
            }
        }

        private void LlblSlot_Tapped(object sender, EventArgs e)
        {
            if (txtpSlot.Text == "<< Multiple Slots >>" && !string.IsNullOrEmpty(txtpSlot_Value))
            {
                var sSlots = txtpSlot_Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
                lvSlots.ItemsSource = sSlots.Select(s => new SlotItem { Slot = s.Trim() }).ToList();
                pnlSlots.IsVisible = true;
            }
        }

        private void PnlSlots_Tapped(object sender, TappedEventArgs e) { }

        // ================== COLLECTION VIEW SELECTION ==================

        private void LvSlots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedSlot = e.CurrentSelection.FirstOrDefault() as SlotItem;
            if (selectedSlot != null)
            {
                txtpSlot.Text = selectedSlot.Slot;
                pnlSlots.IsVisible = false;
                lvSlots.SelectedItem = null;
            }
        }

        // ================== LOADED EVENT HANDLERS ==================

        private void LblCase_Loaded(object sender, EventArgs e) { }
        private void LblEach_Loaded(object sender, EventArgs e) { }
        private void LblEach2_Loaded(object sender, EventArgs e) { }
        private void LblCase2_Loaded(object sender, EventArgs e) { }
        private void LblBarcode_Loaded(object sender, EventArgs e) { }
        private void Gotolbl_Loaded(object sender, EventArgs e) { }
        private void LblLineNo_Loaded(object sender, EventArgs e) { }
        private void LblSKU_Loaded(object sender, EventArgs e) { }
        private void LblDone_Loaded(object sender, EventArgs e) { }
        private void LblInput_Loaded(object sender, EventArgs e) { }
        private void LblConfirmTitle_Loaded(object sender, EventArgs e) { }
        private void LblUser_Loaded(object sender, EventArgs e) { }
        private void LblMultipleSlots_Loaded(object sender, EventArgs e) { }
        private void LblBarcodeTitle_Loaded(object sender, EventArgs e) { }

        // ================== CORE BUSINESS LOGIC ==================

        /// <summary>
        /// Initialize picking session and load data from server
        /// </summary>
        private async Task _GetSetPickNoAsync()
        {
            btnFinished.IsVisible = false;
            pbReq.IsVisible = true;

            int hasUnfinishedTrf = 0;
            string sUserPNo = "";

            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                await DisplayAlert("No Connection!",
                    "Cannot connect to server! Please retry or check settings...", "OK");
                await Navigation.PopAsync();
                return;
            }

            try
            {
                // Get user info
                using (var sqlCmd = new SqlCommand(
                    "SELECT ID_SumHdr, PickRef FROM tblUsers WHERE ID=@UserID", conn))
                {
                    sqlCmd.Parameters.AddWithValue("@UserID", AppGlobal.ID_User);

                    using var reader = await sqlCmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        AppGlobal.ID_SumHdr = reader["ID_SumHdr"] != DBNull.Value
                            ? Convert.ToInt32(reader["ID_SumHdr"])
                            : 0;

                        if (reader["PickRef"] != DBNull.Value)
                        {
                            if (Convert.ToInt64(reader["PickRef"]) != 0)
                            {
                                sUserPNo = reader["PickRef"].ToString().Trim();
                            }
                        }
                    }
                }

                // Check if user has active picking session
                if (AppGlobal.ID_SumHdr != 0 && sUserPNo == AppGlobal.pPickNo)
                {
                    await _AddSKUtoListAsync();
                    return;
                }

                // Request from server (show once only)
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
                        using var updateCmd = new SqlCommand(
                            "UPDATE tblUsers SET isRequest=1, isSummary=@Summary, PickRef=@PickRef WHERE ID=@UserID",
                            conn);
                        updateCmd.Parameters.AddWithValue("@Summary", AppGlobal.isSummary);
                        updateCmd.Parameters.AddWithValue("@PickRef", AppGlobal.pPickNo);
                        updateCmd.Parameters.AddWithValue("@UserID", AppGlobal.ID_User);
                        await updateCmd.ExecuteNonQueryAsync();

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
        /// Load SKU list from database
        /// </summary>
        private async Task _AddSKUtoListAsync()
        {
            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null) return;

            try
            {
                // Load PickHdr
                using (var cmdHdr = new SqlCommand(
                    $"SELECT * FROM tbl{pPickNo}PickHdr WHERE ID=@ID AND TimeEnd='0'", conn))
                {
                    cmdHdr.Parameters.AddWithValue("@ID", ID_SumHdr);

                    using var readerHdr = await cmdHdr.ExecuteReaderAsync();
                    if (await readerHdr.ReadAsync())
                    {
                        isStarted = readerHdr["TimeStart"].ToString().Trim() != "0";
                        var deptId = Convert.ToInt32(readerHdr["iDept"]);
                        readerHdr.Close();

                        if (isSummary == 1)
                        {
                            lblDeptStore.Text = "Department:";
                            txtDeptStore.Text = await AppGlobal._GetDeptName(deptId);
                        }
                        else
                        {
                            lblDeptStore.Text = "Store No:";
                            txtDeptStore.Text = await AppGlobal._GetStoreNo();
                        }
                    }
                }

                // Load PickQty items
                var dsData = new DataSet();
                bool querySuccess = await AppGlobal._WorkQueryAsync(
                    $"SELECT * FROM tbl{pPickNo}PickQty WHERE ID_SumHdr={ID_SumHdr} ORDER BY Slot, SKU",
                    dsData,
                    "PickQty");

                if (!querySuccess || dsData.Tables.Count == 0) return;

                // Populate pickList
                pickList.Clear();

                foreach (DataRow dRow in dsData.Tables["PickQty"].Rows)
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
                            : Convert.ToDouble(dRow["PickQty"])
                    };

                    pickList.Add(item);
                }

                // Reset to find first unpicked
                sSKU = -1;
                await _GetSKUtoPickAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        /// <summary>
        /// Display next SKU to pick
        /// </summary>
        private async Task _GetSKUtoPickAsync()
        {
            _ClearScan();

            if (pickList == null || pickList.Count == 0) return;

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

            var currentItem = pickList[sSKU];

            // Slots
            if (currentItem.Slot.Contains(","))
            {
                txtpSlot.Text = "<< Multiple Slots >>";
                txtpSlotTag = currentItem.Slot;
                txtpSlot_Value = currentItem.Slot;
            }
            else
            {
                txtpSlot.Text = currentItem.Slot;
                txtpSlotTag = "";
                txtpSlot_Value = "";
            }
            llblSlot.Text = txtpSlot.Text;

            // SKU
            txtpSKU.Text = currentItem.SKU;
            txtpSKU_UPC = currentItem.UPC;

            // Description
            txtpDescr.Text = currentItem.Descr;
            llblDescr.Text = txtpDescr.Text;

            // Quantities
            txtpEachTag = currentItem.Qty;
            txtpCaseTag = currentItem.BUM;

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

            // Navigation buttons
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

            _CountPicked();
        }

        /// <summary>
        /// Count picked items and update UI
        /// </summary>
        private void _CountPicked()
        {
            int iPicked = 0;

            if (pickList != null)
            {
                foreach (var item in pickList)
                {
                    if (item.PickedQty > 0)
                        iPicked++;
                }

                txtDone.Text = $"{iPicked}/{pickList.Count}";

                if (iPicked == pickList.Count)
                {
                    btnFinished.IsVisible = true;
                }
            }

            pbReq.IsVisible = false;
        }

        /// <summary>
        /// Clear scan fields
        /// </summary>
        private void _ClearScan(bool bWithBarcode = true)
        {
            if (bWithBarcode)
                txtBarcode.Text = string.Empty;

            txtSKU.Text = string.Empty;
            txtEach.Text = "0";
            txtCase.Text = "0";

            txtEach.CursorPosition = 0;
            txtCase.CursorPosition = 0;

            _ = txtBarcode.Focus();

            txtBarcode.CursorPosition = 0;
            txtBarcode.SelectionLength = txtBarcode.Text?.Length ?? 0;

            txtEach.CursorPosition = 0;
            txtEach.SelectionLength = txtEach.Text?.Length ?? 0;

            txtCase.CursorPosition = 0;
            txtCase.SelectionLength = txtCase.Text?.Length ?? 0;
        }

        /// <summary>
        /// Accept picked item and update database
        /// </summary>
        /// <summary>
        /// Accept picked item and update database
        /// </summary>
        private async Task _AcceptItemAsync()
        {
            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null) return;

            using var sqlCmd = conn.CreateCommand();

            // Calculate picked quantity
            double caseTag = Convert.ToDouble(txtpCaseTag ?? 0);
            double caseQty = double.TryParse(txtCase.Text, out double cQty) ? cQty : 0;
            double eachQty = double.TryParse(txtEach.Text, out double eQty) ? eQty : 0;
            double dQty = (caseTag * caseQty) + eachQty;

            // Get selected SKU item from pickList
            if (sSKU < 0 || sSKU >= pickList.Count) return;
            var lvItem = pickList[sSKU];

            lvItem.PickedQty = dQty;

            // Update PickHdr
            if (!isStarted)
            {
                isStarted = true;
                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickHdr SET " +
                                     "isUpdate=1, TimeStart=@TimeStart WHERE ID=@ID";
                sqlCmd.Parameters.AddWithValue("@TimeStart", await AppGlobal._GetDateTime());
                sqlCmd.Parameters.AddWithValue("@ID", ID_SumHdr);
            }
            else
            {
                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickHdr SET isUpdate=1 WHERE ID=@ID";
                sqlCmd.Parameters.AddWithValue("@ID", ID_SumHdr);
            }
            await sqlCmd.ExecuteNonQueryAsync();

            // Prepare UPC update
            string sUPC = "";
            if (!pbScanned.IsVisible && !string.IsNullOrEmpty(txtBarcode.Text))
                sUPC = "UPC=@UPC,";
            if (eachQty == 0 && caseQty == 0)
                sUPC = "";

            // Update PickDtl
            if (isSummary == 1)
            {
                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET " +
                    $"{sUPC}PickBy=@PickBy, ConfBy=@ConfBy, PickTime=@PickTime " +
                    "WHERE SKU=@SKU AND ID_SumHdr=@SumHdr";
                sqlCmd.Parameters.Clear();
                if (!string.IsNullOrEmpty(sUPC))
                    sqlCmd.Parameters.AddWithValue("@UPC", txtBarcode.Text);
                sqlCmd.Parameters.AddWithValue("@PickBy", AppGlobal.ID_User);
                sqlCmd.Parameters.AddWithValue("@ConfBy", ID_Stocker);
                sqlCmd.Parameters.AddWithValue("@PickTime", await AppGlobal._GetDateTime());
                sqlCmd.Parameters.AddWithValue("@SKU", lvItem.SKU);
                sqlCmd.Parameters.AddWithValue("@SumHdr", ID_SumHdr);
                await sqlCmd.ExecuteNonQueryAsync();
            }
            else
            {
                // ✅ FIX: Use parameterized query directly instead of _WorkQueryAsync
                var dsData = new DataSet();

                // Create a separate command for reading data
                using (var readCmd = conn.CreateCommand())
                {
                    readCmd.CommandText = $"SELECT ID, Qty FROM tbl{pPickNo}PickDtl WHERE SKU=@SKU AND ID_SumHdr=@SumHdr";
                    readCmd.Parameters.AddWithValue("@SKU", lvItem.SKU);
                    readCmd.Parameters.AddWithValue("@SumHdr", ID_SumHdr);

                    using var adapter = new SqlDataAdapter(readCmd);
                    adapter.Fill(dsData, "PickDtl");
                }

                // Update all PickDtl records
                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET " +
                    $"{sUPC}SortTime=@SortTime, SortBy=@SortBy, PickBy=@PickBy, ConfBy=@ConfBy, PickTime=@PickTime " +
                    "WHERE SKU=@SKU AND ID_SumHdr=@SumHdr";
                sqlCmd.Parameters.Clear();
                if (!string.IsNullOrEmpty(sUPC))
                    sqlCmd.Parameters.AddWithValue("@UPC", txtBarcode.Text);
                sqlCmd.Parameters.AddWithValue("@SortTime", await AppGlobal._GetDateTime());
                sqlCmd.Parameters.AddWithValue("@SortBy", AppGlobal.ID_User);
                sqlCmd.Parameters.AddWithValue("@PickBy", AppGlobal.ID_User);
                sqlCmd.Parameters.AddWithValue("@ConfBy", ID_Stocker);
                sqlCmd.Parameters.AddWithValue("@PickTime", await AppGlobal._GetDateTime());
                sqlCmd.Parameters.AddWithValue("@SKU", lvItem.SKU);
                sqlCmd.Parameters.AddWithValue("@SumHdr", ID_SumHdr);
                await sqlCmd.ExecuteNonQueryAsync();

                // Distribute picked quantity across multiple detail records
                if (dsData.Tables["PickDtl"] != null && dsData.Tables["PickDtl"].Rows.Count > 0)
                {
                    int lCount = dsData.Tables["PickDtl"].Rows.Count - 1;

                    for (int iCount = 0; iCount <= lCount; iCount++)
                    {
                        var dRow = dsData.Tables["PickDtl"].Rows[iCount];
                        double dNeedQty = Convert.ToDouble(dRow["Qty"]);

                        if (iCount == lCount) // Last item
                        {
                            sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET " +
                                "isSorted=1, SortQty=@SortQty, isUpdate=1 WHERE ID=@ID";
                            sqlCmd.Parameters.Clear();
                            sqlCmd.Parameters.AddWithValue("@SortQty", dQty);
                            sqlCmd.Parameters.AddWithValue("@ID", dRow["ID"]);
                            await sqlCmd.ExecuteNonQueryAsync();
                        }
                        else
                        {
                            if (dQty >= dNeedQty)
                            {
                                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET " +
                                    "isSorted=1, SortQty=Qty, isUpdate=1 WHERE ID=@ID";
                                sqlCmd.Parameters.Clear();
                                sqlCmd.Parameters.AddWithValue("@ID", dRow["ID"]);
                                await sqlCmd.ExecuteNonQueryAsync();
                                dQty -= dNeedQty;
                            }
                            else
                            {
                                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET " +
                                    "isSorted=1, SortQty=@SortQty, isUpdate=1 WHERE ID=@ID";
                                sqlCmd.Parameters.Clear();
                                sqlCmd.Parameters.AddWithValue("@SortQty", dQty);
                                sqlCmd.Parameters.AddWithValue("@ID", dRow["ID"]);
                                await sqlCmd.ExecuteNonQueryAsync();
                                dQty = 0;
                            }
                        }
                    }
                }

                dsData.Tables.Clear();
            }

            // Update PickQty
            sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickQty SET " +
                "isPicked=1, PickQty=@PickQty WHERE ID=@ID";
            sqlCmd.Parameters.Clear();
            sqlCmd.Parameters.AddWithValue("@PickQty", lvItem.PickedQty);
            sqlCmd.Parameters.AddWithValue("@ID", lvItem.ID);
            await sqlCmd.ExecuteNonQueryAsync();

            // Move to next SKU
            ID_Stocker = 0;
            if (btnNext.IsEnabled)
            {
                BtnNavigate_Clicked(btnNext, null);
            }
            else
            {
                sSKU = -1;
                await _GetSKUtoPickAsync();
            }
        }

        /// <summary>
        /// Get SKU description from local UPC list
        /// </summary>
        private async Task GetSKUDescrAsync()
        {
            if (string.IsNullOrWhiteSpace(txtBarcode.Text))
                return;

            _ClearScan(false);

            // Check local UPC list first
            if (!string.IsNullOrEmpty(txtpSKU_UPC) &&
                txtpSKU_UPC.Contains($"-{txtBarcode.Text.Trim()},"))
            {
                // Match found in local UPC
                txtSKU.Text = txtpSKU.Text;
                txtCase.Text = txtpCase.Text;
                txtEach.Text = txtpEach.Text;
                txtBarcode.SelectionLength = 0;

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
                // Not found in local UPC, try SQL lookup
                await GetSKUDescr_WIFIAsync();
            }
        }

        /// <summary>
        /// Get SKU description from database (SQL lookup)
        /// </summary>
        private async Task GetSKUDescr_WIFIAsync()
        {
            if (string.IsNullOrWhiteSpace(txtBarcode.Text))
                return;

            if (!long.TryParse(txtBarcode.Text.Trim(), out long barcodeValue))
            {
                await DisplayAlert("Error", "Invalid barcode number", "OK");
                txtBarcode.Focus();
                txtBarcode.SelectionLength = txtBarcode.Text?.Length ?? 0;
                return;
            }

            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                await DisplayAlert("Error", "Cannot connect to server!", "OK");
                return;
            }

            try
            {
                using var sqlCmd = conn.CreateCommand();
                sqlCmd.CommandText = "SELECT CSKU FROM invMST WHERE UPC = @UPC";
                sqlCmd.Parameters.AddWithValue("@UPC", barcodeValue);

                using var reader = await sqlCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    string sSKUResult = reader["CSKU"].ToString().Trim();

                    if (sSKUResult == txtpSKU.Text.Trim())
                    {
                        txtSKU.Text = txtpSKU.Text;
                        txtCase.Text = txtpCase.Text;
                        txtEach.Text = txtpEach.Text;
                        txtBarcode.SelectionLength = 0;

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
                        _ClearScan();
                    }
                }
                else
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
        /// Confirm stocker ID
        /// </summary>
        private async Task ConfirmStockerAsync()
        {
            if (string.IsNullOrWhiteSpace(txtStocker.Text))
                return;

            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                await DisplayAlert("Error", "Cannot connect to server!", "OK");
                return;
            }

            try
            {
                using var sqlCmd = conn.CreateCommand();
                sqlCmd.CommandText =
                    "SELECT ID, (LName + ', ' + FName + ' ' + MI) AS FullName " +
                    "FROM tblUsers " +
                    "WHERE EENo = @EENo AND isStocker = 1 AND isActive = 1";
                sqlCmd.Parameters.AddWithValue("@EENo", txtStocker.Text.Trim());

                using var reader = await sqlCmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    ID_Stocker = Convert.ToInt32(reader["ID"]);
                    txtStockerTag = reader["ID"].ToString().Trim();

                    await DisplayAlert("Stocker Name:", reader["FullName"].ToString().Trim(), "OK");
                    btnConfirm.Focus();
                }
                else
                {
                    txtStockerTag = "";
                    await DisplayAlert("Not Found!", "Stocker ID not found!", "OK");
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

        /// <summary>
        /// Timer tick handler for request checking
        /// </summary>
        private async void TmrRequest_Tick(object sender, EventArgs e)
        {
            try
            {
                using var conn = await AppGlobal._SQL_Connect();
                if (conn == null) return;

                // Check if request has been sent
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

                // Retry logic
                if (iRetry >= 5)
                {
                    tmrRequest.Stop();
                    iRetry = 0;

                    if (!_requestFailedShown)
                    {
                        _requestFailedShown = true;

                        using var resetCmd = new SqlCommand(
                            "UPDATE tblUsers SET isRequest=0, isSummary=0 WHERE ID=@ID", conn);
                        resetCmd.Parameters.AddWithValue("@ID", AppGlobal.ID_User);
                        await resetCmd.ExecuteNonQueryAsync();

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

        // ================== HARDWARE KEY HANDLERS ==================

        public void OnF1Pressed()
        {
            if (txtLine.IsFocused)
            {
                pnlGoto.IsVisible = false;
                txtBarcode.Focus();
                return;
            }

            if (txtStocker.IsFocused)
            {
                BtnCancel_Clicked(null, null);
                return;
            }

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

        public void OnEscapePressed()
        {
            if (txtLine.IsFocused)
            {
                pnlGoto.IsVisible = false;
                txtBarcode.Focus();
            }
            else if (txtStocker.IsFocused)
            {
                BtnCancel_Clicked(null, null);
            }
            else
            {
                if (Navigation.NavigationStack.Count > 0)
                {
                    MainThread.BeginInvokeOnMainThread(async () => await Navigation.PopAsync());
                }
            }
        }

        // ================== DATA CLASSES ==================

        public class BarcodeItem
        {
            public string Text { get; set; } = "";
            public string UPC { get; set; } = "";
        }

        public class SlotItem
        {
            public string Text { get; set; } = "";
            public string Slot { get; set; } = "";
        }

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

            // For display
            public string PickedQtyDisplay
            {
                get => PickedQty > 0 ? PickedQty.ToString("N2") : "";
            }

            // Color indicator for picked items
            public Color PickedQtyColor
            {
                get => PickedQty > 0 ? Colors.Green : Colors.Black;
            }
        }
    }
}