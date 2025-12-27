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
using Microsoft.Maui.Dispatching;
using Plugin.Maui.Audio; // ✅ NEW: For alarm sound

namespace PDTPickingSystem.Views
{
    public partial class CheckingPage : ContentPage
    {
        // ================== PRIVATE FIELDS ==================

        // Focus tracking
        private Entry _focusedEntry;

        // SKU and stocker tracking
        private int iSKU;
        private int sSKU = -1;
        private int ID_Stocker = 0;

        // Checking state
        private bool isStarted = false;
        private bool isBarcode = true;
        private int chkqty = 0;
        private int pickqty = 0;

        // SKU tracking
        private string Gsku = string.Empty;
        private int lvCnt = 1;
        private List<int> skuArr = new List<int>();
        private string txtEachVal = string.Empty;
        private int scanCount = 0;

        // Timer for request checking
        private IDispatcherTimer tmrRequest;
        private int iRetry = 0;

        // Data collections
        public ObservableCollection<SKUItem> SKUList { get; set; } = new ObservableCollection<SKUItem>();
        public ObservableCollection<SKUItem> SKUList2 { get; set; } = new ObservableCollection<SKUItem>();

        // Tag replacements
        private string txtStockerTag = "";
        private string txtpSKU_UPC = "";

        // Request tracking flags
        private bool _requestAlreadyShown = false;
        private bool _requestFailedShown = false;

        // MainMenu reference
        private MainMenuPage _mainMenu;

        // Summary mode
        private int isSummary = 0;

        // ================== ✅ NEW: IDLE MONITORING FIELDS ==================

        /// <summary>
        /// Timer that checks for idle state every second
        /// </summary>
        private IDispatcherTimer _idleCheckTimer;

        /// <summary>
        /// Last time user interacted with the app
        /// </summary>
        private DateTime _lastActivityTime;

        /// <summary>
        /// Idle timeout duration (1 minute)
        /// </summary>
        private readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Warning timeout (50 seconds - 10 seconds before alarm)
        /// </summary>
        private readonly TimeSpan _warningTimeout = TimeSpan.FromSeconds(50);

        /// <summary>
        /// Flag to prevent multiple alert popups
        /// </summary>
        private bool _idleAlertShown = false;

        /// <summary>
        /// Flag to prevent multiple warning popups
        /// </summary>
        private bool _warningAlertShown = false;

        /// <summary>
        /// Audio player for alarm sound
        /// </summary>
        private IAudioPlayer _alarmPlayer;

        /// <summary>
        /// Flag to track if checking is currently active
        /// </summary>
        private bool _isCheckingActive = false;

        // ================== CONSTRUCTOR ==================

        public CheckingPage(MainMenuPage mainMenu)
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            _mainMenu = mainMenu;

            // Initialize timer
            tmrRequest = Dispatcher.CreateTimer();
            tmrRequest.Interval = TimeSpan.FromSeconds(1);
            tmrRequest.Tick += TmrRequest_Tick;

            // Initialize fields
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

            // Bind CollectionViews
            lvSKU.ItemsSource = SKUList;
            lvSKU2.ItemsSource = SKUList2;

            // ===== Entry Completed events =====
            txtBarcode.Completed += Entry_Completed;
            txtCase.Completed += Entry_Completed;
            txtEach.Completed += Entry_Completed;
            txtStocker.Completed += TxtStocker_Completed;

            // ===== TextChanged events =====
            txtBarcode.TextChanged += Entry_TextChanged;
            txtCase.TextChanged += Entry_TextChanged;
            txtEach.TextChanged += Entry_TextChanged;
            txtStocker.TextChanged += TxtStocker_TextChanged;
            txtDeptStore.TextChanged += TxtDeptStore_TextChanged;
            txtDesc.TextChanged += TxtDesc_TextChanged;
            txtSKU.TextChanged += TxtSKU_TextChanged;
            txtBum.TextChanged += TxtBum_TextChanged;
            txtDone.TextChanged += TxtDone_TextChanged;

            // ===== Focus events =====
            txtBarcode.Unfocused += TxtEntry_Unfocused;
            txtStocker.Unfocused += TxtEntry_Unfocused;
            txtCase.Unfocused += TxtCaseEach_Unfocused;
            txtEach.Unfocused += TxtCaseEach_Unfocused;

            txtBarcode.Focused += TxtBarcodeQtyFocus_Focused;
            txtStocker.Focused += TxtBarcodeQtyFocus_Focused;
            txtEach.Focused += TxtBarcodeQtyFocus_Focused;
            txtCase.Focused += TxtBarcodeQtyFocus_Focused;

            // ===== Other entry focus (force back to barcode) =====
            txtpSKU.Focused += TxtOther_Focused;
            txtpDescr.Focused += TxtOther_Focused;
            txtpSlot.Focused += TxtOther_Focused;
            txtpEach.Focused += TxtOther_Focused;
            txtpCase.Focused += TxtOther_Focused;
            txtSKU.Focused += TxtOther_Focused;
            txtDone.Focused += TxtOther_Focused;
            txtDeptStore.Focused += TxtOther_Focused;

