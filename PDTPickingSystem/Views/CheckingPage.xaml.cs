using Android.Media.TV;
using Android.OS;
using Microsoft.Data.SqlClient;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using PDTPickingSystem.Helpers;
using Plugin.Maui.Audio;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace PDTPickingSystem.Views
{
    public partial class CheckingPage : ContentPage
    {
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
        public ObservableCollection<SKUItem> SKUList  { get; set; } = new ObservableCollection<SKUItem>();
        public ObservableCollection<SKUItem> SKUList2 { get; set; } = new ObservableCollection<SKUItem>();

        // Tag replacements
        private string txtStockerTag = "";
        private string txtpSKU_UPC   = "";

        // Request tracking flags
        private bool _requestAlreadyShown = false;
        private bool _requestFailedShown  = false;

        // MainMenu reference
        private MainMenuPage _mainMenu;

        // Summary mode
        private int isSummary = 2;

        // Concurrency guards
        private bool _isRequesting    = false;
        private bool _isAccepting     = false;
        private bool _isFinishing     = false;
        private bool _isShowingAlert  = false;
        private bool _isViewingItems  = false;

        // Auto-assignment: after the checker confirms the very first request
        private bool _hasEverRequested = false;

        // Idle Monitoring fields
        // Timer that checks for idle state every second
        private IDispatcherTimer _idleCheckTimer;

        // Last time user interacted with the app
        private DateTime _lastActivityTime;

        // Idle timeout duration (1 minute)
        private readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(1);

        // Warning timeout (50 seconds – 10 seconds before alarm)
        private readonly TimeSpan _warningTimeout = TimeSpan.FromSeconds(50);

        // Flag to prevent multiple alert popups
        private bool _idleAlertShown = false;

        // Flag to prevent multiple warning popups
        private bool _warningAlertShown = false;

        // Audio player for alarm sound
        private IAudioPlayer _alarmPlayer;

        // Flag to track if checking is currently active
        private bool _isCheckingActive = false;

        // Loading Animation

        // Timer for truck animation
        private IDispatcherTimer _truckAnimationTimer;

        // Current truck position
        private double _truckPosition = 0;

        // Timer for loading dots animation
        private IDispatcherTimer _dotsAnimationTimer;

        // Current dot animation step
        private int _dotAnimationStep = 0;

        // Constructor

        public CheckingPage(MainMenuPage mainMenu)
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            _mainMenu = mainMenu;

            // Initialize timer
            tmrRequest          = Dispatcher.CreateTimer();
            tmrRequest.Interval = TimeSpan.FromSeconds(1);
            tmrRequest.Tick    += TmrRequest_Tick;

            // Initialize fields
            _focusedEntry = null;
            iSKU          = 0;
            sSKU          = -1;
            ID_Stocker    = 0;
            isStarted     = false;
            chkqty        = 0;
            pickqty       = 0;
            Gsku          = string.Empty;
            lvCnt         = 1;
            skuArr        = new List<int>();
            txtEachVal    = string.Empty;
            scanCount     = 0;

            lvSKU.ItemsSource  = SKUList;
            lvSKU2.ItemsSource = SKUList2;

            txtBarcode.Completed += Entry_Completed;
            txtCase.Completed    += Entry_Completed;
            txtEach.Completed    += Entry_Completed;
            txtStocker.Completed += TxtStocker_Completed;

            txtBarcode.TextChanged   += Entry_TextChanged;
            txtCase.TextChanged      += Entry_TextChanged;
            txtEach.TextChanged      += Entry_TextChanged;
            txtStocker.TextChanged   += TxtStocker_TextChanged;
            txtDeptStore.TextChanged += TxtDeptStore_TextChanged;
            txtDesc.TextChanged      += TxtDesc_TextChanged;
            txtSKU.TextChanged       += TxtSKU_TextChanged;
            txtBum.TextChanged       += TxtBum_TextChanged;
            txtDone.TextChanged      += TxtDone_TextChanged;

            txtBarcode.Unfocused += TxtEntry_Unfocused;
            txtStocker.Unfocused += TxtEntry_Unfocused;
            txtCase.Unfocused    += TxtCaseEach_Unfocused;
            txtEach.Unfocused    += TxtCaseEach_Unfocused;

            txtBarcode.Focused  += TxtBarcodeQtyFocus_Focused;
            txtStocker.Focused  += TxtBarcodeQtyFocus_Focused;
            txtEach.Focused     += TxtBarcodeQtyFocus_Focused;
            txtCase.Focused     += TxtBarcodeQtyFocus_Focused;

            txtpSKU.Focused    += TxtOther_Focused;
            txtpDescr.Focused  += TxtOther_Focused;
            txtpSlot.Focused   += TxtOther_Focused;
            txtpEach.Focused   += TxtOther_Focused;
            txtpCase.Focused   += TxtOther_Focused;
            txtSKU.Focused     += TxtOther_Focused;
            txtDone.Focused    += TxtOther_Focused;
            txtDeptStore.Focused += TxtOther_Focused;

            btnAccept.Clicked      += BtnAccept_Clicked;
            btnFinished.Clicked    += BtnFinished_Clicked;
            btnViewItems.Clicked   += BtnViewItems_Clicked;
            btnConso.Clicked       += BtnConso_Clicked;
            btnCloseItems.Clicked  += BtnCloseItems_Clicked;
            btnCloseItems2.Clicked += BtnCloseItems2_Clicked;
            btnConfirm.Clicked     += BtnConfirm_Clicked;
            btnCancel.Clicked      += BtnCancel_Clicked;

            lblBarcode.Loaded      += LblBarcode_Loaded;
            lblSKU.Loaded          += LblSKU_Loaded;
            lblCase.Loaded         += LblCase_Loaded;
            lblEach.Loaded         += LblEach_Loaded;
            lblBUM.Loaded          += LblBUM_Loaded;
            lblDone.Loaded         += LblDone_Loaded;
            lblTransfer.Loaded     += LblTransfer_Loaded;
            lblLoc.Loaded          += LblLoc_Loaded;
            lblPicker.Loaded       += LblPicker_Loaded;
            lblTrf.Loaded          += LblTrf_Loaded;
            lblDeptStore.Loaded    += LblDeptStore_Loaded;
            lblConfirmTitle.Loaded += LblConfirmTitle_Loaded;
            lblInput.Loaded        += LblInput_Loaded;

            lvSKU.SelectionChanged += LvSKU_SelectionChanged;

            Appearing    += CheckingPage_Appearing;
            Disappearing += CheckingPage_Disappearing;

            _InitializeIdleMonitoring();
            _InitializeLoadingAnimations();
            _AttachActivityTracking();
        }

        // Idle Monitoring Initialization

        private void _InitializeIdleMonitoring()
        {
            _lastActivityTime = DateTime.Now;

            _idleCheckTimer          = Dispatcher.CreateTimer();
            _idleCheckTimer.Interval = TimeSpan.FromSeconds(1);
            _idleCheckTimer.Tick    += IdleCheckTimer_Tick;
            // Timer starts when checking begins
        }

        private void _AttachActivityTracking()
        {
            txtBarcode.TextChanged += OnUserActivity;
            txtBarcode.Focused     += OnUserActivity;
            txtCase.TextChanged    += OnUserActivity;
            txtCase.Focused        += OnUserActivity;
            txtEach.TextChanged    += OnUserActivity;
            txtEach.Focused        += OnUserActivity;
            txtStocker.TextChanged += OnUserActivity;
            txtStocker.Focused     += OnUserActivity;

            btnAccept.Clicked      += OnUserActivity;
            btnFinished.Clicked    += OnUserActivity;
            btnViewItems.Clicked   += OnUserActivity;
            btnConso.Clicked       += OnUserActivity;
            btnCloseItems.Clicked  += OnUserActivity;
            btnCloseItems2.Clicked += OnUserActivity;
            btnConfirm.Clicked     += OnUserActivity;
            btnCancel.Clicked      += OnUserActivity;

            lvSKU.SelectionChanged += OnUserActivity;
        }

        // Activity Tracking
        private void OnUserActivity(object sender, EventArgs e) => _ResetIdleTimer();

        private void _ResetIdleTimer()
        {
            _lastActivityTime  = DateTime.Now;
            _idleAlertShown    = false;
            _warningAlertShown = false;
            _StopAlarm();
        }

        private void _StartIdleMonitoring()
        {
            _isCheckingActive  = true;
            _lastActivityTime  = DateTime.Now;
            _idleAlertShown    = false;
            _warningAlertShown = false;

            if (!_idleCheckTimer.IsRunning)
                _idleCheckTimer.Start();
        }

        private void _StopIdleMonitoring()
        {
            _isCheckingActive = false;
            _idleCheckTimer?.Stop();
            _StopAlarm();
        }

        // Idle Check Timer

        private async void IdleCheckTimer_Tick(object sender, EventArgs e)
        {
            if (!_isCheckingActive) return;

            var idleTime = DateTime.Now - _lastActivityTime;

            if (idleTime >= _warningTimeout && !_warningAlertShown)
            {
                _warningAlertShown = true;
                await _ShowWarningAlert();
            }

            if (idleTime >= _idleTimeout && !_idleAlertShown)
            {
                _idleAlertShown = true;
                await _ShowIdleAlert();
                await _PlayAlarm();
            }
        }

        // Alert and Alarm
        private async Task _ShowWarningAlert()
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                _VibrateDevice(500);

                var result = await DisplayAlert(
                    "⚠️ Idle Warning",
                    "You have been idle for 50 seconds.\n\n" +
                    "Alarm will sound in 10 seconds if no activity detected.\n\n" +
                    "Tap 'Continue' to resume checking.",
                    "Continue", "Cancel");

                if (result) _ResetIdleTimer();
            });
        }

        private async Task _ShowIdleAlert()
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                _VibrateDevice(1000);

                await DisplayAlert(
                    "🚨 IDLE ALERT! 🚨",
                    "You have been idle for 1 minute!\n\n" +
                    "Checking is still in progress.\n\n" +
                    "Tap 'Resume' to continue checking.",
                    "Resume");

                _ResetIdleTimer();
                txtBarcode.Focus();
            });
        }

        private async Task _PlayAlarm()
        {
            try
            {
                var audioManager = Plugin.Maui.Audio.AudioManager.Current;
                var alarmStream  = await FileSystem.OpenAppPackageFileAsync("alarm_sound.mp3");

                if (alarmStream != null)
                {
                    _alarmPlayer        = audioManager.CreatePlayer(alarmStream);
                    _alarmPlayer.Loop   = true;
                    _alarmPlayer.Volume = 1.0;
                    _alarmPlayer.Play();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Alarm audio failed: {ex.Message}");
                _PlaySystemBeep();
            }
        }

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
        private async void _PlaySystemBeep()
        {
            for (int i = 0; i < 5; i++)
            {
                _VibrateDevice(200);
                await Task.Delay(300);
            }
        }

        private void _VibrateDevice(int durationMs)
        {
            try
            {
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(durationMs));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Vibration failed: {ex.Message}");
            }
        }

        // Loading Animations
        private void _InitializeLoadingAnimations()
        {
            _truckAnimationTimer          = Dispatcher.CreateTimer();
            _truckAnimationTimer.Interval = TimeSpan.FromMilliseconds(30);
            _truckAnimationTimer.Tick    += TruckAnimationTimer_Tick;
            _dotsAnimationTimer          = Dispatcher.CreateTimer();
            _dotsAnimationTimer.Interval = TimeSpan.FromMilliseconds(400);
            _dotsAnimationTimer.Tick    += DotsAnimationTimer_Tick;
        }

        private void TruckAnimationTimer_Tick(object sender, EventArgs e)
        {
            _truckPosition += 3;
            if (_truckPosition > 240) _truckPosition = -80;
            truckIcon.Margin = new Thickness(_truckPosition, 0, 0, 0);
        }

        private void DotsAnimationTimer_Tick(object sender, EventArgs e)
        {
            _dotAnimationStep = (_dotAnimationStep + 1) % 4;
            switch (_dotAnimationStep)
            {
                case 0: dot1.Opacity = 1;   dot2.Opacity = 0.3; dot3.Opacity = 0.3; break;
                case 1: dot1.Opacity = 0.3; dot2.Opacity = 1;   dot3.Opacity = 0.3; break;
                case 2: dot1.Opacity = 0.3; dot2.Opacity = 0.3; dot3.Opacity = 1;   break;
                case 3: dot1.Opacity = 0.3; dot2.Opacity = 0.3; dot3.Opacity = 0.3; break;
            }
        }

        private void _ShowLoading(string message = "Loading...")
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                loadingText.Text = message;
                pbReq.IsVisible  = true;
                _truckPosition   = -80;
                truckIcon.Margin = new Thickness(_truckPosition, 0, 0, 0);
                _truckAnimationTimer.Start();
                _dotsAnimationTimer.Start();
            });
        }

        private void _HideLoading()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                pbReq.IsVisible = false;
                _truckAnimationTimer?.Stop();
                _dotsAnimationTimer?.Stop();
            });
        }

        // Page Lifecycle

        private async void CheckingPage_Appearing(object sender, EventArgs e)
        {
            txtBarcode.Text = string.Empty;
            txtEachVal      = string.Empty;
            pnlItems.IsVisible  = false;
            pnlItems2.IsVisible = false;
            isBarcode             = true;
            btnFinished.IsVisible = false;
            await _GetSetPickNoAsync();
            scanCount = 0;
            await Task.Delay(300);
            MainThread.BeginInvokeOnMainThread(() => txtBarcode.Focus());
        }

        private void CheckingPage_Disappearing(object sender, EventArgs e)
        {
            tmrRequest?.Stop();
            _StopIdleMonitoring();
            _HideLoading();
            _hasEverRequested = false;
        }

        // Focus Handlers
        private void TxtEntry_Unfocused(object sender, FocusEventArgs e)
        {
            if (sender is Entry entry)
                entry.BackgroundColor = Colors.WhiteSmoke;
        }

        private void TxtCaseEach_Unfocused(object sender, FocusEventArgs e)
        {
            if (sender is Entry entry)
            {
                entry.SelectionLength = 0;
                entry.CursorPosition  = 0;
            }
        }

        private void TxtBarcodeQtyFocus_Focused(object sender, FocusEventArgs e)
        {
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
                    entry.CursorPosition  = 0;
                    entry.SelectionLength = entry.Text?.Length ?? 0;
                }
            }
        }
        private void TxtOther_Focused(object sender, FocusEventArgs e) => _focusedEntry?.Focus();

        // Entry Handlers
        private async void Entry_Completed(object sender, EventArgs e)
        {
            if (_isShowingAlert) return;
            _isShowingAlert = true;

            try
            {
                _ResetIdleTimer();

                var entry = sender as Entry;
                if (entry == null) return;

                if (entry.Text != null && entry.Text.Any(c => AppGlobal._isAllowedNum(c) == '\0'))
                {
                    entry.Text = "";
                    return;
                }

                pbScanned.IsVisible = false;
                txtDesc.Text        = string.Empty;

                if (entry == txtBarcode)
                {
                    txtBarcode.Text = double.TryParse(txtBarcode.Text.Trim(), out double val)
                        ? val.ToString()
                        : txtBarcode.Text;

                    var result = await _isUPCFound(txtBarcode.Text.Trim());

                    if (result == false)
                    {
                        await DisplayAlert("Mismatch!", "Wrong scanned item!", "OK");
                        _ClearScan();
                    }
                    else if (result == null)
                    {
                        _ClearScan();
                    }
                }
                else if (entry == txtCase)
                {
                    txtCase.SelectionLength = 0;
                    txtEach.Focus();
                    txtEach.CursorPosition  = 0;
                    txtEach.SelectionLength = txtEach.Text?.Length ?? 0;
                }
                else if (entry == txtEach)
                {
                    BtnAccept_Clicked(null, null);
                }
            }
            finally
            {
                _isShowingAlert = false;
            }
        }

        private void Entry_TextChanged(object sender, TextChangedEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;

            if (e.NewTextValue != null && e.NewTextValue.Any(c => AppGlobal._isAllowedNum(c) == '\0'))
                entry.Text = e.OldTextValue;
        }

        private void TxtStocker_TextChanged(object sender, TextChangedEventArgs e)
        {
            var txt = txtStocker.Text;
            if (!string.IsNullOrEmpty(txt))
            {
                var filtered = new string(txt.Where(c => AppGlobal._isAllowedNum(c) != '\0').ToArray());
                if (txt != filtered)
                {
                    txtStocker.Text           = filtered;
                    txtStocker.CursorPosition = txtStocker.Text.Length;
                }
            }
        }

        private void TxtStocker_Completed(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtStocker.Text))
                _ = ConfirmStockerAsync();
        }

        // Text Change Handlers
        private void TxtDeptStore_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtDesc_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtSKU_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtBum_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtDone_TextChanged(object sender, TextChangedEventArgs e) { }

        // Button Click Handlers
        private async void BtnAccept_Clicked(object sender, EventArgs e)
        {
            if (_isAccepting) return;
            _isAccepting = true;

            try
            {
                _ResetIdleTimer();

                if (string.IsNullOrWhiteSpace(txtSKU.Text)) return;

                if (!string.IsNullOrWhiteSpace(txtEach.Text) &&
                    !string.IsNullOrWhiteSpace(txtCase.Text) &&
                    double.TryParse(txtEach.Text, out double eachVal) && eachVal >= 0 &&
                    double.TryParse(txtCase.Text, out double caseVal) && caseVal >= 0)
                {
                    bool answer = await DisplayAlert("Accept?", "Accept quantity?", "Yes", "No");
                    if (answer) await _AcceptItemAsync();
                }
            }
            finally
            {
                _isAccepting = false;
            }
        }

        // Finishes the current Transfer/SKU, commits to DB, then automatically requests again
        private async void BtnFinished_Clicked(object sender, EventArgs e)
        {
            if (_isFinishing) return;
            _isFinishing = true;

            try
            {
                _StopIdleMonitoring();

                bool answer = await DisplayAlert("Done Checking?", "Done Checking? Please Verify.", "Yes", "No");
                if (!answer) { _StartIdleMonitoring(); return; }

                answer = await DisplayAlert("Finished?", "Finish Checking?", "Yes", "No");
                if (!answer) { _StartIdleMonitoring(); return; }

                using var conn = await AppGlobal._SQL_Connect();
                if (conn == null)
                {
                    await DisplayAlert("No Connection!", "Cannot connect to server!", "OK");
                    _StartIdleMonitoring();
                    return;
                }

                try
                {
                    using var sqlCmd = conn.CreateCommand();
                    foreach (var skuItem in SKUList)
                    {
                        if (string.IsNullOrEmpty(skuItem.ChkQty) ||
                            (double.TryParse(skuItem.ChkQty, out double chkQtyVal) && chkQtyVal == 0))
                        {
                            sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                                                 "isUpdate=1, checkTime='00:00:00', isCSorted=1, CheckBy=@UserID " +
                                                 "WHERE SKU=@SKU AND ID_SumHdr=@ID_SumHdr";
                            sqlCmd.Parameters.Clear();
                            sqlCmd.Parameters.AddWithValue("@UserID",    AppGlobal.ID_User);
                            sqlCmd.Parameters.AddWithValue("@SKU",       skuItem.SKU);
                            sqlCmd.Parameters.AddWithValue("@ID_SumHdr", AppGlobal.ID_SumHdr);
                            await sqlCmd.ExecuteNonQueryAsync();
                        }
                        if (!string.IsNullOrEmpty(skuItem.Slot) && skuItem.Slot.Split(',').Length > 1)
                        {
                            sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                                                 "isCSorted=1, CheckBy=@UserID " +
                                                 "WHERE SKU=@SKU AND ID_SumHdr=@ID_SumHdr";
                            sqlCmd.Parameters.Clear();
                            sqlCmd.Parameters.AddWithValue("@UserID",    AppGlobal.ID_User);
                            sqlCmd.Parameters.AddWithValue("@SKU",       skuItem.SKU);
                            sqlCmd.Parameters.AddWithValue("@ID_SumHdr", AppGlobal.ID_SumHdr);
                            await sqlCmd.ExecuteNonQueryAsync();
                        }
                    }
                    sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickQty SET isChecked=1 WHERE id_sumhdr=@ID_SumHdr";
                    sqlCmd.Parameters.Clear();
                    sqlCmd.Parameters.AddWithValue("@ID_SumHdr", AppGlobal.ID_SumHdr);
                    await sqlCmd.ExecuteNonQueryAsync();

                    string updateChkStart = scanCount < 1 ? "chkStart=@chkStart," : "";
                    sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickHdr SET " +
                                         $"{updateChkStart}isUpdate=1, chkEnd=@chkEnd, chkDateDone=@chkDateDone " +
                                         "WHERE ID=@ID_SumHdr";
                    sqlCmd.Parameters.Clear();
                    if (scanCount < 1)
                        sqlCmd.Parameters.AddWithValue("@chkStart", await AppGlobal._GetDateTime());
                    sqlCmd.Parameters.AddWithValue("@chkEnd",      await AppGlobal._GetDateTime());
                    sqlCmd.Parameters.AddWithValue("@chkDateDone", await AppGlobal._GetDateTime(true));
                    sqlCmd.Parameters.AddWithValue("@ID_SumHdr",   AppGlobal.ID_SumHdr);
                    await sqlCmd.ExecuteNonQueryAsync();

                    sqlCmd.CommandText = "UPDATE tblUsers SET ID_SumHdr=0 WHERE ID=@UserID";
                    sqlCmd.Parameters.Clear();
                    sqlCmd.Parameters.AddWithValue("@UserID", AppGlobal.ID_User);
                    await sqlCmd.ExecuteNonQueryAsync();

                    _ResetSessionState();

                    await _GetSetPickNoAsync(autoRequest: true);
                }
                catch (SqlException ex)
                {
                    await DisplayAlert("Error!", ex.Message, "OK");
                    _StartIdleMonitoring();
                }
            }
            finally
            {
                _isFinishing = false;
            }
        }
        private async void BtnViewItems_Clicked(object sender, EventArgs e)
        {
            if (_isViewingItems) return;
            _isViewingItems = true;

            try
            {
                viewItemsLoadingOverlay.IsVisible = true;
                btnViewItems.IsEnabled            = false;
                _ResetIdleTimer();

                await Task.Delay(100);

                System.Diagnostics.Debug.WriteLine($"📊 SKUList.Count = {SKUList.Count}");

                if (SKUList.Count == 0)
                {
                    viewItemsLoadingOverlay.IsVisible = false;
                    btnViewItems.IsEnabled            = true;
                    await DisplayAlert("SKU List Empty!", "SKU List is empty. No items to display.", "OK");
                    return;
                }

                pnlItems.IsVisible = true;
                lblCnt.Text        = $"Count: {SKUList.Count}";

                if (!string.IsNullOrWhiteSpace(txtSKU.Text) && SKUList.Count > 0)
                {
                    int index = _getIndexLV();
                    System.Diagnostics.Debug.WriteLine($"📍 Current index = {index}");

                    if (index >= 0 && index < SKUList.Count)
                    {
                        await Task.Delay(50);
                        lvSKU.ScrollTo(SKUList[index], position: ScrollToPosition.MakeVisible, animate: true);
                        lvSKU.SelectedItem = SKUList[index];
                    }
                }
            }
            finally
            {
                viewItemsLoadingOverlay.IsVisible = false;
                btnViewItems.IsEnabled            = true;
                _isViewingItems                   = false;
            }
        }
        private void BtnConso_Clicked(object sender, EventArgs e)
        {
            pnlItems2.IsVisible = true;
            lblCnt2.Text        = $"Count: {SKUList2.Count}";
        }

        private void BtnCloseItems_Clicked(object sender, EventArgs e)
        {
            pnlItems.IsVisible = false;
            txtBarcode.Focus();
        }
        private void BtnCloseItems2_Clicked(object sender, EventArgs e)
        {
            pnlItems2.IsVisible = false;
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
            pnlInput.IsVisible    = true;
            pnlConfirm.IsVisible  = false;
            txtBarcode.Focus();
        }

        // Collection View Handlers
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
                    }
                }
                lvSKU.SelectedItem = null;
            }
        }

        // Loaded Event Handlers
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
        private void LblConfirmTitle_Loaded(object sender, EventArgs e) { }
        private void LblInput_Loaded(object sender, EventArgs e) { }

        //Modal Dialog
        private TaskCompletionSource<bool> _modalDialogResult;

        private void ModalYesButton_Clicked(object sender, EventArgs e)
        {
            _modalDialogResult?.TrySetResult(true);
            customModalOverlay.IsVisible = false;
        }

        private void ModalNoButton_Clicked(object sender, EventArgs e)
        {
            _modalDialogResult?.TrySetResult(false);
            customModalOverlay.IsVisible = false;
        }

        private async Task<bool> ShowCustomModalDialog(
            string title, string message,
            string yesText = "Yes", string noText = "No")
        {
            _modalDialogResult = new TaskCompletionSource<bool>();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                modalTitle.Text   = title;
                modalMessage.Text = message;
                modalYesButton.Text = yesText;
                modalNoButton.Text  = noText;
                customModalOverlay.IsVisible = true;
            });

            return await _modalDialogResult.Task;
        }

        // Business Logic Methods
        // Resets all in-memory session so the page is ready to receive brand-new Transfer/SKU
        private void _ResetSessionState()
        {
            AppGlobal.ID_SumHdr = 0;
            sSKU      = -1;
            isStarted = false;
            scanCount = 0;
            ID_Stocker = 0;
            iRetry     = 0;

            _requestAlreadyShown = false;
            _requestFailedShown  = false;

            _idleAlertShown    = false;
            _warningAlertShown = false;

            SKUList.Clear();
            SKUList2.Clear();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                txtDone.Text      = "";
                txtBarcode.Text   = "";
                txtSKU.Text       = "";
                txtCase.Text      = "0";
                txtEach.Text      = "0";
                txtDesc.Text      = "";
                txtBum.Text       = "";
                txtDeptStore.Text = "";
                lblLoc.Text       = "";
                lblTrf.Text       = "";
                lblPicker.Text    = "";
                btnFinished.IsVisible = false;
                pnlItems.IsVisible   = false;
                pnlItems2.IsVisible  = false;
                pnlConfirm.IsVisible = false;
            });
        }
        private async Task _GetSetPickNoAsync(bool autoRequest = false)
        {
            btnFinished.IsVisible = false;
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
                using (var sqlCmd = new SqlCommand(
                    "SELECT a.ID_SumHdr, a.PickRef FROM tblUsers a WHERE a.ID=@UserID", conn))
                {
                    sqlCmd.Parameters.AddWithValue("@UserID", AppGlobal.ID_User);

                    using var reader = await sqlCmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        AppGlobal.ID_SumHdr = reader["ID_SumHdr"] != DBNull.Value
                            ? Convert.ToInt32(reader["ID_SumHdr"]) : 0;

                        if (reader["PickRef"] != DBNull.Value && Convert.ToInt32(reader["PickRef"]) != 0)
                            sUserPNo = reader["PickRef"].ToString().Trim();
                    }
                }
                if (AppGlobal.ID_SumHdr != 0 && sUserPNo == AppGlobal.pPickNo)
                {
                    System.Diagnostics.Debug.WriteLine("✅ Found existing checking session – loading data...");
                    _ShowLoading("Loading existing checking session...");
                    await Task.Yield();
                    txtSKU.Text = string.Empty;
                    await _AddSKUtoListAsync();
                    return;
                }

                System.Diagnostics.Debug.WriteLine(
                    $"❌ No existing session – will request. " +
                    $"autoRequest={autoRequest}, _hasEverRequested={_hasEverRequested}");

                if (_isRequesting) return;
                _isRequesting = true;

                try
                {
                    if (autoRequest && _hasEverRequested)
                    {
                        System.Diagnostics.Debug.WriteLine("🔄 Auto-requesting next Transfer/SKU (Checking)...");

                        _ShowLoading("Loading next checking session...");
                        await Task.Yield();

                        using var autoCmd = new SqlCommand(
                            "UPDATE tblUsers SET isRequest=1, isSummary=@Summary, PickRef=@PickNo WHERE ID=@UserID",
                            conn);
                        autoCmd.Parameters.AddWithValue("@Summary", 2);
                        autoCmd.Parameters.AddWithValue("@PickNo",  AppGlobal.pPickNo);
                        autoCmd.Parameters.AddWithValue("@UserID",  AppGlobal.ID_User);
                        await autoCmd.ExecuteNonQueryAsync();

                        tmrRequest.Start();
                        return;
                    }
                    bool answer = await ShowCustomModalDialog(
                        "Requesting...",
                        "Request from server?",
                        "Yes", "No");

                    if (answer)
                    {
                        _hasEverRequested = true;

                        _ShowLoading("Requesting...");
                        await Task.Yield();

                        using var updateCmd = new SqlCommand(
                            "UPDATE tblUsers SET isRequest=1, isSummary=@Summary, PickRef=@PickNo WHERE ID=@UserID",
                            conn);
                        updateCmd.Parameters.AddWithValue("@Summary", 2);
                        updateCmd.Parameters.AddWithValue("@PickNo",  AppGlobal.pPickNo);
                        updateCmd.Parameters.AddWithValue("@UserID",  AppGlobal.ID_User);
                        await updateCmd.ExecuteNonQueryAsync();

                        tmrRequest.Start();
                    }
                    else
                    {
                        await Navigation.PopModalAsync();
                    }
                }
                finally
                {
                    _isRequesting = false;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // Load SKU list from database
        private async Task _AddSKUtoListAsync()
        {
            MainThread.BeginInvokeOnMainThread(() => loadingText.Text = "Loading checking data...");

            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                _HideLoading();
                await DisplayAlert("Error", "Cannot connect to server!", "OK");
                return;
            }

            try
            {
                using (var sqlCmd = new SqlCommand(
                    $"SELECT * FROM tbl{AppGlobal.pPickNo}PickHdr WHERE ID=@SumHdr", conn))
                {
                    sqlCmd.Parameters.AddWithValue("@SumHdr", AppGlobal.ID_SumHdr);

                    using var reader = await sqlCmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        lblDeptStore.Text = isSummary == 1 ? "Department:" : "Store No:";
                        txtDeptStore.Text = isSummary == 1
                            ? await AppGlobal._GetDeptName(Convert.ToInt32(reader["iDept"]))
                            : await AppGlobal._GetStoreNo();

                        lblPicker.Text = " Picker : " + await AppGlobal._GetUserName(reader["User_ID"].ToString());

                        if (reader["TimeEnd"].ToString().Trim() == "0" ||
                            reader["DateDone"].ToString().Trim() == "")
                        {
                            _HideLoading();
                            await DisplayAlert("System Says", "Not yet Picked!", "OK");
                            await Navigation.PopModalAsync();
                            return;
                        }

                        isStarted = reader["chkStart"].ToString().Trim() != "0";
                    }
                }

                var dsData  = new DataSet();
                string cmdData = $@"
                    SELECT a.*, b.ToLoc, b.TranNo
                    FROM tbl{AppGlobal.pPickNo}PickQty a
                    LEFT JOIN (
                        SELECT DISTINCT id_sumhdr, ToLoc, TranNo
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
                    var item = new SKUItem
                    {
                        ID        = row["ID"].ToString().Trim(),
                        BUM       = row["BUM"].ToString().Trim(),
                        Slot      = row["Slot"].ToString().Trim(),
                        SKU       = row["SKU"].ToString().Trim(),
                        Descr     = row["Descr"].ToString().Trim(),
                        Qty       = row["Qty"].ToString().Trim(),
                        isPicked  = row["isPicked"].ToString().Trim(),
                        PickQty   = row["isPicked"].ToString().Trim() == "0" ? "" : row["PickQty"].ToString(),
                        UPC       = row["UPC"].ToString().Trim(),
                        isChecked = row["isChecked"].ToString().Trim(),
                        ChkQty    = row["isChecked"].ToString().Trim() == "0" ? "" : row["chkQty"].ToString()
                    };
                    SKUList.Add(item);

                    _loadlvSKU2(
                        row["ID"].ToString(),   row["BUM"].ToString(),
                        row["Slot"].ToString(), row["SKU"].ToString(),
                        row["Descr"].ToString(), row["Qty"].ToString(),
                        row["isPicked"].ToString(), row["PickQty"].ToString(),
                        row["UPC"].ToString(),  row["isChecked"].ToString(),
                        row["chkQty"].ToString());
                }

                if (dsData.Tables[0].Rows.Count > 0)
                {
                    var firstRow   = dsData.Tables[0].Rows[0];
                    lblLoc.Text    = "Location: "   + firstRow["ToLoc"].ToString();
                    lblTrf.Text    = "Transfer # : " + firstRow["TranNo"].ToString();
                }

                dsData.Tables.Clear();
                sSKU = -1;
                _CountPicked();
                _StartIdleMonitoring();
            }
            catch (Exception ex)
            {
                _HideLoading();
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private void _loadlvSKU2(string id, string bum, string slot, string sku, string descr,
                                  string qty, string isPck, string pckqty, string upc,
                                  string isChecked, string chkQty)
        {
            var existingItem = SKUList2.FirstOrDefault(x => x.SKU == sku);

            if (existingItem != null)
            {
                if (double.TryParse(existingItem.ChkQty, out double existingChk) &&
                    double.TryParse(chkQty, out double addChk))
                    existingItem.ChkQty = (existingChk + addChk).ToString();
                return;
            }

            SKUList2.Add(new SKUItem
            {
                ID        = id,
                BUM       = bum,
                Slot      = slot,
                SKU       = sku,
                Descr     = descr,
                Qty       = qty,
                isPicked  = isPck,
                PickQty   = isPck.Trim() == "0" ? "" : pckqty,
                UPC       = upc.Trim(),
                isChecked = isChecked,
                ChkQty    = isChecked.Trim() == "0" ? "" : chkQty
            });

            lvCnt++;
        }
        private void _CountPicked()
        {
            int iPicked = SKUList.Count(item => !string.IsNullOrWhiteSpace(item.ChkQty));
            txtDone.Text = $"{iPicked}/{SKUList.Count}";

            btnFinished.IsVisible = iPicked == SKUList.Count && SKUList.Count > 0;

            _HideLoading();

            MainThread.BeginInvokeOnMainThread(() => txtBarcode.Focus());
        }

        private async void TmrRequest_Tick(object sender, EventArgs e)
        {
            try
            {
                using var conn = await AppGlobal._SQL_Connect();
                if (conn == null) return;

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

                        _HideLoading();

                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            btnAccept.IsEnabled      = false;
                            btnFinished.IsEnabled    = false;
                            btnViewItems.IsEnabled   = false;
                            btnConso.IsEnabled       = false;
                            btnCloseItems.IsEnabled  = false;
                            btnCloseItems2.IsEnabled = false;
                            btnConfirm.IsEnabled     = false;
                            btnCancel.IsEnabled      = false;
                            txtBarcode.IsEnabled     = false;
                            txtCase.IsEnabled        = false;
                            txtEach.IsEnabled        = false;
                            txtStocker.IsEnabled     = false;
                        });

                        await DisplayAlert("Unable to Request!", "No Picking # available!", "OK");

                        await MainThread.InvokeOnMainThreadAsync(async () =>
                            await Navigation.PopModalAsync());
                    }
                }
                else
                {
                    iRetry++;
                }
            }
            catch (Exception ex)
            {
                _HideLoading();
                await DisplayAlert("Error!", ex.Message, "OK");
            }
        }

        private void _ClearScan(bool bWithBarcode = true)
        {
            if (bWithBarcode) txtBarcode.Text = string.Empty;

            txtSKU.Text  = string.Empty;
            txtEach.Text = "0";
            txtCase.Text = "0";

            txtCase.CursorPosition = 0;
            txtCase.SelectionLength = 0;
            txtEach.CursorPosition  = 0;
            txtEach.SelectionLength = 0;

            txtBarcode.Focus();
            txtBarcode.CursorPosition  = 0;
            txtBarcode.SelectionLength = txtBarcode.Text?.Length ?? 0;
        }

        private async Task _AcceptItemAsync()
        {
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

                double dBum  = ParseEntry(txtBum);
                double dCase = ParseEntry(txtCase);
                double dEach = string.IsNullOrWhiteSpace(txtEach.Text) ? 0 : ParseEntry(txtEach);
                double dQty  = (dBum * dCase) + dEach;
                double totQty = 0.0;

                if (sSKU < 0 || sSKU >= SKUList.Count) return;
                var lvI = SKUList[sSKU];

                if (!isStarted)
                {
                    isStarted = true;
                    sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickHdr SET " +
                                         "isUpdate=1, chkStart=@chkStart WHERE ID=@ID_SumHdr";
                    sqlCmd.Parameters.AddWithValue("@chkStart",  await AppGlobal._GetDateTime());
                    sqlCmd.Parameters.AddWithValue("@ID_SumHdr", AppGlobal.ID_SumHdr);
                }
                else
                {
                    sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickHdr SET " +
                                         "isUpdate=1 WHERE ID=@ID_SumHdr";
                    sqlCmd.Parameters.AddWithValue("@ID_SumHdr", AppGlobal.ID_SumHdr);
                }
                await sqlCmd.ExecuteNonQueryAsync();

                string sUPC = "";
                if (!pbScanned.IsVisible && !string.IsNullOrWhiteSpace(txtBarcode.Text))
                    sUPC = "UPC=@UPC,";
                if (dEach == 0 && dCase == 0)
                    sUPC = "";

                if (isSummary == 1)
                {
                    sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                        $"{sUPC}PickBy=@PickBy, ConfBy=@ConfBy, PickTime=@PickTime " +
                        "WHERE SKU=@SKU AND ID_SumHdr=@SumHdr";
                    sqlCmd.Parameters.Clear();
                    if (!string.IsNullOrEmpty(sUPC)) sqlCmd.Parameters.AddWithValue("@UPC", txtBarcode.Text);
                    sqlCmd.Parameters.AddWithValue("@PickBy",   AppGlobal.ID_User);
                    sqlCmd.Parameters.AddWithValue("@ConfBy",   ID_Stocker);
                    sqlCmd.Parameters.AddWithValue("@PickTime", await AppGlobal._GetDateTime());
                    sqlCmd.Parameters.AddWithValue("@SKU",      lvI.SKU);
                    sqlCmd.Parameters.AddWithValue("@SumHdr",   AppGlobal.ID_SumHdr);
                    await sqlCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    var dsData = new DataSet();

                    using (var readCmd = conn.CreateCommand())
                    {
                        readCmd.Transaction = txn;
                        readCmd.CommandText = $"SELECT ID, Qty FROM tbl{AppGlobal.pPickNo}PickDtl " +
                                              "WHERE SKU=@SKU AND ID_SumHdr=@SumHdr ORDER BY slot, sku";
                        readCmd.Parameters.AddWithValue("@SKU",    lvI.SKU);
                        readCmd.Parameters.AddWithValue("@SumHdr", AppGlobal.ID_SumHdr);
                        using var adapter = new SqlDataAdapter(readCmd);
                        adapter.Fill(dsData, "DATA");
                    }

                    sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                        $"{sUPC}SortBy=@SortBy, CheckBy=@CheckBy, ConfBy=@ConfBy, CheckTime=@CheckTime " +
                        "WHERE SKU=@SKU AND ID_SumHdr=@SumHdr";
                    sqlCmd.Parameters.Clear();
                    if (!string.IsNullOrEmpty(sUPC)) sqlCmd.Parameters.AddWithValue("@UPC", txtBarcode.Text);
                    sqlCmd.Parameters.AddWithValue("@SortBy",    AppGlobal.ID_User);
                    sqlCmd.Parameters.AddWithValue("@CheckBy",   AppGlobal.ID_User);
                    sqlCmd.Parameters.AddWithValue("@ConfBy",    ID_Stocker);
                    sqlCmd.Parameters.AddWithValue("@CheckTime", await AppGlobal._GetDateTime());
                    sqlCmd.Parameters.AddWithValue("@SKU",       lvI.SKU);
                    sqlCmd.Parameters.AddWithValue("@SumHdr",    AppGlobal.ID_SumHdr);
                    await sqlCmd.ExecuteNonQueryAsync();

                    if (dsData.Tables["DATA"]?.Rows.Count > 0)
                    {
                        int lCount = dsData.Tables["DATA"].Rows.Count - 1;

                        for (int iCount = 0; iCount <= lCount; iCount++)
                        {
                            var dRow     = dsData.Tables["DATA"].Rows[iCount];
                            double dNeed = Convert.ToDouble(dRow["Qty"]);

                            if (iCount == lCount)
                            {
                                sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                                    "isCSorted=1, cSortQty=@cSortQty, isUpdate=1 WHERE ID=@ID";
                                sqlCmd.Parameters.Clear();
                                sqlCmd.Parameters.AddWithValue("@cSortQty", dQty);
                                sqlCmd.Parameters.AddWithValue("@ID",       dRow["ID"]);
                                await sqlCmd.ExecuteNonQueryAsync();
                                totQty += dQty;
                            }
                            else if (dQty >= dNeed)
                            {
                                sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                                    "isCSorted=1, cSortQty=Qty, isUpdate=1 WHERE ID=@ID";
                                sqlCmd.Parameters.Clear();
                                sqlCmd.Parameters.AddWithValue("@ID", dRow["ID"]);
                                await sqlCmd.ExecuteNonQueryAsync();
                                dQty   -= dNeed;
                                totQty += dNeed;
                            }
                            else
                            {
                                sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickDtl SET " +
                                    "isCSorted=1, cSortQty=@cSortQty, isUpdate=1 WHERE ID=@ID";
                                sqlCmd.Parameters.Clear();
                                sqlCmd.Parameters.AddWithValue("@cSortQty", dQty);
                                sqlCmd.Parameters.AddWithValue("@ID",       dRow["ID"]);
                                await sqlCmd.ExecuteNonQueryAsync();
                                totQty += dQty;
                                dQty    = 0;
                                break;
                            }
                        }

                        lvI.ChkQty = totQty.ToString("N2");
                    }

                    dsData.Tables.Clear();
                }

                sqlCmd.CommandText = $"UPDATE tbl{AppGlobal.pPickNo}PickQty SET " +
                    "isChecked=1, chkQty=@chkQty WHERE ID=@ID";
                sqlCmd.Parameters.Clear();
                sqlCmd.Parameters.AddWithValue("@chkQty", totQty);
                sqlCmd.Parameters.AddWithValue("@ID",     lvI.ID);
                await sqlCmd.ExecuteNonQueryAsync();

                txn.Commit();
                scanCount++;
                pbScanned.IsVisible = true;

                _CountPicked();
                _ClearScan();

                MainThread.BeginInvokeOnMainThread(() => txtBarcode.Focus());
            }
            catch (Exception ex)
            {
                txn?.Rollback();
                await DisplayAlert("Transaction Error", $"Please Retry.\n{ex.Message}", "OK");
                MainThread.BeginInvokeOnMainThread(() => txtBarcode.Focus());
            }
        }

        private async Task ConfirmStockerAsync()
        {
            if (string.IsNullOrWhiteSpace(txtStocker.Text)) return;

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
                    ID_Stocker    = Convert.ToInt32(reader["ID"]);
                    txtStockerTag = reader["ID"].ToString().Trim();
                    await DisplayAlert("Stocker Name:", reader["FullName"].ToString().Trim(), "OK");
                    btnConfirm.Focus();
                }
                else
                {
                    txtStockerTag = "";
                    await DisplayAlert("Not Found!", "Stocker ID not found!", "OK");
                    txtStocker.Focus();
                    txtStocker.CursorPosition  = 0;
                    txtStocker.SelectionLength = txtStocker.Text?.Length ?? 0;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // Check if UPC is found in the SKU list
        private async Task<bool?> _isUPCFound(string upc)
        {
            foreach (var item in SKUList)
            {
                if (!string.IsNullOrWhiteSpace(item.UPC) && item.UPC.Contains("-" + upc.Trim() + ","))
                {
                    System.Diagnostics.Debug.WriteLine($"🔍 Found UPC match: SKU={item.SKU}, ChkQty={item.ChkQty}");

                    if (double.TryParse(item.ChkQty, out double chkQty) && chkQty > 0)
                    {
                        await DisplayAlert("Checked!", $"SKU: {item.SKU} Checked already", "OK");
                        return null;
                    }

                    if (double.TryParse(item.Qty, out double qty) &&
                        double.TryParse(item.ChkQty, out double chkQty2) &&
                        qty == chkQty2)
                        continue;

                    sSKU = SKUList.IndexOf(item);
                    _getDuplicateSKUIndex(item.SKU.Trim());

                    double bumVal = double.TryParse(item.BUM, out double b) ? b : 0;

                    double qtyVal = 0;
                    if (!string.IsNullOrWhiteSpace(item.PickQty) &&
                        double.TryParse(item.PickQty, out double pq) && pq > 0)
                    {
                        qtyVal = pq;
                    }
                    else if (double.TryParse(item.Qty, out double q))
                    {
                        qtyVal = q;
                    }

                    _loadDetails(item.SKU, bumVal, qtyVal, item.Descr);
                    return true;
                }
            }

            return false;
        }

        private void _getDuplicateSKUIndex(string sku)
        {
            skuArr = new List<int>();
            for (int i = 0; i < SKUList.Count; i++)
            {
                var item = SKUList[i];
                if (!string.IsNullOrWhiteSpace(item.SKU) && item.SKU.Trim() == sku)
                    skuArr.Add(i);
            }
        }

        private void _loadDetails(string sku, double cse, double qty, string skuDesc)
        {
            System.Diagnostics.Debug.WriteLine($"📝 _loadDetails: SKU={sku}, BUM={cse}, Qty={qty}");

            txtCase.IsEnabled = cse != 1;

            double caseValue = 0;
            double eachValue = 0;

            if (cse == 1 || qty < cse)
            {
                caseValue = 0;
                eachValue = qty;
            }
            else
            {
                caseValue = Math.Floor(qty / cse);
                eachValue = qty % cse;
            }

            txtCase.TextChanged -= Entry_TextChanged;
            txtEach.TextChanged -= Entry_TextChanged;

            try
            {
                txtCase.Text = ((int)caseValue).ToString();
                txtEach.Text = ((int)eachValue).ToString();
                txtBum.Text  = cse.ToString("N2");
                txtSKU.Text  = sku;
                txtDesc.Text = skuDesc;
                txtBarcode.SelectionLength = 0;
            }
            finally
            {
                txtCase.TextChanged += Entry_TextChanged;
                txtEach.TextChanged += Entry_TextChanged;
            }

            if (caseValue == 0)
            {
                txtEach.Focus();
                txtEach.CursorPosition  = 0;
                txtEach.SelectionLength = txtEach.Text?.Length ?? 0;
            }
            else
            {
                txtCase.Focus();
                txtCase.CursorPosition  = 0;
                txtCase.SelectionLength = txtCase.Text?.Length ?? 0;
            }
        }

        private int _getIndexLV()
        {
            for (int i = 0; i < SKUList.Count; i++)
            {
                if (SKUList[i].SKU?.Trim() == txtSKU.Text.Trim())
                    return i;
            }
            return -1;
        }

        private void _SetUser(Label lbl)
        {
            if (lbl != null) lbl.Text = $"User: {AppGlobal.sUserName}";
        }

        private double ParseEntry(Entry entry)
            => double.TryParse(entry.Text, out double val) ? val : 0;

        public void OnF1Pressed()
        {
            _ResetIdleTimer();

            if (txtStocker.IsFocused)
            {
                BtnCancel_Clicked(null, null);
                return;
            }

            if (Navigation.NavigationStack.Count > 0)
                MainThread.BeginInvokeOnMainThread(async () => await Navigation.PopAsync());
        }

        public void OnF2Pressed()
        {
            _ResetIdleTimer();

            if (txtBarcode != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    txtBarcode.Focus();
                    txtBarcode.CursorPosition  = 0;
                    txtBarcode.SelectionLength = txtBarcode.Text?.Length ?? 0;
                });
            }
        }
        public void OnEscapePressed()
        {
            _StopIdleMonitoring();

            if (txtStocker.IsFocused)
                BtnCancel_Clicked(null, null);
            else if (Navigation.NavigationStack.Count > 0)
                MainThread.BeginInvokeOnMainThread(async () => await Navigation.PopAsync());
        }

        // Data Class for SKU Items

        public class SKUItem : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            public string ID        { get; set; }
            public string BUM       { get; set; }
            public string Slot      { get; set; }
            public string SKU       { get; set; }
            public string Descr     { get; set; }
            public string Qty       { get; set; }
            public string isPicked  { get; set; }
            public string PickQty   { get; set; }
            public string UPC       { get; set; }
            public string isChecked { get; set; }

            private string _chkQty;
            public string ChkQty
            {
                get => _chkQty;
                set
                {
                    if (_chkQty != value)
                    {
                        _chkQty = value;
                        OnPropertyChanged();
                        OnPropertyChanged(nameof(ChkQtyDisplay));
                        OnPropertyChanged(nameof(ChkQtyColor));
                    }
                }
            }

            public string ChkQtyDisplay => !string.IsNullOrEmpty(ChkQty) ? ChkQty : "";
            public Color  ChkQtyColor   => !string.IsNullOrEmpty(ChkQty) ? Colors.Green : Colors.Black;
        }
    }
}