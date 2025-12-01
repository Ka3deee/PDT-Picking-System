using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using PDTPickingSystem.Helpers; // <-- for EntryExtensions
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace PDTPickingSystem.Views
{
    public partial class PickingPage : ContentPage
    {
        // -------------------------
        // Local helper state
        // -------------------------
        private Entry txtboxFocus;
        private int sSKUIndex = 0;        // Current index
        private string sSKU = "";         // Current SKU string
        private int ID_Stocker = 0;
        private bool isStarted = false;
        private bool isBarcode = true;

        // Timer
        private IDispatcherTimer tmrRequest;
        private int tmrRetryCounter = 0;

        // Pick list
        private List<PickQtyItem> pickList = new List<PickQtyItem>();

        // Slot & Barcode lists
        private List<string> slotList = new List<string>();
        private List<string> barcodeList = new List<string>();

        // User info
        private static int ID_User = 1;
        private static int ID_SumHdr = 0;
        private static string pPickNo = "PICK001";
        private static bool isSummary = false;

        public PickingPage()
        {
            InitializeComponent();

            txtboxFocus = txtBarcode;

            // Timer
            tmrRequest = Dispatcher.CreateTimer();
            tmrRequest.Interval = TimeSpan.FromSeconds(1);
            tmrRequest.Tick += TmrRequest_Tick;

            // Page events
            Appearing += PickingPage_Appearing;
            Disappearing += PickingPage_Disappearing;

            // Entry focus events
            txtBarcode.Focused += Entry_GotFocus;
            txtBarcode.Unfocused += Entry_LostFocus;
            txtStocker.Focused += Entry_GotFocus;
            txtStocker.Unfocused += Entry_LostFocus;
            txtCase.Focused += Entry_GotFocus;
            txtpEach.Focused += Entry_GotFocus;
        }

        // -------------------------
        // Page lifecycle
        // -------------------------
        private void PickingPage_Appearing(object sender, EventArgs e)
        {
            isBarcode = true;
            pnlConfirm.IsVisible = false;
            _GetSetPickNoAsync();
        }

        private void PickingPage_Disappearing(object sender, EventArgs e)
        {
            if (tmrRequest?.IsRunning == true)
                tmrRequest.Stop();
        }

        // -------------------------
        // Placeholder for loading Pick Number
        // -------------------------
        private async Task _GetSetPickNoAsync()
        {
            // TODO: Implement your logic to set/load current pick number
            await Task.Delay(1); // placeholder to keep it async
        }

        // -------------------------
        // Entry focus logic
        // -------------------------
        private void Entry_GotFocus(object sender, EventArgs e)
        {
            txtboxFocus = sender as Entry;

            if (txtboxFocus == txtBarcode || txtboxFocus == txtStocker)
            {
                isBarcode = txtboxFocus == txtBarcode;
                txtboxFocus.BackgroundColor = Colors.LightGreen;
            }
            else
            {
                txtboxFocus.CursorPosition = 0;
                txtboxFocus.SelectionLength = txtboxFocus.Text?.Length ?? 0;
            }
        }

        private void Entry_LostFocus(object sender, EventArgs e)
        {
            var entry = sender as Entry;
            entry.BackgroundColor = Colors.WhiteSmoke;
            entry.SelectionLength = 0;
        }

        // -------------------------
        // Barcode handling
        // -------------------------
        private async void TxtBarcode_Completed(object sender, EventArgs e)
        {
            await GetSKUDescrAsync();
        }

        private async Task GetSKUDescrAsync()
        {
            if (string.IsNullOrWhiteSpace(txtBarcode.Text)) return;

            _ClearScan(false);

            var tagValue = EntryExtensions.GetTag(txtpSKU)?.ToString() ?? "";
            bool isMatch = tagValue.Contains("-" + txtBarcode.Text.Trim() + ",");

            if (isMatch)
            {
                txtSKU.Text = txtpSKU.Text;
                txtCase.Text = txtpCase.Text; // separate txtpCase
                txtpEach.Text = txtpEach.Text;

                if (double.TryParse(txtCase.Text, out var caseVal) && caseVal == 0)
                {
                    txtpEach.Focus();
                    txtpEach.CursorPosition = 0;
                    txtpEach.SelectionLength = txtpEach.Text?.Length ?? 0;
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

        // -------------------------
        // Accept / Confirm Logic
        // -------------------------
        private async void BtnConfirm_Clicked(object sender, EventArgs e)
        {
            await AcceptItemAsync();
        }

        private async Task AcceptItemAsync()
        {
            if (!double.TryParse(txtCase.Text, out var caseVal)) caseVal = 0;
            if (!double.TryParse(txtpEach.Text, out var eachVal)) eachVal = 0;

            if (caseVal == 0 && eachVal == 0)
            {
                bool ok = await DisplayAlert("Confirm", "Quantity is 0. Accept?", "Yes", "No");
                if (!ok) return;
            }

            if (!int.TryParse(sSKU, out var index)) index = 0;
            if (index < 0 || index >= pickList.Count) return;

            var current = pickList[index];
            double setQty = current.Qty;
            double totalQty = (setQty * caseVal) + eachVal;
            current.PickedQty = totalQty;

            ID_Stocker = 0;
            sSKU = "";
            _GetSKUtoPick();
        }

        private void _GetSKUtoPick()
        {
            if (pickList.Count == 0) return;

            if (string.IsNullOrEmpty(sSKU))
            {
                for (int i = 0; i < pickList.Count; i++)
                {
                    if (pickList[i].PickedQty == 0)
                    {
                        sSKU = i.ToString();
                        break;
                    }
                }
            }

            int index = int.TryParse(sSKU, out int idx) ? idx : 0;
            if (index >= pickList.Count) index = 0;

            var item = pickList[index];

            txtpSKU.Text = item.SKU;
            txtpDescr.Text = item.Descr;
            EntryExtensions.SetTag(txtpEach, item.Qty);
            EntryExtensions.SetTag(txtpCase, 1); // separate txtpCase

            // Enable/disable navigation buttons
            btnPrev.IsEnabled = index > 0;
            btnNext.IsEnabled = index < pickList.Count - 1;

            _CountPicked();
        }

        private void _CountPicked()
        {
            int picked = 0;
            foreach (var item in pickList)
            {
                if (item.PickedQty > 0) picked++;
            }

            txtDone.Text = $"{picked}/{pickList.Count}";

            if (picked == pickList.Count)
                btnFinished.IsVisible = true;
        }

        private void _ClearScan(bool withBarcode = true)
        {
            if (withBarcode) txtBarcode.Text = "";
            txtSKU.Text = "";
            txtCase.Text = "0";
            txtpEach.Text = "0";

            txtboxFocus.Focus();
            txtboxFocus.CursorPosition = 0;
            txtboxFocus.SelectionLength = txtboxFocus.Text?.Length ?? 0;
        }

        // -------------------------
        // Stocker Confirmation
        // -------------------------
        private async void TxtStockerConfirm_Completed(object sender, EventArgs e)
        {
            await ConfirmStockerAsync();
        }

        private async Task ConfirmStockerAsync()
        {
            if (string.IsNullOrEmpty(txtStocker.Text))
            {
                await DisplayAlert("Error", "Stocker ID cannot be empty", "OK");
                return;
            }

            ID_Stocker = int.Parse(txtStocker.Text);
            pnlConfirm.IsVisible = false;
        }

        // -------------------------
        // Navigation buttons
        // -------------------------
        private void BtnNavigate_Clicked(object sender, EventArgs e)
        {
            if (sender == btnNext) sSKUIndex++;
            else if (sender == btnPrev) sSKUIndex--;
            else if (sender == btnFirst) sSKUIndex = 0;
            else if (sender == btnLast) sSKUIndex = pickList.Count - 1;

            if (sSKUIndex < 0) sSKUIndex = 0;
            if (sSKUIndex >= pickList.Count) sSKUIndex = pickList.Count - 1;

            sSKU = sSKUIndex.ToString();
            _GetSKUtoPick();
        }

        private void BtnCloseGoto_Clicked(object sender, EventArgs e)
        {
            pnlGoto.IsVisible = false;
        }

        private void TxtLine_Completed(object sender, EventArgs e)
        {
            if (int.TryParse(txtLine.Text, out int lineNum))
            {
                if (lineNum < 1 || lineNum > pickList.Count)
                {
                    DisplayAlert("Error", "Line number out of range!", "OK");
                    txtLine.Focus();
                    return;
                }

                sSKU = (lineNum - 1).ToString();
                _GetSKUtoPick();
            }

            pnlGoto.IsVisible = false;
        }

        // -------------------------
        // Timer tick
        // -------------------------
        private async void TmrRequest_Tick(object sender, EventArgs e)
        {
            tmrRetryCounter++;
            if (tmrRetryCounter > 5)
            {
                tmrRetryCounter = 0;
                await DisplayAlert("Error", "Unable to request pick list!", "OK");
                tmrRequest.Stop();
            }
        }

        // -------------------------
        // PickQtyItem class
        // -------------------------
        public class PickQtyItem
        {
            public int ID { get; set; }
            public string SKU { get; set; } = "";
            public string Descr { get; set; } = "";
            public double Qty { get; set; }
            public double PickedQty { get; set; }
            public string DisplayQty => PickedQty.ToString("N2");
        }
    }
}