            // ===== Button events =====
            btnAccept.Clicked += BtnAccept_Clicked;
            btnFinished.Clicked += BtnFinished_Clicked;
            btnViewItems.Clicked += BtnViewItems_Clicked;
            btnConso.Clicked += BtnConso_Clicked;
            btnCloseItems.Clicked += BtnCloseItems_Clicked;
            btnCloseItems2.Clicked += BtnCloseItems2_Clicked;
            btnConfirm.Clicked += BtnConfirm_Clicked;
            btnCancel.Clicked += BtnCancel_Clicked;

            // ===== Loaded events =====
            lblBarcode.Loaded += LblBarcode_Loaded;
            lblSKU.Loaded += LblSKU_Loaded;
            lblCase.Loaded += LblCase_Loaded;
            lblEach.Loaded += LblEach_Loaded;
            lblBUM.Loaded += LblBUM_Loaded;
            lblDone.Loaded += LblDone_Loaded;
            lblTransfer.Loaded += LblTransfer_Loaded;
            lblLoc.Loaded += LblLoc_Loaded;
            lblPicker.Loaded += LblPicker_Loaded;
            lblTrf.Loaded += LblTrf_Loaded;
            lblDeptStore.Loaded += LblDeptStore_Loaded;

            // ===== CollectionView selection =====
            lvSKU.SelectionChanged += LvSKU_SelectionChanged;

            // Page events
            Appearing += CheckingPage_Appearing;
            Disappearing += CheckingPage_Disappearing;

            // ✅ NEW: Initialize idle monitoring
            _InitializeIdleMonitoring();

            // ✅ NEW: Attach activity tracking to all interactive controls
            _AttachActivityTracking();
        }

        // ================== ✅ NEW: IDLE MONITORING INITIALIZATION ==================

        /// <summary>
        /// Initialize idle monitoring system
        /// </summary>
        private void _InitializeIdleMonitoring()
        {
            // Initialize last activity time
            _lastActivityTime = DateTime.Now;

            // Create idle check timer (checks every second)
            _idleCheckTimer = Dispatcher.CreateTimer();
            _idleCheckTimer.Interval = TimeSpan.FromSeconds(1);
            _idleCheckTimer.Tick += IdleCheckTimer_Tick;

            // Don't start timer yet - starts when checking begins
        }

        /// <summary>
        /// Attach activity tracking to all interactive controls
        /// </summary>
        private void _AttachActivityTracking()
        {
            // Track Entry interactions
            txtBarcode.TextChanged += OnUserActivity;
            txtBarcode.Focused += OnUserActivity;
            txtCase.TextChanged += OnUserActivity;
            txtCase.Focused += OnUserActivity;
            txtEach.TextChanged += OnUserActivity;
            txtEach.Focused += OnUserActivity;
            txtStocker.TextChanged += OnUserActivity;
            txtStocker.Focused += OnUserActivity;

            // Track Button clicks
            btnAccept.Clicked += OnUserActivity;
            btnFinished.Clicked += OnUserActivity;
            btnViewItems.Clicked += OnUserActivity;
            btnConso.Clicked += OnUserActivity;
            btnCloseItems.Clicked += OnUserActivity;
            btnCloseItems2.Clicked += OnUserActivity;
            btnConfirm.Clicked += OnUserActivity;
            btnCancel.Clicked += OnUserActivity;

            // Track CollectionView interactions
            lvSKU.SelectionChanged += OnUserActivity;
        }

        // ================== ✅ NEW: ACTIVITY TRACKING ==================

        /// <summary>
        /// Called whenever user interacts with the app
        /// </summary>
        private void OnUserActivity(object sender, EventArgs e)
        {
            _ResetIdleTimer();
        }

        /// <summary>
        /// Reset idle timer when user is active
        /// </summary>
        private void _ResetIdleTimer()
        {
            _lastActivityTime = DateTime.Now;
            _idleAlertShown = false;
            _warningAlertShown = false;

            // Stop alarm if playing
            _StopAlarm();
        }

        /// <summary>
        /// Start idle monitoring (when checking begins)
        /// </summary>
        private void _StartIdleMonitoring()
        {
            _isCheckingActive = true;
            _lastActivityTime = DateTime.Now;
            _idleAlertShown = false;
            _warningAlertShown = false;

            if (!_idleCheckTimer.IsRunning)
            {
                _idleCheckTimer.Start();
            }
        }

        /// <summary>
        /// Stop idle monitoring (when checking ends or page closes)
        /// </summary>
        private void _StopIdleMonitoring()
        {
            _isCheckingActive = false;
            _idleCheckTimer?.Stop();
            _StopAlarm();
        }

        // ================== ✅ NEW: IDLE CHECK TIMER ==================

        /// <summary>
        /// Timer tick - checks for idle state every second
        /// </summary>
        private async void IdleCheckTimer_Tick(object sender, EventArgs e)
        {
            // Only monitor if checking is active
            if (!_isCheckingActive)
                return;

            var idleTime = DateTime.Now - _lastActivityTime;

            // Check for warning threshold (50 seconds - 10 seconds before alarm)
            if (idleTime >= _warningTimeout && !_warningAlertShown)
            {
                _warningAlertShown = true;
                await _ShowWarningAlert();
            }

            // Check for idle threshold (1 minute)
            if (idleTime >= _idleTimeout && !_idleAlertShown)
            {
                _idleAlertShown = true;
                await _ShowIdleAlert();
                await _PlayAlarm();
            }
        }

