using Android.Content.Res;
using Android.Media;
using Android.OS;
using AndroidX.Annotations;
using Java.Security;
using Microsoft.Data.SqlClient;
using Microsoft.Maui.Animations;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using PDTPickingSystem.Helpers;
using Plugin.Maui.Audio;
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

        // Concurrency guards
        private bool _isRequesting = false;
        private bool _isAccepting = false;
        private bool _isFinishing = false;
        private bool _isConfirming = false;

        // ── Auto-assignment: true after the picker confirms the very first request.
        //    All subsequent Transfer/SKU requests are issued silently (no modal).
        private bool _hasEverRequested = false;

        // ================== IDLE MONITORING FIELDS ==================

        /// <summary>Timer that checks for idle state every second</summary>
        private IDispatcherTimer _idleCheckTimer;

        /// <summary>Last time user interacted with the app</summary>
        private DateTime _lastActivityTime;

        /// <summary>Idle timeout duration (3 minutes)</summary>
        private readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(3);

        /// <summary>Warning timeout (2.5 minutes – 30 seconds before alarm)</summary>
        private readonly TimeSpan _warningTimeout = TimeSpan.FromMinutes(2.5);

        /// <summary>Flag to prevent multiple alert popups</summary>
        private bool _idleAlertShown = false;

        /// <summary>Flag to prevent multiple warning popups</summary>
        private bool _warningAlertShown = false;

        /// <summary>Audio player for alarm sound</summary>
        private IAudioPlayer _alarmPlayer;

        /// <summary>Flag to track if picking is currently active</summary>
        private bool _isPickingActive = false;

        // ================== 🚚 LOADING ANIMATION FIELDS ==================

        /// <summary>Timer for truck animation</summary>
        private IDispatcherTimer _truckAnimationTimer;

        /// <summary>Current truck position (0 to 240)</summary>
        private double _truckPosition = 0;

        /// <summary>Timer for loading dots animation</summary>
        private IDispatcherTimer _dotsAnimationTimer;

        /// <summary>Current dot animation step (0-3)</summary>
        private int _dotAnimationStep = 0;

        // ================== CONSTRUCTOR ==================

        public PickingPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            // Initialize from AppGlobal
            pPickNo   = AppGlobal.pPickNo;
            isSummary = AppGlobal.isSummary;
            ID_SumHdr = AppGlobal.ID_SumHdr;

            // Initialize focus tracker
            txtboxFocus = txtBarcode;

            // Bind collections
            lvBarcodes.ItemsSource = barcodeList;

            // Initialize timer
            tmrRequest          = Dispatcher.CreateTimer();
            tmrRequest.Interval = TimeSpan.FromSeconds(1);
            tmrRequest.Tick    += TmrRequest_Tick;

            // Initialize idle monitoring
            _InitializeIdleMonitoring();

            // Initialize loading animations
            _InitializeLoadingAnimations();

            // Page events
            Appearing    += PickingPage_Appearing;
            Disappearing += PickingPage_Disappearing;

            // ===== Focus events – Force focus back to barcode/qty entry =====
            txtpSKU.Focused   += TxtOther_GotFocus;
            txtpDescr.Focused += TxtOther_GotFocus;
            txtpSlot.Focused  += TxtOther_GotFocus;
            txtpEach.Focused  += TxtOther_GotFocus;
            txtpCase.Focused  += TxtOther_GotFocus;
            txtSKU.Focused    += TxtOther_GotFocus;
            txtDone.Focused   += TxtOther_GotFocus;
            txtDeptStore.Focused += TxtOther_GotFocus;

            // ===== Entry focus events (tracking + highlighting) =====
            txtBarcode.Focused   += Entry_GotFocus;
            txtBarcode.Unfocused += Entry_LostFocus;
            txtSKU.Focused       += Entry_GotFocus;
            txtSKU.Unfocused     += Entry_LostFocus;
            txtCase.Focused      += Entry_GotFocus;
            txtCase.Unfocused    += Entry_LostFocus;
            txtEach.Focused      += Entry_GotFocus;
            txtEach.Unfocused    += Entry_LostFocus;
            txtpCase.Focused     += Entry_GotFocus;
            txtpCase.Unfocused   += Entry_LostFocus;
            txtpEach.Focused     += Entry_GotFocus;
            txtpEach.Unfocused   += Entry_LostFocus;
            txtStocker.Focused   += Entry_GotFocus;
            txtStocker.Unfocused += Entry_LostFocus;
            txtDone.Focused      += Entry_GotFocus;
            txtDone.Unfocused    += Entry_LostFocus;

            // ===== TextChanged events =====
            txtBarcode.TextChanged  += TxtBarcode_TextChanged;
            txtDeptStore.TextChanged += TxtDeptStore_TextChanged;
            txtpSKU.TextChanged     += TxtpSKU_TextChanged;
            txtpCase.TextChanged    += TxtpCase_TextChanged;
            txtpEach.TextChanged    += TxtpEach_TextChanged;
            txtEach.TextChanged     += TxtEach_TextChanged;
            txtSKU.TextChanged      += TxtSKU_TextChanged;
            txtCase.TextChanged     += TxtCase_TextChanged;
            txtpSlot.TextChanged    += TxtpSlot_TextChanged;
            txtDone.TextChanged     += TxtDone_TextChanged;
            txtpDescr.TextChanged   += TxtpDescr_TextChanged;
            txtStocker.TextChanged  += TxtStocker_TextChanged;
            txtLine.TextChanged     += TxtLine_TextChanged;

            // ===== Loaded events =====
            lblCase.Loaded         += LblCase_Loaded;
            lblEach.Loaded         += LblEach_Loaded;
            lblEach2.Loaded        += LblEach2_Loaded;
            lblCase2.Loaded        += LblCase2_Loaded;
            lblBarcode.Loaded      += LblBarcode_Loaded;
            Gotolbl.Loaded         += Gotolbl_Loaded;
            lblLineNo.Loaded       += LblLineNo_Loaded;
            lblSKU.Loaded          += LblSKU_Loaded;
            lblDone.Loaded         += LblDone_Loaded;
            lblInput.Loaded        += LblInput_Loaded;
            lblConfirmTitle.Loaded += LblConfirmTitle_Loaded;
            lblUser.Loaded         += LblUser_Loaded;
            lblMultipleSlots.Loaded += LblMultipleSlots_Loaded;
            lblBarcodeTitle.Loaded  += LblBarcodeTitle_Loaded;

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
            pbScanned.Clicked    += PbScanned_Clicked;
            btnAccept.Clicked    += BtnAccept_Clicked;
            btnCloseSlot.Clicked += BtnCloseSlot_Clicked;
            btnConfirm.Clicked   += BtnConfirm_Clicked;
            btnCancel.Clicked    += BtnCancel_Clicked;
            btnFinished.Clicked  += BtnFinished_Clicked;
            btnClose.Clicked     += BtnClose_Clicked;

            // ===== Navigation buttons (unified handler) =====
            btnGoto.Clicked   += BtnNavigate_Clicked;
            btnFirst.Clicked  += BtnNavigate_Clicked;
            btnPrev.Clicked   += BtnNavigate_Clicked;
            btnNext.Clicked   += BtnNavigate_Clicked;
            btnLast.Clicked   += BtnNavigate_Clicked;
            btnUnpick.Clicked += BtnNavigate_Clicked;

            // ===== Entry Completed events =====
            txtBarcode.Completed += Entry_BarcodeAndQty_Completed;
            txtCase.Completed    += Entry_BarcodeAndQty_Completed;
            txtEach.Completed    += Entry_BarcodeAndQty_Completed;
            txtStocker.Completed += TxtStocker_Completed;
            txtLine.Completed    += TxtLine_Completed;

            // ===== Panel focus =====
            pnlBarcodes.Focused += PnlBarcodes_Focused;
            pnlGoto.Focused     += PnlGoto_Focused;

            // ===== CollectionView selection =====
            lvSlots.SelectionChanged += LvSlots_SelectionChanged;

            // Attach activity tracking to all interactive controls
            _AttachActivityTracking();
        }

        // ================== IDLE MONITORING INITIALIZATION ==================

        private void _InitializeIdleMonitoring()
        {
            _lastActivityTime = DateTime.Now;

            _idleCheckTimer          = Dispatcher.CreateTimer();
            _idleCheckTimer.Interval = TimeSpan.FromSeconds(1);
            _idleCheckTimer.Tick    += IdleCheckTimer_Tick;
            // Don't start timer yet – starts when picking begins
        }

        private void _AttachActivityTracking()
        {
            // Entries
            txtBarcode.TextChanged += OnUserActivity;
            txtBarcode.Focused     += OnUserActivity;
            txtCase.TextChanged    += OnUserActivity;
            txtCase.Focused        += OnUserActivity;
            txtEach.TextChanged    += OnUserActivity;
            txtEach.Focused        += OnUserActivity;
            txtStocker.TextChanged += OnUserActivity;
            txtStocker.Focused     += OnUserActivity;
            txtLine.TextChanged    += OnUserActivity;
            txtLine.Focused        += OnUserActivity;

            // Buttons
            btnAccept.Clicked    += OnUserActivity;
            btnFinished.Clicked  += OnUserActivity;
            btnConfirm.Clicked   += OnUserActivity;
            btnCloseGoto.Clicked += OnUserActivity;
            btnCloseSlot.Clicked += OnUserActivity;
            btnClose.Clicked     += OnUserActivity;

            // CollectionView
            lvSlots.SelectionChanged += OnUserActivity;
        }

        // ================== ACTIVITY TRACKING ==================

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
            _isPickingActive   = true;
            _lastActivityTime  = DateTime.Now;
            _idleAlertShown    = false;
            _warningAlertShown = false;

            if (!_idleCheckTimer.IsRunning)
                _idleCheckTimer.Start();
        }

        private void _StopIdleMonitoring()
        {
            _isPickingActive = false;
            _idleCheckTimer?.Stop();
            _StopAlarm();
        }

        // ================== IDLE CHECK TIMER ==================

        private async void IdleCheckTimer_Tick(object sender, EventArgs e)
        {
            if (!_isPickingActive) return;

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

        // ================== ALERT & ALARM FUNCTIONS ==================

        private async Task _ShowWarningAlert()
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                _VibrateDevice(500);

                var result = await DisplayAlert(
                    "⚠️ Idle Warning",
                    "You have been idle for 2.5 minutes.\n\n" +
                    "Alarm will sound in 30 seconds if no activity detected.\n\n" +
                    "Tap 'Continue' to resume picking.",
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
                    "You have been idle for 3 minutes!\n\n" +
                    "Picking is still in progress.\n\n" +
                    "Tap 'Resume' to continue picking.",
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

        // ================== 🚚 LOADING ANIMATION METHODS ==================

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

        // ================== PAGE LIFECYCLE ==================

        private async void PickingPage_Appearing(object sender, EventArgs e)
        {
            AppGlobal._SetUser(lblUser);

            isBarcode = true;

            pnlConfirm.IsVisible  = false;
            pnlBarcodes.IsVisible = false;
            pnlSlots.IsVisible    = false;
            pnlGoto.IsVisible     = false;
            btnFinished.IsVisible = false;

            await _GetSetPickNoAsync();

            await Task.Delay(300);
            MainThread.BeginInvokeOnMainThread(() => txtBarcode.Focus());
        }

        private void PickingPage_Disappearing(object sender, EventArgs e)
        {
            tmrRequest?.Stop();
            _StopIdleMonitoring();
            _HideLoading();

            // Reset the first-request flag so a fresh page visit starts clean
            _hasEverRequested = false;
        }

        // ================== FOCUS MANAGEMENT ==================

        private void Entry_GotFocus(object sender, FocusEventArgs e)
        {
            _ResetIdleTimer();

            txtboxFocus = sender as Entry;
            if (txtboxFocus == null) return;

            if (txtboxFocus == txtBarcode || txtboxFocus == txtStocker)
            {
                isBarcode = txtboxFocus == txtBarcode;
                txtboxFocus.BackgroundColor = Colors.PaleGreen;
            }
            else
            {
                txtboxFocus.CursorPosition  = 0;
                txtboxFocus.SelectionLength = txtboxFocus.Text?.Length ?? 0;
            }
        }

        private void Entry_LostFocus(object sender, FocusEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;

            entry.SelectionLength = 0;

            if (entry == txtBarcode || entry == txtStocker)
                entry.BackgroundColor = Colors.WhiteSmoke;
        }

        private void TxtOther_GotFocus(object sender, FocusEventArgs e) => txtboxFocus?.Focus();

        private void PnlBarcodes_Focused(object sender, FocusEventArgs e)
        {
            if (pnlBarcodes.Content is Microsoft.Maui.Controls.Layout layout &&
                layout.Children.FirstOrDefault() is Entry firstEntry)
                firstEntry.Focus();
        }

        private void PnlGoto_Focused(object sender, FocusEventArgs e) { }

        // ================== ENTRY VALIDATION & COMPLETION ==================

        private async void Entry_BarcodeAndQty_Completed(object sender, EventArgs e)
        {
            _ResetIdleTimer();

            if (sender is not Entry entry) return;

            if (entry.Text != null && entry.Text.Any(c => AppGlobal._isAllowedNum(c) == '\0'))
            {
                entry.Text = "";
                return;
            }

            if (entry == txtBarcode)
            {
                await GetSKUDescrAsync();
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

        private void TxtStocker_Completed(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtStocker.Text))
                _ = ConfirmStockerAsync();
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
                txtLine.CursorPosition  = 0;
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

        private void TxtpSKU_TextChanged(object sender, TextChangedEventArgs e) { }
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
                    txtStocker.Text            = filtered;
                    txtStocker.CursorPosition  = txtStocker.Text.Length;
                }
            }
        }

        private void TxtLine_TextChanged(object sender, TextChangedEventArgs e)
        {
            var txt = txtLine.Text;
            if (!string.IsNullOrEmpty(txt))
            {
                txtLine.Text           = new string(txt.Where(c => AppGlobal._isAllowedNum(c) != '\0').ToArray());
                txtLine.CursorPosition = txtLine.Text.Length;
            }
        }

        // ================== BUTTON CLICK HANDLERS ==================

        private async void BtnAccept_Clicked(object sender, EventArgs e)
        {
            if (_isAccepting) return;

            _isAccepting = true;
            _ResetIdleTimer();

            try
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
                            pnlNavigate.IsVisible       = true;
                            pnlInput.IsVisible          = true;
                            txtStocker.Text             = "";
                            txtStockerTag               = "";
                            txtStocker.IsReadOnly       = await AppGlobal._CheckOption_StockerAsync();
                            pnlConfirm.IsVisible        = true;
                            txtStocker.Focus();
                        }
                    }
                    else if (txtSKU.Text.Trim() != "" && txtSKU.Text == txtpSKU.Text)
                    {
                        double totalQty = (Convert.ToDouble(txtpCaseTag ?? 0) * caseQty) + eachQty;
                        string itemInfo = $"SKU: {txtpSKU.Text}\n" +
                                          $"Description: {txtpDescr.Text}\n" +
                                          $"Case: {caseQty:N2}\n" +
                                          $"Each: {eachQty:N2}\n" +
                                          $"Total Qty: {totalQty:N2}";

                        bool confirmAccept = await DisplayAlert(
                            "Accept Item?",
                            itemInfo + "\n\nDo you want to accept this item?",
                            "Yes", "No");

                        if (confirmAccept)
                            await _AcceptItemAsync();
                        else
                            txtBarcode.Focus();
                    }
                }
            }
            finally
            {
                _isAccepting = false;
            }
        }

        private async void BtnConfirm_Clicked(object sender, EventArgs e)
        {
            if (_isConfirming) return;

            if (string.IsNullOrEmpty(txtStockerTag))
            {
                await DisplayAlert("Error", "Please enter and validate Stocker ID first!", "OK");
                txtStocker.Focus();
                return;
            }

            if (int.TryParse(txtStockerTag, out int stockerId))
            {
                _isConfirming = true;
                try
                {
                    ID_Stocker = stockerId;
                    await DisplayAlert("OK", "Item Confirmed!", "OK");
                    BtnCancel_Clicked(null, null);
                    await _AcceptItemAsync();
                }
                finally
                {
                    _isConfirming = false;
                }
            }
        }

        private void BtnCancel_Clicked(object sender, EventArgs e)
        {
            pnlConfirm.IsVisible    = false;
            pnlNavigate.IsVisible   = true;
            pnlInput.IsVisible      = true;
            txtBarcode.Focus();
        }

        /// <summary>
        /// Finishes the current Transfer/SKU, commits to DB, then automatically
        /// requests the next one without prompting the user again.
        /// </summary>
        private async void BtnFinished_Clicked(object sender, EventArgs e)
        {
            if (_isFinishing) return;

            _isFinishing = true;

            try
            {
                // Pause idle monitoring during the finish flow
                _StopIdleMonitoring();

                bool confirm = await DisplayAlert("Finish?", "Finish Picking?", "Yes", "No");
                if (!confirm)
                {
                    _StartIdleMonitoring(); // resume if user cancels
                    return;
                }

                using var conn = await AppGlobal._SQL_Connect();
                if (conn == null)
                {
                    await DisplayAlert("Error", "Cannot connect to server!", "OK");
                    _StartIdleMonitoring();
                    return;
                }

                using var sqlCmd = conn.CreateCommand();

                // Mark any un-picked items as zero
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

                // Close the picking header
                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickHdr SET " +
                                     "isUpdate=1, TimeEnd=@TimeEnd, DateDone=@DateDone " +
                                     "WHERE ID=@ID";
                sqlCmd.Parameters.Clear();
                sqlCmd.Parameters.AddWithValue("@TimeEnd",  await AppGlobal._GetDateTime());
                sqlCmd.Parameters.AddWithValue("@DateDone", await AppGlobal._GetDateTime(true));
                sqlCmd.Parameters.AddWithValue("@ID", ID_SumHdr);
                await sqlCmd.ExecuteNonQueryAsync();

                // Clear the user's active session on the server
                sqlCmd.CommandText = "UPDATE tblUsers SET ID_SumHdr=0 WHERE ID=@UserID";
                sqlCmd.Parameters.Clear();
                sqlCmd.Parameters.AddWithValue("@UserID", AppGlobal.ID_User);
                await sqlCmd.ExecuteNonQueryAsync();

                // ── Reset all local session state ready for the next Transfer/SKU ──
                _ResetSessionState();

                // ── Auto-request the next Transfer/SKU (no modal shown) ──
                await _GetSetPickNoAsync(autoRequest: true);
            }
            finally
            {
                _isFinishing = false;
            }
        }

        private void BtnCloseGoto_Clicked(object sender, EventArgs e) => pnlGoto.IsVisible = false;

        private void BtnCloseSlot_Clicked(object sender, EventArgs e) => pnlSlots.IsVisible = false;

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

            _ResetIdleTimer();

            int oldSSKU = sSKU;

            switch (btn.StyleId)
            {
                case "btnGoto":
                    txtLine.Text      = string.Empty;
                    pnlGoto.IsVisible = true;
                    btnFirst.Focus();
                    return;

                case "btnFirst":
                    sSKU = 0;
                    pnlGoto.IsVisible = false;
                    System.Diagnostics.Debug.WriteLine($"🔵 FIRST clicked: sSKU {oldSSKU} → {sSKU}");
                    break;

                case "btnPrev":
                    if (sSKU > 0)
                    {
                        sSKU--;
                        System.Diagnostics.Debug.WriteLine($"🔵 PREV clicked: sSKU {oldSSKU} → {sSKU}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"🔴 PREV blocked: already at first (sSKU={sSKU})");
                    }
                    break;

                case "btnNext":
                    System.Diagnostics.Debug.WriteLine($"📊 NEXT clicked: sSKU={sSKU}, Count={pickList.Count}");
                    if (sSKU < pickList.Count - 1)
                    {
                        sSKU++;
                        System.Diagnostics.Debug.WriteLine($"🔵 NEXT: sSKU {oldSSKU} → {sSKU}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"🔴 NEXT blocked: already at last (sSKU={sSKU})");
                    }
                    break;

                case "btnLast":
                    sSKU = pickList.Count - 1;
                    pnlGoto.IsVisible = false;
                    System.Diagnostics.Debug.WriteLine($"🔵 LAST clicked: sSKU {oldSSKU} → {sSKU}");
                    break;

                case "btnUnpick":
                    sSKU = -1;
                    pnlGoto.IsVisible = false;
                    System.Diagnostics.Debug.WriteLine($"🔵 UNPICK clicked: sSKU reset to -1");
                    break;
            }

            System.Diagnostics.Debug.WriteLine($"📍 Before _GetSKUtoPickAsync: sSKU={sSKU}");
            await _GetSKUtoPickAsync();
            System.Diagnostics.Debug.WriteLine($"📍 After  _GetSKUtoPickAsync: sSKU={sSKU}");
        }

        // ================== TAP GESTURE HANDLERS ==================

        private void LlblDescr_Tapped(object sender, EventArgs e)
        {
            barcodeList.Clear();

            if (!string.IsNullOrWhiteSpace(txtpSKU?.Text) && !string.IsNullOrWhiteSpace(txtpSKU_UPC))
            {
                foreach (var sUPC in txtpSKU_UPC.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var cleaned = sUPC.Replace("-", "").Trim();
                    if (!string.IsNullOrEmpty(cleaned))
                        barcodeList.Add(new BarcodeItem { Text = "", UPC = cleaned });
                }
            }

            pnlBarcodes.IsVisible = true;

            if (pnlBarcodes.Content is Microsoft.Maui.Controls.Layout layout &&
                layout.Children.FirstOrDefault() is Entry firstEntry)
                firstEntry.Focus();
        }

        private void LlblSlot_Tapped(object sender, EventArgs e)
        {
            if (txtpSlot.Text == "<< Multiple Slots >>" && !string.IsNullOrEmpty(txtpSlot_Value))
            {
                lvSlots.ItemsSource = txtpSlot_Value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => new SlotItem { Slot = s.Trim() })
                    .ToList();
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
                txtpSlot.Text      = selectedSlot.Slot;
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

        // ================== MODAL DIALOG ==================

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

        /// <summary>
        /// Show custom modal dialog (truly modal – blocks all interaction).
        /// Only used for the very first server request.
        /// </summary>
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

        // ================== CORE BUSINESS LOGIC ==================

        /// <summary>
        /// Resets all in-memory session state so the page is ready to receive a
        /// brand-new Transfer/SKU assignment without navigating away.
        /// </summary>
        private void _ResetSessionState()
        {
            // In-memory data
            ID_SumHdr          = 0;
            AppGlobal.ID_SumHdr = 0;
            pickList.Clear();
            sSKU       = -1;
            isStarted  = false;
            ID_Stocker = 0;

            // Request-guard flags
            iRetry               = 0;
            _requestFailedShown  = false;
            _requestAlreadyShown = false;

            // Idle-monitor flags (monitoring itself was stopped by _StopIdleMonitoring)
            _idleAlertShown    = false;
            _warningAlertShown = false;

            // Clear UI on the main thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                txtDone.Text    = "";
                txtBarcode.Text = "";
                txtSKU.Text     = "";
                txtCase.Text    = "0";
                txtEach.Text    = "0";
                txtpSKU.Text    = "";
                txtpDescr.Text  = "";
                txtpSlot.Text   = "";
                txtpCase.Text   = "0";
                txtpEach.Text   = "0";
                txtDeptStore.Text = "";
                btnFinished.IsVisible = false;

                // Re-hide all overlay panels
                pnlConfirm.IsVisible  = false;
                pnlBarcodes.IsVisible = false;
                pnlSlots.IsVisible    = false;
                pnlGoto.IsVisible     = false;
            });
        }

        /// <summary>
        /// Entry point for loading / requesting a picking session.
        /// <para>
        /// <paramref name="autoRequest"/> = <c>false</c> (default) →
        ///   first-time path: shows "Request from server?" modal.<br/>
        /// <paramref name="autoRequest"/> = <c>true</c> →
        ///   continuation path: silently issues the next request with no modal,
        ///   provided the picker has already confirmed at least once
        ///   (<see cref="_hasEverRequested"/> is <c>true</c>).
        /// </para>
        /// </summary>
        private async Task _GetSetPickNoAsync(bool autoRequest = false)
        {
            btnFinished.IsVisible = false;

            string sUserPNo = "";

            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                await DisplayAlert("No Connection!",
                    "Cannot connect to server! Please retry or check settings...", "OK");
                await Navigation.PopModalAsync();
                return;
            }

            try
            {
                // ── Read the user's current session from the DB ──
                using (var sqlCmd = new SqlCommand(
                    "SELECT ID_SumHdr, PickRef FROM tblUsers WHERE ID=@UserID", conn))
                {
                    sqlCmd.Parameters.AddWithValue("@UserID", AppGlobal.ID_User);

                    using var reader = await sqlCmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        long dbSumHdr = reader["ID_SumHdr"] != DBNull.Value
                            ? Convert.ToInt64(reader["ID_SumHdr"]) : 0;

                        AppGlobal.ID_SumHdr = dbSumHdr;
                        ID_SumHdr           = dbSumHdr;

                        System.Diagnostics.Debug.WriteLine($"📊 Read from DB – ID_SumHdr: {ID_SumHdr}");

                        if (reader["PickRef"] != DBNull.Value)
                        {
                            long pickRefValue = Convert.ToInt64(reader["PickRef"]);
                            if (pickRefValue != 0)
                            {
                                sUserPNo = pickRefValue.ToString().Trim();
                                System.Diagnostics.Debug.WriteLine($"📊 PickRef: {sUserPNo}, pPickNo: {AppGlobal.pPickNo}");
                            }
                        }
                    }
                }

                // ── If the server already assigned a session, load it directly ──
                if (ID_SumHdr != 0 && sUserPNo == AppGlobal.pPickNo)
                {
                    System.Diagnostics.Debug.WriteLine("✅ Found existing picking session – loading data...");
                    _ShowLoading("Loading existing picking session...");
                    await Task.Yield();
                    await _AddSKUtoListAsync();
                    return;
                }

                System.Diagnostics.Debug.WriteLine(
                    $"❌ No existing session – will request. " +
                    $"autoRequest={autoRequest}, _hasEverRequested={_hasEverRequested}, " +
                    $"ID_SumHdr={ID_SumHdr}, sUserPNo={sUserPNo}");

                if (_isRequesting) return;
                _isRequesting = true;

                try
                {
                    // ════════════════════════════════════════════════════════
                    // AUTO-REQUEST PATH
                    // Silently issue the next request – no modal, no prompt.
                    // Used after BtnFinished completes a Transfer/SKU.
                    // ════════════════════════════════════════════════════════
                    if (autoRequest && _hasEverRequested)
                    {
                        System.Diagnostics.Debug.WriteLine("🔄 Auto-requesting next Transfer/SKU...");

                        _ShowLoading("Loading next picking session...");
                        await Task.Yield();

                        using var autoCmd = new SqlCommand(
                            "UPDATE tblUsers SET isRequest=1, isSummary=@Summary, PickRef=@PickRef WHERE ID=@UserID",
                            conn);
                        autoCmd.Parameters.AddWithValue("@Summary", AppGlobal.isSummary);
                        autoCmd.Parameters.AddWithValue("@PickRef", AppGlobal.pPickNo);
                        autoCmd.Parameters.AddWithValue("@UserID",  AppGlobal.ID_User);
                        await autoCmd.ExecuteNonQueryAsync();

                        tmrRequest.Start();
                        return; // timer will call _AddSKUtoListAsync when assigned
                    }

                    // ════════════════════════════════════════════════════════
                    // FIRST-TIME REQUEST PATH
                    // Show the "Request from server?" modal once.
                    // ════════════════════════════════════════════════════════
                    bool requestFromServer = await ShowCustomModalDialog(
                        "Requesting...",
                        "Request from server?",
                        "Yes", "No");

                    if (requestFromServer)
                    {
                        // Mark that the picker has now given at least one explicit confirmation.
                        // All future requests in this page session will be automatic.
                        _hasEverRequested = true;

                        _ShowLoading("Requesting...");
                        await Task.Yield();

                        using var updateCmd = new SqlCommand(
                            "UPDATE tblUsers SET isRequest=1, isSummary=@Summary, PickRef=@PickRef WHERE ID=@UserID",
                            conn);
                        updateCmd.Parameters.AddWithValue("@Summary", AppGlobal.isSummary);
                        updateCmd.Parameters.AddWithValue("@PickRef", AppGlobal.pPickNo);
                        updateCmd.Parameters.AddWithValue("@UserID",  AppGlobal.ID_User);
                        await updateCmd.ExecuteNonQueryAsync();

                        tmrRequest.Start();
                    }
                    else
                    {
                        // User explicitly declined – return to Main Menu
                        _HideLoading();
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
                _HideLoading();
                _isRequesting = false;
                await DisplayAlert("Error", ex.Message, "OK");
                await Navigation.PopModalAsync();
            }
        }

        private async Task _AddSKUtoListAsync()
        {
            MainThread.BeginInvokeOnMainThread(() => loadingText.Text = "Loading picking data...");

            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null)
            {
                _HideLoading();
                await DisplayAlert("Error", "Cannot connect to server!", "OK");
                return;
            }

            try
            {
                using (var cmdHdr = new SqlCommand(
                    $"SELECT * FROM tbl{pPickNo}PickHdr WHERE ID=@ID AND TimeEnd='0'", conn))
                {
                    cmdHdr.Parameters.AddWithValue("@ID", ID_SumHdr);
                    using var readerHdr = await cmdHdr.ExecuteReaderAsync();
                    if (await readerHdr.ReadAsync())
                    {
                        isStarted   = readerHdr["TimeStart"].ToString().Trim() != "0";
                        var deptId  = Convert.ToInt32(readerHdr["iDept"]);
                        readerHdr.Close();

                        if (isSummary == 1)
                        {
                            lblDeptStore.Text  = "Department:";
                            txtDeptStore.Text  = await AppGlobal._GetDeptName(deptId);
                        }
                        else
                        {
                            lblDeptStore.Text  = "Store No:";
                            txtDeptStore.Text  = await AppGlobal._GetStoreNo();
                        }
                    }
                }

                var dsData       = new DataSet();
                bool querySuccess = await AppGlobal._WorkQueryAsync(
                    $"SELECT * FROM tbl{pPickNo}PickQty WHERE ID_SumHdr={ID_SumHdr} ORDER BY Slot, SKU",
                    dsData, "PickQty");

                if (!querySuccess || dsData.Tables.Count == 0)
                {
                    _HideLoading();
                    await DisplayAlert("Error", "Failed to load picking data!", "OK");
                    return;
                }

                pickList.Clear();

                foreach (DataRow dRow in dsData.Tables["PickQty"].Rows)
                {
                    var isPicked  = dRow["isPicked"].ToString().Trim() != "0";
                    var pickedQty = isPicked ? Convert.ToDouble(dRow["PickQty"]) : 0;

                    pickList.Add(new PickQtyItem
                    {
                        ID        = Convert.ToInt32(dRow["ID"]),
                        BUM       = dRow["BUM"].ToString().Trim(),
                        Slot      = dRow["Slot"].ToString().Trim(),
                        SKU       = dRow["SKU"].ToString().Trim(),
                        Descr     = dRow["Descr"].ToString().Trim(),
                        Qty       = Convert.ToDouble(dRow["Qty"]),
                        UPC       = dRow["UPC"].ToString().Trim(),
                        IsPicked  = isPicked && pickedQty > 0,
                        PickedQty = pickedQty
                    });
                }

                sSKU = -1;
                await _GetSKUtoPickAsync();

                _StartIdleMonitoring();
            }
            catch (Exception ex)
            {
                _HideLoading();
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async Task _GetSKUtoPickAsync()
        {
            System.Diagnostics.Debug.WriteLine(
                $"🟢 _GetSKUtoPickAsync START: sSKU={sSKU}, Count={pickList?.Count ?? 0}");

            if (pickList == null || pickList.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("🔴 EXIT: pickList is null or empty");
                return;
            }

            // Auto-find first unpicked item
            if (sSKU == -1)
            {
                System.Diagnostics.Debug.WriteLine("🔍 Auto-finding first unpicked item...");
                for (int i = 0; i < pickList.Count; i++)
                {
                    if (!pickList[i].IsPicked)
                    {
                        sSKU = i;
                        System.Diagnostics.Debug.WriteLine($"✅ Found unpicked item at index {i}");
                        break;
                    }
                }

                if (sSKU == -1)
                {
                    sSKU = 0;
                    System.Diagnostics.Debug.WriteLine("⚠️ All items picked, defaulting to index 0");
                }
            }

            // Clamp to valid range
            int orig = sSKU;
            if (sSKU < 0)                   { sSKU = 0;                   System.Diagnostics.Debug.WriteLine($"⚠️ Clamped {orig} → 0"); }
            if (sSKU >= pickList.Count)     { sSKU = pickList.Count - 1;  System.Diagnostics.Debug.WriteLine($"⚠️ Clamped {orig} → {sSKU}"); }

            var currentItem = pickList[sSKU];

            System.Diagnostics.Debug.WriteLine($"📦 Loading SKU[{sSKU}]: {currentItem.SKU}");

            txtpEachTag = currentItem.Qty;
            txtpCaseTag = currentItem.BUM;

            double dSetQty = Convert.ToDouble(txtpCaseTag ?? 0);
            double eachQty = Convert.ToDouble(txtpEachTag ?? 0);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Slot
                if (currentItem.Slot.Contains(","))
                {
                    txtpSlot.Text     = "<< Multiple Slots >>";
                    txtpSlotTag       = currentItem.Slot;
                    txtpSlot_Value    = currentItem.Slot;
                }
                else
                {
                    txtpSlot.Text     = currentItem.Slot;
                    txtpSlotTag       = "";
                    txtpSlot_Value    = "";
                }

                // SKU / Description
                txtpSKU.Text    = currentItem.SKU;
                txtpSKU_UPC     = currentItem.UPC;
                txtpDescr.Text  = currentItem.Descr;

                System.Diagnostics.Debug.WriteLine($"🖥️ txtpSKU  = '{txtpSKU.Text}'");
                System.Diagnostics.Debug.WriteLine($"🖥️ txtpDescr = '{txtpDescr.Text}'");

                txtpSKU.InvalidateMeasure();
                txtpDescr.InvalidateMeasure();
                llblDescr.InvalidateMeasure();

                // Quantity breakdown
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

                // Scanned status
                pbScanned.IsVisible = currentItem.IsPicked;

                if (currentItem.IsPicked)
                {
                    txtSKU.Text = txtpSKU.Text;
                    double pQty = currentItem.PickedQty;

                    if (dSetQty == 1 || pQty < dSetQty)
                    {
                        txtCase.Text = "0";
                        txtEach.Text = pQty.ToString("N2");
                    }
                    else
                    {
                        txtCase.Text = Math.Floor(pQty / dSetQty).ToString("N2");
                        txtEach.Text = (pQty % dSetQty).ToString("N2");
                    }
                }
                else
                {
                    txtBarcode.Text = string.Empty;
                    txtSKU.Text     = string.Empty;
                    txtEach.Text    = "0";
                    txtCase.Text    = "0";
                }

                // Navigation button states
                if (pickList.Count > 0)
                {
                    btnFirst.IsEnabled = sSKU > 0;
                    btnPrev.IsEnabled  = sSKU > 0;
                    btnNext.IsEnabled  = sSKU < pickList.Count - 1;
                    btnLast.IsEnabled  = sSKU < pickList.Count - 1;
                }
                else
                {
                    btnFirst.IsEnabled = btnPrev.IsEnabled =
                    btnNext.IsEnabled  = btnLast.IsEnabled = false;
                }
            });

            _CountPicked();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!currentItem.IsPicked) txtBarcode.Focus();
            });

            System.Diagnostics.Debug.WriteLine($"🟢 _GetSKUtoPickAsync END: sSKU={sSKU}");
            System.Diagnostics.Debug.WriteLine("═══════════════════════════════════════════════\n");
        }

        private void _CountPicked()
        {
            int iPicked = 0;

            if (pickList != null)
            {
                foreach (var item in pickList)
                    if (item.IsPicked) iPicked++;

                txtDone.Text = $"{iPicked}/{pickList.Count}";

                if (iPicked == pickList.Count)
                    btnFinished.IsVisible = true;
            }

            _HideLoading();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (sSKU >= 0 && sSKU < pickList.Count && !pickList[sSKU].IsPicked)
                    txtBarcode.Focus();
            });
        }

        private void _ClearScan(bool bWithBarcode = true)
        {
            if (bWithBarcode) txtBarcode.Text = string.Empty;

            txtSKU.Text  = string.Empty;
            txtEach.Text = "0";
            txtCase.Text = "0";

            txtEach.CursorPosition = 0;
            txtCase.CursorPosition = 0;

            _ = txtBarcode.Focus();

            txtBarcode.CursorPosition  = 0;
            txtBarcode.SelectionLength = txtBarcode.Text?.Length ?? 0;
            txtEach.CursorPosition     = 0;
            txtEach.SelectionLength    = txtEach.Text?.Length ?? 0;
            txtCase.CursorPosition     = 0;
            txtCase.SelectionLength    = txtCase.Text?.Length ?? 0;
        }

        private async Task _AcceptItemAsync()
        {
            _ResetIdleTimer();

            using var conn = await AppGlobal._SQL_Connect();
            if (conn == null) return;

            using var sqlCmd = conn.CreateCommand();

            double caseTag = Convert.ToDouble(txtpCaseTag ?? 0);
            double caseQty = double.TryParse(txtCase.Text, out double cQ) ? cQ : 0;
            double eachQty = double.TryParse(txtEach.Text, out double eQ) ? eQ : 0;
            double dQty    = (caseTag * caseQty) + eachQty;

            if (sSKU < 0 || sSKU >= pickList.Count) return;
            var lvItem = pickList[sSKU];

            lvItem.PickedQty = dQty;
            lvItem.IsPicked  = true;

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

            string sUPC = "";
            if (!pbScanned.IsVisible && !string.IsNullOrEmpty(txtBarcode.Text))
                sUPC = "UPC=@UPC,";
            if (eachQty == 0 && caseQty == 0)
                sUPC = "";

            if (isSummary == 1)
            {
                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET " +
                    $"{sUPC}PickBy=@PickBy, ConfBy=@ConfBy, PickTime=@PickTime " +
                    "WHERE SKU=@SKU AND ID_SumHdr=@SumHdr";
                sqlCmd.Parameters.Clear();
                if (!string.IsNullOrEmpty(sUPC)) sqlCmd.Parameters.AddWithValue("@UPC", txtBarcode.Text);
                sqlCmd.Parameters.AddWithValue("@PickBy",   AppGlobal.ID_User);
                sqlCmd.Parameters.AddWithValue("@ConfBy",   ID_Stocker);
                sqlCmd.Parameters.AddWithValue("@PickTime", await AppGlobal._GetDateTime());
                sqlCmd.Parameters.AddWithValue("@SKU",      lvItem.SKU);
                sqlCmd.Parameters.AddWithValue("@SumHdr",   ID_SumHdr);
                await sqlCmd.ExecuteNonQueryAsync();
            }
            else
            {
                var dsData = new DataSet();
                using (var readCmd = conn.CreateCommand())
                {
                    readCmd.CommandText =
                        $"SELECT ID, Qty FROM tbl{pPickNo}PickDtl WHERE SKU=@SKU AND ID_SumHdr=@SumHdr";
                    readCmd.Parameters.AddWithValue("@SKU",    lvItem.SKU);
                    readCmd.Parameters.AddWithValue("@SumHdr", ID_SumHdr);
                    using var adapter = new SqlDataAdapter(readCmd);
                    adapter.Fill(dsData, "PickDtl");
                }

                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET " +
                    $"{sUPC}SortTime=@SortTime, SortBy=@SortBy, PickBy=@PickBy, ConfBy=@ConfBy, PickTime=@PickTime " +
                    "WHERE SKU=@SKU AND ID_SumHdr=@SumHdr";
                sqlCmd.Parameters.Clear();
                if (!string.IsNullOrEmpty(sUPC)) sqlCmd.Parameters.AddWithValue("@UPC", txtBarcode.Text);
                sqlCmd.Parameters.AddWithValue("@SortTime", await AppGlobal._GetDateTime());
                sqlCmd.Parameters.AddWithValue("@SortBy",   AppGlobal.ID_User);
                sqlCmd.Parameters.AddWithValue("@PickBy",   AppGlobal.ID_User);
                sqlCmd.Parameters.AddWithValue("@ConfBy",   ID_Stocker);
                sqlCmd.Parameters.AddWithValue("@PickTime", await AppGlobal._GetDateTime());
                sqlCmd.Parameters.AddWithValue("@SKU",      lvItem.SKU);
                sqlCmd.Parameters.AddWithValue("@SumHdr",   ID_SumHdr);
                await sqlCmd.ExecuteNonQueryAsync();

                if (dsData.Tables["PickDtl"]?.Rows.Count > 0)
                {
                    int lCount = dsData.Tables["PickDtl"].Rows.Count - 1;

                    for (int iCount = 0; iCount <= lCount; iCount++)
                    {
                        var dRow      = dsData.Tables["PickDtl"].Rows[iCount];
                        double dNeed  = Convert.ToDouble(dRow["Qty"]);

                        if (iCount == lCount)
                        {
                            sqlCmd.CommandText =
                                $"UPDATE tbl{pPickNo}PickDtl SET isSorted=1, SortQty=@SortQty, isUpdate=1 WHERE ID=@ID";
                            sqlCmd.Parameters.Clear();
                            sqlCmd.Parameters.AddWithValue("@SortQty", dQty);
                            sqlCmd.Parameters.AddWithValue("@ID",      dRow["ID"]);
                            await sqlCmd.ExecuteNonQueryAsync();
                        }
                        else if (dQty >= dNeed)
                        {
                            sqlCmd.CommandText =
                                $"UPDATE tbl{pPickNo}PickDtl SET isSorted=1, SortQty=Qty, isUpdate=1 WHERE ID=@ID";
                            sqlCmd.Parameters.Clear();
                            sqlCmd.Parameters.AddWithValue("@ID", dRow["ID"]);
                            await sqlCmd.ExecuteNonQueryAsync();
                            dQty -= dNeed;
                        }
                        else
                        {
                            sqlCmd.CommandText =
                                $"UPDATE tbl{pPickNo}PickDtl SET isSorted=1, SortQty=@SortQty, isUpdate=1 WHERE ID=@ID";
                            sqlCmd.Parameters.Clear();
                            sqlCmd.Parameters.AddWithValue("@SortQty", dQty);
                            sqlCmd.Parameters.AddWithValue("@ID",      dRow["ID"]);
                            await sqlCmd.ExecuteNonQueryAsync();
                            dQty = 0;
                        }
                    }
                }

                dsData.Tables.Clear();
            }

            sqlCmd.CommandText =
                $"UPDATE tbl{pPickNo}PickQty SET isPicked=1, PickQty=@PickQty WHERE ID=@ID";
            sqlCmd.Parameters.Clear();
            sqlCmd.Parameters.AddWithValue("@PickQty", lvItem.PickedQty);
            sqlCmd.Parameters.AddWithValue("@ID",      lvItem.ID);
            await sqlCmd.ExecuteNonQueryAsync();

            ID_Stocker = 0;

            int  pickedCount    = pickList.Count(item => item.IsPicked);
            bool allItemsPicked = pickedCount == pickList.Count && pickList.Count > 0;

            if (allItemsPicked)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    btnFinished.IsVisible = true;
                    txtDone.Text          = $"{pickedCount}/{pickList.Count}";
                });
                _ClearScan();
            }
            else
            {
                sSKU = -1;
                await _GetSKUtoPickAsync();
            }
        }

        private async Task GetSKUDescrAsync()
        {
            if (string.IsNullOrWhiteSpace(txtBarcode.Text)) return;

            _ClearScan(false);

            if (!string.IsNullOrEmpty(txtpSKU_UPC) &&
                txtpSKU_UPC.Contains($"-{txtBarcode.Text.Trim()},"))
            {
                txtSKU.Text  = txtpSKU.Text;
                txtCase.Text = txtpCase.Text;
                txtEach.Text = txtpEach.Text;
                txtBarcode.SelectionLength = 0;

                if (!double.TryParse(txtCase.Text, out double caseVal) || caseVal == 0)
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
            else
            {
                await GetSKUDescr_WIFIAsync();
            }
        }

        private async Task GetSKUDescr_WIFIAsync()
        {
            if (string.IsNullOrWhiteSpace(txtBarcode.Text)) return;

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
                        txtSKU.Text  = txtpSKU.Text;
                        txtCase.Text = txtpCase.Text;
                        txtEach.Text = txtpEach.Text;
                        txtBarcode.SelectionLength = 0;

                        if (!double.TryParse(txtCase.Text, out double caseVal) || caseVal == 0)
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

                    btnConfirm.IsEnabled = true;
                    btnConfirm.Opacity   = 1.0;
                }
                else
                {
                    txtStockerTag        = "";
                    ID_Stocker           = 0;
                    btnConfirm.IsEnabled = false;
                    btnConfirm.Opacity   = 0.5;

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

        private async void TmrRequest_Tick(object sender, EventArgs e)
        {
            try
            {
                using var conn = await AppGlobal._SQL_Connect();
                if (conn == null) return;

                using (var cmd = new SqlCommand(
                    "SELECT ID_SumHdr FROM tblUsers WHERE ID=@ID AND ID_SumHdr<>0", conn))
                {
                    cmd.Parameters.AddWithValue("@ID", AppGlobal.ID_User);

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        tmrRequest.Stop();

                        long dbSumHdr       = Convert.ToInt64(reader["ID_SumHdr"]);
                        AppGlobal.ID_SumHdr = dbSumHdr;
                        ID_SumHdr           = dbSumHdr;

                        System.Diagnostics.Debug.WriteLine($"✅ Server assigned ID_SumHdr: {ID_SumHdr}");

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
                        await DisplayAlert("Unable to request!", "No Picking # Available!", "OK");
                        await Navigation.PopModalAsync();
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

        // ================== HARDWARE KEY HANDLERS ==================

        public void OnF1Pressed()
        {
            _ResetIdleTimer();

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
                    MainThread.BeginInvokeOnMainThread(async () => await Navigation.PopAsync());
            }
        }

        // ================== DATA CLASSES ==================

        public class BarcodeItem
        {
            public string Text { get; set; } = "";
            public string UPC  { get; set; } = "";
        }

        public class SlotItem
        {
            public string Text { get; set; } = "";
            public string Slot { get; set; } = "";
        }

        public class PickQtyItem
        {
            public int    ID        { get; set; }
            public string SKU       { get; set; } = "";
            public string Descr     { get; set; } = "";
            public string Slot      { get; set; } = "";
            public string BUM       { get; set; } = "";
            public string UPC       { get; set; } = "";
            public double Qty       { get; set; }
            public double PickedQty { get; set; }
            /// <summary>Tracks whether this item has been picked/processed.</summary>
            public bool   IsPicked  { get; set; }

            public string PickedQtyDisplay
                => PickedQty > 0 ? PickedQty.ToString("N2") : "";

            public Color PickedQtyColor
                => PickedQty > 0 ? Colors.Green : Colors.Black;
        }
    }
}