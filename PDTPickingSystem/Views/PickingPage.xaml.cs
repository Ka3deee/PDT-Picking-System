using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using PDTPickingSystem.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PDTPickingSystem.Views
{
    public partial class PickingPage : ContentPage
    {
        private Entry txtboxFocus;
        private int sSKUIndex = 0;
        private string sSKU = "";
        private int ID_Stocker = 0;
        private bool isBarcode = true;

        private IDispatcherTimer tmrRequest;
        private int tmrRetryCounter = 0;

        private List<PickQtyItem> pickList = new List<PickQtyItem>();

        // Fields replacing Entry.Tag
        private string txtpSKU_UPC = "";
        private string txtpSlot_Value = "";

        public PickingPage()
        {
            InitializeComponent();

            txtboxFocus = txtBarcode;

            tmrRequest = Dispatcher.CreateTimer();
            tmrRequest.Interval = TimeSpan.FromSeconds(1);
            tmrRequest.Tick += TmrRequest_Tick;

            Appearing += PickingPage_Appearing;
            Disappearing += PickingPage_Disappearing;

            txtBarcode.Focused += Entry_GotFocus;
            txtBarcode.Unfocused += Entry_LostFocus;
            txtStocker.Focused += Entry_GotFocus;
            txtStocker.Unfocused += Entry_LostFocus;
            txtCase.Focused += Entry_GotFocus;
            txtpEach.Focused += Entry_GotFocus;

            txtDeptStore.TextChanged += TxtDeptStore_TextChanged;
            txtpSKU.TextChanged += TxtpSKU_TextChanged;
            lblSkuToPick.ParentChanged += LblSkuToPick_ParentChanged;

            // Tap gesture for llblDescr
            var tapGestureDescr = new TapGestureRecognizer();
            tapGestureDescr.Tapped += LlblDescr_Tapped;
            llblDescr.GestureRecognizers.Add(tapGestureDescr);

            // Tap gesture for llblSlot
            var tapGestureSlot = new TapGestureRecognizer();
            tapGestureSlot.Tapped += LlblSlot_Tapped;
            llblSlot.GestureRecognizers.Add(tapGestureSlot);
        }

        private void PickingPage_Appearing(object sender, EventArgs e)
        {
            isBarcode = true;
            pnlConfirm.IsVisible = false;
        }

        private void PickingPage_Disappearing(object sender, EventArgs e)
        {
            if (tmrRequest?.IsRunning == true)
                tmrRequest.Stop();
        }

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

        private async void TxtBarcode_Completed(object sender, EventArgs e)
        {
            await GetSKUDescrAsync();
        }

        private async Task GetSKUDescrAsync()
        {
            var currentSKU = txtpSKU.Text?.Trim();
            if (!string.IsNullOrEmpty(currentSKU))
            {
                var item = pickList.Find(p => p.SKU == currentSKU);
                llblDescr.Text = item != null ? item.Descr : "";
            }
            else
            {
                llblDescr.Text = "";
            }
        }

        private async void BtnConfirm_Clicked(object sender, EventArgs e) { }
        private void TxtStockerConfirm_Completed(object sender, EventArgs e) { }
        private void BtnNavigate_Clicked(object sender, EventArgs e) { }
        private void BtnCloseGoto_Clicked(object sender, EventArgs e) => pnlGoto.IsVisible = false;
        private void TxtLine_Completed(object sender, EventArgs e) { }

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

        private void TxtDeptStore_TextChanged(object sender, TextChangedEventArgs e)
        {
            AppGlobal.DeptStore = txtDeptStore.Text;
        }

        private void LblSkuToPick_ParentChanged(object sender, EventArgs e) { }

        private void TxtpSKU_TextChanged(object sender, TextChangedEventArgs e)
        {
            var currentSKU = txtpSKU.Text?.Trim();
            if (!string.IsNullOrEmpty(currentSKU))
            {
                var item = pickList.Find(p => p.SKU == currentSKU);
                llblDescr.Text = item != null ? item.Descr : "";
            }
            else
            {
                llblDescr.Text = "";
            }
        }

        // -----------------------------
        // llblDescr tapped
        // -----------------------------
        private void LlblDescr_Tapped(object sender, EventArgs e)
        {
            lvBarcodes.ItemsSource = null;
            var upcList = new List<BarcodeItem>();

            if (!string.IsNullOrEmpty(txtpSKU_UPC))
            {
                foreach (var sUPC in txtpSKU_UPC.Split(','))
                {
                    var cleanUPC = sUPC.Replace("-", "").Trim();
                    if (!string.IsNullOrEmpty(cleanUPC))
                        upcList.Add(new BarcodeItem { Text = "", UPC = cleanUPC });
                }
            }

            lvBarcodes.ItemsSource = upcList;
            pnlBarcodes.IsVisible = true;
        }

        // -----------------------------
        // llblSlot tapped
        // -----------------------------
        private void LlblSlot_Tapped(object sender, EventArgs e)
        {
            if (txtpSlot.Text == "<< Multiple Slots >>")
            {
                lvSlots.ItemsSource = null;
                var slotList = new List<SlotItem>();

                foreach (var sSlot in txtpSlot_Value.Split(','))
                {
                    var cleanSlot = sSlot.Trim();
                    if (!string.IsNullOrEmpty(cleanSlot))
                        slotList.Add(new SlotItem { Text = "", Slot = cleanSlot });
                }

                lvSlots.ItemsSource = slotList;
                pnlSlots.IsVisible = true;
            }
        }

        // -----------------------------
        // Helper classes
        // -----------------------------
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
            public double Qty { get; set; }
            public double PickedQty { get; set; }
            public string DisplayQty => PickedQty.ToString("N2");
        }
    }
}