        // ================== ✅ NEW: ALERT & ALARM FUNCTIONS ==================

        /// <summary>
        /// Show warning alert (10 seconds before alarm)
        /// </summary>
        private async Task _ShowWarningAlert()
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Vibrate device
                _VibrateDevice(500);

                var result = await DisplayAlert(
                    "⚠️ Idle Warning",
                    "You have been idle for 50 seconds.\n\n" +
                    "Alarm will sound in 10 seconds if no activity detected.\n\n" +
                    "Tap 'Continue' to resume checking.",
                    "Continue",
                    "Cancel"
                );

                if (result)
                {
                    // User tapped Continue - reset timer
                    _ResetIdleTimer();
                }
            });
        }

        /// <summary>
        /// Show idle alert with alarm
        /// </summary>
        private async Task _ShowIdleAlert()
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Vibrate device strongly
                _VibrateDevice(1000);

                var result = await DisplayAlert(
                    "🚨 IDLE ALERT! 🚨",
                    "You have been idle for 1 minute!\n\n" +
                    "Checking is still in progress.\n\n" +
                    "Tap 'Resume' to continue checking.\n" +
                    "Tap 'Finish' to complete checking.",
                    "Resume",
                    "Finish"
                );

                if (result)
                {
                    // User tapped Resume
                    _ResetIdleTimer();
                    txtBarcode.Focus();
                }
                else
                {
                    // User tapped Finish
                    _StopAlarm();
                    BtnFinished_Clicked(null, null);
                }
            });
        }

        /// <summary>
        /// Play alarm sound
        /// </summary>
        private async Task _PlayAlarm()
        {
            try
            {
                // Get audio manager
                var audioManager = Plugin.Maui.Audio.AudioManager.Current;

                // Load alarm sound from embedded resource
                var alarmStream = await FileSystem.OpenAppPackageFileAsync("alarm_sound.mp3");

                if (alarmStream != null)
                {
                    _alarmPlayer = audioManager.CreatePlayer(alarmStream);

                    // Set to loop and max volume
                    _alarmPlayer.Loop = true;
                    _alarmPlayer.Volume = 1.0;

                    _alarmPlayer.Play();
                }
            }
            catch (Exception ex)
            {
                // Fallback: Use system beep if audio file fails
                System.Diagnostics.Debug.WriteLine($"Alarm audio failed: {ex.Message}");
                _PlaySystemBeep();
            }
        }

        /// <summary>
        /// Stop alarm sound
        /// </summary>
        private void _StopAlarm()
        {
            try
            {
                if (_alarmPlayer != null && _alarmPlayer.IsPlaying)
                {
                    _alarmPlayer.Stop();
                    _alarmPlayer.Dispose();
                    _alarmPlayer = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Stop alarm failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Fallback: Play system beep sound (repeating)
        /// </summary>
        private async void _PlaySystemBeep()
        {
            // Play 5 beeps in a row
            for (int i = 0; i < 5; i++)
            {
                _VibrateDevice(200);
                await Task.Delay(300);
            }
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

        // ================== PAGE LIFECYCLE ==================

        private async void CheckingPage_Appearing(object sender, EventArgs e)
        {
            _SetUser(lblTrf);

            txtBarcode.Text = string.Empty;
            txtEachVal = string.Empty;
            txtBarcode.Focus();

            pnlItems.IsVisible = false;
            pnlitems2.IsVisible = false;
            pbReq.IsVisible = true;

            isBarcode = true;
            btnFinished.IsVisible = false;

            await _GetSetPickNoAsync();

            scanCount = 0;
        }

        private void CheckingPage_Disappearing(object sender, EventArgs e)
        {
            tmrRequest?.Stop();

            // ✅ NEW: Stop idle monitoring and alarm
            _StopIdleMonitoring();
        }

        // ================== FOCUS MANAGEMENT ==================

        private void TxtEntry_Unfocused(object sender, FocusEventArgs e)
        {
            if (sender is Entry entry)
            {
                entry.BackgroundColor = Colors.WhiteSmoke;
            }
        }

        private void TxtCaseEach_Unfocused(object sender, FocusEventArgs e)
        {
            if (sender is Entry entry)
            {
                entry.SelectionLength = 0;
                entry.CursorPosition = 0;
            }
        }

        private void TxtBarcodeQtyFocus_Focused(object sender, FocusEventArgs e)
        {
            // ✅ ADDED: Reset idle timer on focus
            _ResetIdleTimer();

            if (sender is Entry entry)
            {
                _focusedEntry = entry;

                if (entry == txtBarcode || entry == txtStocker)
                {
                    isBarcode = entry == txtBarcode;
                    entry.BackgroundColor = Colors.PaleGreen;
                }
                else
                {
                    entry.CursorPosition = 0;
                    entry.SelectionLength = entry.Text?.Length ?? 0;
                }
            }
        }

        private void TxtOther_Focused(object sender, FocusEventArgs e)
        {
            _focusedEntry?.Focus();
        }

        // ================== ENTRY VALIDATION & COMPLETION ==================

        private async void Entry_Completed(object sender, EventArgs e)
        {
            // ✅ ADDED: Reset idle timer on entry completion
            _ResetIdleTimer();

            var entry = sender as Entry;
            if (entry == null) return;

            if (entry.Text != null && entry.Text.Any(c => AppGlobal._isAllowedNum(c) == '\0'))
            {
                entry.Text = "";
                return;
            }

            pbScanned.IsVisible = false;
            txtDesc.Text = string.Empty;

            if (entry == txtBarcode)
            {
                txtBarcode.Text = double.TryParse(txtBarcode.Text.Trim(), out double val)
                    ? val.ToString()
                    : txtBarcode.Text;

                if (!_isUPCFound(txtBarcode.Text.Trim()))
                {
                    await DisplayAlert("Mismatch!", "Wrong scanned item!", "OK");
                    _ClearScan();
                }
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

        private void Entry_TextChanged(object sender, TextChangedEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;

            if (e.NewTextValue != null && e.NewTextValue.Any(c => AppGlobal._isAllowedNum(c) == '\0'))
            {
                entry.Text = e.OldTextValue;
            }
        }

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

        private void TxtStocker_Completed(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtStocker.Text))
            {
                _ = ConfirmStockerAsync();
            }
        }

        // ================== TEXT CHANGED HANDLERS ==================

        private void TxtDeptStore_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtDesc_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtSKU_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtBum_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtDone_TextChanged(object sender, TextChangedEventArgs e) { }

        // ================== BUTTON CLICK HANDLERS ==================

        private async void BtnAccept_Clicked(object sender, EventArgs e)
        {
            // ✅ ADDED: Reset idle timer on button click
            _ResetIdleTimer();

            if (string.IsNullOrWhiteSpace(txtSKU.Text))
                return;

            if (!string.IsNullOrWhiteSpace(txtEach.Text) &&
                !string.IsNullOrWhiteSpace(txtCase.Text) &&
                double.TryParse(txtEach.Text, out double eachVal) && eachVal >= 0 &&
                double.TryParse(txtCase.Text, out double caseVal) && caseVal >= 0)
            {
                bool answer = await DisplayAlert("Accept?", "Accept quantity?", "Yes", "No");
                if (answer)
                {
                    await _AcceptItemAsync();
                }
            }
        }

        private async void BtnFinished_Clicked(object sender, EventArgs e)
        {
            // ✅ ADDED: Stop idle monitoring when finishing
            _StopIdleMonitoring();

            bool answer = await DisplayAlert("System Says", "Done Checking? Please Verify", "Yes", "No");
            if (!answer)
            {
                // ✅ ADDED: Restart monitoring if user cancels
                _StartIdleMonitoring();
                return;
            }

            answer = await DisplayAlert("Finish?", "Finish Checking?", "Yes", "No");
            if (!answer)
            {
                // ✅ ADDED: Restart monitoring if user cancels
                _StartIdleMonitoring();
                return;
            }

            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                await DisplayAlert("No Connection!", "Cannot connect to server!", "OK");
                _StartIdleMonitoring(); // ✅ ADDED: Restart monitoring
                return;
            }

            try
            {
                using var sqlCmd = conn.CreateCommand();

                // Update items with zero checked quantity
                foreach (var skuItem in SKUList)
                {
                    if (string.IsNullOrEmpty(skuItem.ChkQty) ||
                        (double.TryParse(skuItem.ChkQty, out double chkQtyVal) && chkQtyVal == 0))
                    {
                        sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                                             "isUpdate=1, checkTime='00:00:00', isCSorted=1, CheckBy=@UserID " +
                                             "WHERE SKU=@SKU AND ID_SumHdr=@ID_SumHdr";
                        sqlCmd.Parameters.Clear();
                        sqlCmd.Parameters.AddWithValue("@UserID", AppGlobal.ID_User);
                        sqlCmd.Parameters.AddWithValue("@SKU", skuItem.SKU);
                        sqlCmd.Parameters.AddWithValue("@ID_SumHdr", AppGlobal.ID_SumHdr);
                        await sqlCmd.ExecuteNonQueryAsync();
                    }

                    // Update items with multiple slots
                    if (!string.IsNullOrEmpty(skuItem.Slot) && skuItem.Slot.Split(',').Length > 1)
                    {
                        sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                                             "isCSorted=1, CheckBy=@UserID " +
                                             "WHERE SKU=@SKU AND ID_SumHdr=@ID_SumHdr";
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
                string updateChkStart = scanCount < 1 ? "chkStart=@chkStart," : "";
                sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickHdr SET " +
                                     $"{updateChkStart}isUpdate=1, chkEnd=@chkEnd, chkDateDone=@chkDateDone " +
                                     "WHERE ID=@ID_SumHdr";
                sqlCmd.Parameters.Clear();
                if (scanCount < 1)
                    sqlCmd.Parameters.AddWithValue("@chkStart", await AppGlobal._GetDateTime());
                sqlCmd.Parameters.AddWithValue("@chkEnd", await AppGlobal._GetDateTime());
                sqlCmd.Parameters.AddWithValue("@chkDateDone", await AppGlobal._GetDateTime(true));
                sqlCmd.Parameters.AddWithValue("@ID_SumHdr", AppGlobal.ID_SumHdr);
                await sqlCmd.ExecuteNonQueryAsync();

                // Reset user sum header
                sqlCmd.CommandText = "UPDATE tblUsers SET ID_SumHdr=0 WHERE ID=@UserID";
                sqlCmd.Parameters.Clear();
                sqlCmd.Parameters.AddWithValue("@UserID", AppGlobal.ID_User);
                await sqlCmd.ExecuteNonQueryAsync();

                scanCount = 0;
                await _GetSetPickNoAsync();
            }
            catch (SqlException ex)
            {
                await DisplayAlert("Error!", ex.Message, "OK");
                _StartIdleMonitoring(); // ✅ ADDED: Restart monitoring on error
            }
        }

        private void BtnViewItems_Clicked(object sender, EventArgs e)
        {
            pnlItems.IsVisible = true;
            lblCnt.Text = $"Count: {SKUList.Count}";
            lvSKU.Focus();

            if (!string.IsNullOrWhiteSpace(txtSKU.Text) && SKUList.Count > 0)
            {
                int index = _getIndexLV();
                if (index >= 0 && index < SKUList.Count)
                {
                    lvSKU.ScrollTo(SKUList[index], position: ScrollToPosition.MakeVisible, animate: true);
                    lvSKU.SelectedItem = SKUList[index];
                }
            }
        }

        private void BtnConso_Clicked(object sender, EventArgs e)
        {
            pnlitems2.IsVisible = true;
            lblCnt2.Text = $"Count: {SKUList2.Count}";
        }

        private void BtnCloseItems_Clicked(object sender, EventArgs e)
        {
            pnlItems.IsVisible = false;
            txtBarcode.Focus();
        }

        private void BtnCloseItems2_Clicked(object sender, EventArgs e)
        {
            pnlitems2.IsVisible = false;
            txtBarcode.Focus();
        }

        private async void BtnConfirm_Clicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtStockerTag))
            {
                await DisplayAlert("OK", "Item Confirmed!", "OK");
                ID_Stocker = int.Parse(txtStockerTag);
                BtnCancel_Clicked(null, null);
                await _AcceptItemAsync();
            }
        }

        private void BtnCancel_Clicked(object sender, EventArgs e)
        {
            pnlNavigate.IsVisible = true;
            pnlInput.IsVisible = true;
            pnlConfirm.IsVisible = false;
            txtBarcode.Focus();
        }

        // ================== COLLECTION VIEW SELECTION ==================

        private async void LvSKU_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvSKU.SelectedItem is SKUItem selectedItem)
            {
                if (string.IsNullOrWhiteSpace(selectedItem.ChkQty))
                {
                    bool answer = await DisplayAlert("System Says", "Receive as OS?", "Yes", "No");
                    if (answer)
                    {
                        txtBarcode.Text = selectedItem.UPC?.Replace("-", "").Replace(",", "").Trim();
                        BtnCloseItems_Clicked(null, null);

                        var upcCount = selectedItem.UPC?.Split(',').Length ?? 0;
                        if (upcCount > 0)
                        {
                            // Additional logic if needed
                        }
                    }
                }
                lvSKU.SelectedItem = null;
            }
        }

        // ================== LOADED EVENT HANDLERS ==================

        private void LblBarcode_Loaded(object sender, EventArgs e) { }
        private void LblSKU_Loaded(object sender, EventArgs e) { }
        private void LblCase_Loaded(object sender, EventArgs e) { }
        private void LblEach_Loaded(object sender, EventArgs e) { }
        private void LblBUM_Loaded(object sender, EventArgs e) { }
        private void LblDone_Loaded(object sender, EventArgs e) { }
        private void LblTransfer_Loaded(object sender, EventArgs e) { }
        private void LblLoc_Loaded(object sender, EventArgs e) { }
        private void LblPicker_Loaded(object sender, EventArgs e) { }
        private void LblTrf_Loaded(object sender, EventArgs e) { }
        private void LblDeptStore_Loaded(object sender, EventArgs e) { }

        // ================== CORE BUSINESS LOGIC ==================

        /// <summary>
        /// Initialize checking session and load data from server
        /// </summary>
        private async Task _GetSetPickNoAsync()
        {
            btnFinished.IsVisible = false;
            pbReq.IsVisible = true;

            string sUserPNo = "";

            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                await DisplayAlert("No Connection!", "Cannot connect to server! Please retry or check settings...", "OK");
                await Navigation.PopAsync();
                return;
            }

            try
            {
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
                reader.Close();

                // Check if user has active checking session
                if (AppGlobal.ID_SumHdr != 0 && sUserPNo == AppGlobal.pPickNo)
                {
                    txtSKU.Text = string.Empty;
                    await _AddSKUtoListAsync();
                    return;
                }

                // Request from server (show once only)
                if (!_requestAlreadyShown)
                {
                    _requestAlreadyShown = true;

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
                string cmdStr = $"SELECT * FROM tbl{AppGlobal.pPickNo}PickHdr WHERE ID=@SumHdr";
                using var sqlCmd = new SqlCommand(cmdStr, conn);
                sqlCmd.Parameters.AddWithValue("@SumHdr", AppGlobal.ID_SumHdr);

                using var reader = await sqlCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    lblDeptStore.Text = isSummary == 1 ? "Department:" : "Store No:";

                    if (isSummary == 1)
                    {
                        txtDeptStore.Text = await AppGlobal._GetDeptName(Convert.ToInt32(reader["iDept"]));
                    }
                    else
                    {
                        txtDeptStore.Text = await _GetStoreNoAsync();
                    }

                    lblPicker.Text = " Picker : " + await AppGlobal._GetUserName(reader["User_ID"].ToString());

                    // Do not allow not yet picked transfers
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
                var dsData = new DataSet();
                string cmdData = $@"
                    SELECT a.*, b.toLoc, b.Tranno 
                    FROM tbl{AppGlobal.pPickNo}PickQty a 
                    LEFT JOIN (
                        SELECT DISTINCT id_sumhdr, toLoc, Tranno 
                        FROM tbl{AppGlobal.pPickNo}PickDtl
                    ) b ON a.id_sumhdr = b.id_sumhdr 
                    WHERE a.ID_SumHdr=@SumHdr 
                    ORDER BY a.slot, a.sku";

                using (var loadCmd = new SqlCommand(cmdData, conn))
                {
                    loadCmd.Parameters.AddWithValue("@SumHdr", AppGlobal.ID_SumHdr);
                    using var adapter = new SqlDataAdapter(loadCmd);
                    adapter.Fill(dsData, "DATA");
                }

                SKUList.Clear();
                SKUList2.Clear();
                lvCnt = 0;

                foreach (DataRow row in dsData.Tables[0].Rows)
                {
                    // Add to main list (SKUList)
                    var item = new SKUItem
                    {
                        ID = row["ID"].ToString().Trim(),
                        BUM = row["BUM"].ToString().Trim(),
                        Slot = row["Slot"].ToString().Trim(),
                        SKU = row["SKU"].ToString().Trim(),
                        Descr = row["Descr"].ToString().Trim(),
                        Qty = row["Qty"].ToString().Trim(),
                        isPicked = row["isPicked"].ToString().Trim(),
                        PickQty = row["isPicked"].ToString().Trim() == "0" ? "" : row["PickQty"].ToString(),
                        UPC = row["UPC"].ToString().Trim(),
                        isChecked = row["isChecked"].ToString().Trim(),
                        ChkQty = row["isChecked"].ToString().Trim() == "0" ? "" : row["chkQty"].ToString()
                    };
                    SKUList.Add(item);

                    // Add to consolidated list (SKUList2) - unique SKUs
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

                if (dsData.Tables[0].Rows.Count > 0)
                {
                    var firstRow = dsData.Tables[0].Rows[0];
                    lblLoc.Text = "Location: " + firstRow["toLoc"].ToString();
                    lblTrf.Text = "Transfer # : " + firstRow["Tranno"].ToString();
                }

                dsData.Tables.Clear();
                sSKU = -1;
                _CountPicked();

                // ✅ NEW: Start idle monitoring - checking has begun!
                _StartIdleMonitoring();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        /// <summary>
        /// Load consolidated SKU list (unique SKUs)
        /// </summary>
        private void _loadlvSKU2(string id, string bum, string slot, string sku, string descr,
                                 string qty, string isPck, string pckqty, string upc,
                                 string isChecked, string chkQty)
        {
            // Check if SKU already exists in consolidated list
            var existingItem = SKUList2.FirstOrDefault(x => x.SKU == sku);

            if (existingItem != null)
            {
                // Sum the checked quantity
                if (double.TryParse(existingItem.ChkQty, out double existingChk) &&
                    double.TryParse(chkQty, out double addChk))
                {
                    existingItem.ChkQty = (existingChk + addChk).ToString();
                }
                return;
            }

            // Add new unique SKU
            var item = new SKUItem
            {
                ID = id,
                BUM = bum,
                Slot = slot,
                SKU = sku,
                Descr = descr,
                Qty = qty,
                isPicked = isPck,
                PickQty = isPck.Trim() == "0" ? "" : pckqty,
                UPC = upc.Trim(),
                isChecked = isChecked,
                ChkQty = isChecked.Trim() == "0" ? "" : chkQty
            };

            SKUList2.Add(item);
            lvCnt++;
        }

        /// <summary>
        /// Count checked items and update UI
        /// </summary>
        private void _CountPicked()
        {
            int iPicked = SKUList.Count(item => !string.IsNullOrWhiteSpace(item.ChkQty));
            txtDone.Text = $"{iPicked}/{SKUList.Count}";

            // Always show finish button if any items are checked or if list has items
            if (iPicked > 0 || iPicked == 0)
            {
                btnFinished.IsVisible = true;
            }

            pbReq.IsVisible = false;
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

                        await DisplayAlert("Unable to request!", "No picking no. available!", "OK");
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

            txtCase.CursorPosition = 0;
            txtCase.SelectionLength = 0;

            txtEach.CursorPosition = 0;
            txtEach.SelectionLength = 0;

            txtBarcode.Focus();
            txtBarcode.CursorPosition = 0;
            txtBarcode.SelectionLength = txtBarcode.Text?.Length ?? 0;
        }

        /// <summary>
        /// Accept checked item and update database
        /// </summary>
        private async Task _AcceptItemAsync()
        {
            // ✅ ADDED: Reset idle timer on successful action
            _ResetIdleTimer();

            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                await DisplayAlert("No Connection!", "Cannot connect to server!", "OK");
                return;
            }

            SqlTransaction txn = null;

            try
            {
                txn = conn.BeginTransaction();
                using var sqlCmd = conn.CreateCommand();
                sqlCmd.Transaction = txn;

                // Calculate checked quantity
                double dBum = ParseEntry(txtBum);
                double dCase = ParseEntry(txtCase);
                double dEach = string.IsNullOrWhiteSpace(txtEach.Text) ? 0 : ParseEntry(txtEach);
                double dQty = (dBum * dCase) + dEach;
                double totQty = 0.0;

                if (sSKU < 0 || sSKU >= SKUList.Count) return;
                var lvI = SKUList[sSKU];

                // Update PickHdr
                if (!isStarted)
                {
                    isStarted = true;
                    sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickHdr SET " +
                                         "isUpdate=1, chkStart=@chkStart WHERE ID=@ID_SumHdr";
                    sqlCmd.Parameters.AddWithValue("@chkStart", await AppGlobal._GetDateTime());
                    sqlCmd.Parameters.AddWithValue("@ID_SumHdr", AppGlobal.ID_SumHdr);
                }
                else
                {
                    sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickHdr SET " +
                                         "isUpdate=1 WHERE ID=@ID_SumHdr";
                    sqlCmd.Parameters.AddWithValue("@ID_SumHdr", AppGlobal.ID_SumHdr);
                }
                await sqlCmd.ExecuteNonQueryAsync();

                // Prepare UPC update
                string sUPC = "";
                if (!pbScanned.IsVisible && !string.IsNullOrWhiteSpace(txtBarcode.Text))
                    sUPC = "UPC=@UPC,";
                if (dEach == 0 && dCase == 0)
                    sUPC = "";

                // Update PickDtl
                if (isSummary == 1)
                {
                    sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                        $"{sUPC}PickBy=@PickBy, ConfBy=@ConfBy, PickTime=@PickTime " +
                        "WHERE SKU=@SKU AND ID_SumHdr=@SumHdr";
                    sqlCmd.Parameters.Clear();
                    if (!string.IsNullOrEmpty(sUPC))
                        sqlCmd.Parameters.AddWithValue("@UPC", txtBarcode.Text);
                    sqlCmd.Parameters.AddWithValue("@PickBy", AppGlobal.ID_User);
                    sqlCmd.Parameters.AddWithValue("@ConfBy", ID_Stocker);
                    sqlCmd.Parameters.AddWithValue("@PickTime", await AppGlobal._GetDateTime());
                    sqlCmd.Parameters.AddWithValue("@SKU", lvI.SKU);
                    sqlCmd.Parameters.AddWithValue("@SumHdr", AppGlobal.ID_SumHdr);
                    await sqlCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    // ✅ FIXED: Parameterized query
                    var dsData = new DataSet();

                    using (var readCmd = conn.CreateCommand())
                    {
                        readCmd.Transaction = txn;
                        readCmd.CommandText = $"SELECT ID, Qty FROM tbl{AppGlobal.pPickNo}PickDtl " +
                                              "WHERE SKU=@SKU AND ID_SumHdr=@SumHdr ORDER BY slot, sku";
                        readCmd.Parameters.AddWithValue("@SKU", lvI.SKU);
                        readCmd.Parameters.AddWithValue("@SumHdr", AppGlobal.ID_SumHdr);

                        using var adapter = new SqlDataAdapter(readCmd);
                        adapter.Fill(dsData, "DATA");
                    }

                    // Update all PickDtl records
                    sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                        $"{sUPC}SortBy=@SortBy, CheckBy=@CheckBy, ConfBy=@ConfBy, CheckTime=@CheckTime " +
                        "WHERE SKU=@SKU AND ID_SumHdr=@SumHdr";
                    sqlCmd.Parameters.Clear();
                    if (!string.IsNullOrEmpty(sUPC))
                        sqlCmd.Parameters.AddWithValue("@UPC", txtBarcode.Text);
                    sqlCmd.Parameters.AddWithValue("@SortBy", AppGlobal.ID_User);
                    sqlCmd.Parameters.AddWithValue("@CheckBy", AppGlobal.ID_User);
                    sqlCmd.Parameters.AddWithValue("@ConfBy", ID_Stocker);
                    sqlCmd.Parameters.AddWithValue("@CheckTime", await AppGlobal._GetDateTime());
                    sqlCmd.Parameters.AddWithValue("@SKU", lvI.SKU);
                    sqlCmd.Parameters.AddWithValue("@SumHdr", AppGlobal.ID_SumHdr);
                    await sqlCmd.ExecuteNonQueryAsync();

                    // Distribute checked quantity across multiple detail records
                    if (dsData.Tables["DATA"] != null && dsData.Tables["DATA"].Rows.Count > 0)
                    {
                        int lCount = dsData.Tables["DATA"].Rows.Count - 1;

                        for (int iCount = 0; iCount <= lCount; iCount++)
                        {
                            var dRow = dsData.Tables["DATA"].Rows[iCount];
                            double dNeedQty = Convert.ToDouble(dRow["Qty"]);

                            if (iCount == lCount) // Last item
                            {
                                sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                                    "isCSorted=1, cSortQty=@cSortQty, isUpdate=1 WHERE ID=@ID";
                                sqlCmd.Parameters.Clear();
                                sqlCmd.Parameters.AddWithValue("@cSortQty", dQty);
                                sqlCmd.Parameters.AddWithValue("@ID", dRow["ID"]);
                                await sqlCmd.ExecuteNonQueryAsync();
                                totQty += dQty;
                            }
                            else
                            {
                                if (dQty >= dNeedQty)
                                {
                                    sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                                        "isCSorted=1, cSortQty=Qty, isUpdate=1 WHERE ID=@ID";
                                    sqlCmd.Parameters.Clear();
                                    sqlCmd.Parameters.AddWithValue("@ID", dRow["ID"]);
                                    await sqlCmd.ExecuteNonQueryAsync();
                                    dQty -= dNeedQty;
                                    totQty += dNeedQty;
                                }
                                else
                                {
                                    sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                                        "isCSorted=1, cSortQty=@cSortQty, isUpdate=1 WHERE ID=@ID";
                                    sqlCmd.Parameters.Clear();
                                    sqlCmd.Parameters.AddWithValue("@cSortQty", dQty);
                                    sqlCmd.Parameters.AddWithValue("@ID", dRow["ID"]);
                                    await sqlCmd.ExecuteNonQueryAsync();
                                    totQty += dQty;
                                    dQty = 0;
                                    break;
                                }
                            }
                        }

                        // Update ListView
                        lvI.ChkQty = totQty.ToString("N2");
                    }

                    dsData.Tables.Clear();
                }

                // Update PickQty
                sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickQty SET " +
                    "isChecked=1, chkQty=@chkQty WHERE ID=@ID";
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
                txn?.Rollback();
                await DisplayAlert("Transaction Error", $"Please Retry.\n{ex.Message}", "OK");
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
        /// Check if UPC is found in SKU list
        /// </summary>
        private bool _isUPCFound(string upc)
        {
            foreach (var item in SKUList)
            {
                if (!string.IsNullOrWhiteSpace(item.UPC) && item.UPC.Contains("-" + upc.Trim() + ","))
                {
                    // Check if already checked
                    if (double.TryParse(item.ChkQty, out double chkQty) && chkQty > 0)
                    {
                        _ = DisplayAlert("Checked!", $"SKU: {item.SKU} Checked already", "OK");
                        break;
                    }

                    // Check if quantity matches
                    if (double.TryParse(item.Qty, out double qty) &&
                        double.TryParse(item.ChkQty, out double chkQty2) &&
                        qty == chkQty2)
                    {
                        continue;
                    }

                    // Set current SKU index
                    sSKU = SKUList.IndexOf(item);

                    // Get duplicate SKU indices
                    _getDuplicateSKUIndex(item.SKU.Trim());

                    // Load details
                    double bumVal = double.TryParse(item.BUM, out double b) ? b : 0;
                    double qtyVal = double.TryParse(item.Qty, out double q) ? q : 0;

                    _loadDetails(item.SKU, bumVal, qtyVal, item.Descr);

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Get indices of duplicate SKUs
        /// </summary>
        private void _getDuplicateSKUIndex(string sku)
        {
            skuArr = new List<int>();

            for (int i = 0; i < SKUList.Count; i++)
            {
                var item = SKUList[i];
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

            txtBarcode.SelectionLength = 0;

            if (double.TryParse(txtCase.Text, out var cv) && cv == 0)
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

        /// <summary>
        /// Get index of SKU in list matching current txtSKU
        /// </summary>
        private int _getIndexLV()
        {
            for (int i = 0; i < SKUList.Count; i++)
            {
                var item = SKUList[i];
                if (item.SKU?.Trim() == txtSKU.Text.Trim())
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Get store number
        /// </summary>
        private async Task<string> _GetStoreNoAsync()
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

        /// <summary>
        /// Set user label
        /// </summary>
        private void _SetUser(Label lbl)
        {
            if (lbl != null)
                lbl.Text = $"User: {AppGlobal.sUserName}";
        }

        /// <summary>
        /// Parse entry text to double
        /// </summary>
        private double ParseEntry(Entry entry)
        {
            return double.TryParse(entry.Text, out double val) ? val : 0;
        }

        // ================== HARDWARE KEY HANDLERS ==================

        public void OnF1Pressed()
        {
            // ✅ ADDED: Reset idle timer on hardware key
            _ResetIdleTimer();

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
            // ✅ ADDED: Reset idle timer on hardware key
            _ResetIdleTimer();
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
            // ✅ ADDED: Stop monitoring on Escape
            _StopIdleMonitoring();
            if (txtStocker.IsFocused)
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

        // ================== DATA CLASS ==================

        public class SKUItem
        {
            public string ID { get; set; }
            public string BUM { get; set; }
            public string Slot { get; set; }
            public string SKU { get; set; }
            public string Descr { get; set; }
            public string Qty { get; set; }
            public string isPicked { get; set; }
            public string PickQty { get; set; }
            public string UPC { get; set; }
            public string isChecked { get; set; }
            public string ChkQty { get; set; }

            // Display properties
            public string ChkQtyDisplay => !string.IsNullOrEmpty(ChkQty) ? ChkQty : "";
            public Color ChkQtyColor => !string.IsNullOrEmpty(ChkQty) ? Colors.Green : Colors.Black;
        }
    }
}