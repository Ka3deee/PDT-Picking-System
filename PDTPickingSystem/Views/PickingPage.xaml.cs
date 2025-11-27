using Microsoft.Maui.Controls;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections.ObjectModel;
using System.Linq;

namespace PDTPickingSystem.Views
{
    public partial class PickingPage : ContentPage
    {
        // Fields
        private Entry txtboxFocus;
        private int iSKU = 0;
        private string sSKU = "";
        private int ID_Stocker = 0;
        private bool isStarted = false;
        private bool isBarcode = true;

        private int ID_User = 1;       // Replace with actual user ID
        private int ID_SumHdr = 0;
        private string pPickNo = "Pick001"; // Replace with actual picking ref
        private int isSummary = 0;
        private string fmtNumber2 = "0.##";

        private SqlConnection sql_Con;

        // ObservableCollection to bind to ListView
        public ObservableCollection<SKUItem> lvSKUCollection { get; set; } = new ObservableCollection<SKUItem>();

        public PickingPage()
        {
            InitializeComponent();
            BindingContext = this;

            // Initialize focus handlers
            txtBarcode.Focused += TxtBox_GotFocus;
            txtStocker.Focused += TxtBox_GotFocus;
            txtEach.Focused += TxtBox_GotFocus;
            txtCase.Focused += TxtBox_GotFocus;

            txtBarcode.Unfocused += TxtBox_LostFocus;
            txtStocker.Unfocused += TxtBox_LostFocus;
            txtEach.Unfocused += TxtBox_LostFocus;
            txtCase.Unfocused += TxtBox_LostFocus;

            // Completed events
            txtBarcode.Completed += TxtBarcode_Completed;
            txtStocker.Completed += TxtStocker_Completed;
            txtEach.Completed += TxtEach_Completed;
            txtCase.Completed += TxtCase_Completed;

            // Button clicks
            btnAccept.Clicked += BtnAccept_Clicked;
            btnConfirm.Clicked += BtnConfirm_Clicked;
            btnCancel.Clicked += BtnCancel_Clicked;
            btnFinished.Clicked += BtnFinished_Clicked;

            btnFirst.Clicked += BtnNavigate_Clicked;
            btnPrev.Clicked += BtnNavigate_Clicked;
            btnNext.Clicked += BtnNavigate_Clicked;
            btnLast.Clicked += BtnNavigate_Clicked;
            btnGoto.Clicked += BtnNavigate_Clicked;
            btnUnpick.Clicked += BtnNavigate_Clicked;

            btnCloseGoto.Clicked += (s, e) => pnlGoto.IsVisible = false;
            btnClose.Clicked += (s, e) => pnlBarcodes.IsVisible = false;
            btnCloseSlot.Clicked += (s, e) => pnlSlots.IsVisible = false;

            txtboxFocus = txtBarcode;

            // Load picking page
            GetSetPickNo();
        }

        #region Focus Handlers
        private void TxtBox_GotFocus(object sender, FocusEventArgs e)
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

        private void TxtBox_LostFocus(object sender, FocusEventArgs e)
        {
            (sender as Entry).BackgroundColor = Colors.WhiteSmoke;
        }
        #endregion

        #region Completed Handlers
        private void TxtBarcode_Completed(object sender, EventArgs e) => GetSKUDescr();
        private void TxtCase_Completed(object sender, EventArgs e)
        {
            txtEach.Focus();
            txtEach.CursorPosition = 0;
            txtEach.SelectionLength = txtEach.Text?.Length ?? 0;
        }
        private void TxtEach_Completed(object sender, EventArgs e) => BtnAccept_Clicked(null, null);
        private void TxtStocker_Completed(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtStocker.Text))
                ConfirmStocker();
        }
        #endregion

        #region Accept / Confirm / Cancel / Finish
        private async void BtnAccept_Clicked(object sender, EventArgs e)
        {
            if (!double.TryParse(txtEach.Text, out double eachQty)) eachQty = 0;
            if (!double.TryParse(txtCase.Text, out double caseQty)) caseQty = 0;

            if (eachQty == 0 && caseQty == 0)
            {
                bool acceptZero = await DisplayAlert("Accept?",
                    "You entered 0 quantity. Item is not available. Accept 0 quantity?", "Yes", "No");
                if (!acceptZero) return;

                pnlNavigate.IsVisible = false;
                pnlInput.IsVisible = false;
                txtStocker.Text = "";
                txtStocker.Tag = null;
                txtStocker.IsReadOnly = !CheckOptionStocker();
                txtStocker.Focus();
                return;
            }

            if (txtSKU.Text == txtpSKU.Text && !string.IsNullOrWhiteSpace(txtSKU.Text))
            {
                bool acceptQty = await DisplayAlert("Accept?", "Accept quantity?", "Yes", "No");
                if (acceptQty)
                    AcceptItem();
            }
        }

        private async void BtnConfirm_Clicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtStocker.Tag?.ToString()))
            {
                await DisplayAlert("OK", "Item Confirmed!", "OK");
                ID_Stocker = Convert.ToInt32(txtStocker.Tag);
                BtnCancel_Clicked(null, null);
                AcceptItem();
            }
        }

        private void BtnCancel_Clicked(object sender, EventArgs e)
        {
            pnlNavigate.IsVisible = true;
            pnlInput.IsVisible = true;
            pnlConfirm.IsVisible = false;
            txtBarcode.Focus();
        }

        private async void BtnFinished_Clicked(object sender, EventArgs e)
        {
            bool finish = await DisplayAlert("Finish?", "Finish Picking?", "Yes", "No");
            if (!finish) return;

            if (!SQLConnect()) return;
            using var sqlCmd = new SqlCommand("", sql_Con);
            foreach (var lvItem in lvSKUCollection)
            {
                if (lvItem.PickedQty == 0)
                {
                    sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET SortQty=0,isSorted=1,isUpdate=1,pickTime='00:00:00',TSort_Start='00:00:00',TSort_End='00:00:00' WHERE SKU={lvItem.SKU} AND ID_SumHdr={ID_SumHdr}";
                    sqlCmd.ExecuteNonQuery();
                }
            }

            sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickHdr SET isUpdate=1, TimeEnd='{GetDateTime()}', DateDone='{GetDateTime(true)}' WHERE ID={ID_SumHdr}";
            sqlCmd.ExecuteNonQuery();

            sqlCmd.CommandText = $"UPDATE tblUsers SET ID_SumHdr=0 WHERE ID={ID_User}";
            sqlCmd.ExecuteNonQuery();

            GetSetPickNo();
        }
        #endregion

        #region Navigation
        private void BtnNavigate_Clicked(object sender, EventArgs e)
        {
            var btn = sender as Button;
            switch (btn.Text)
            {
                case "First": sSKU = "0"; break;
                case "Prev": sSKU = (int.Parse(sSKU) - 1).ToString(); break;
                case "Next": sSKU = (int.Parse(sSKU) + 1).ToString(); break;
                case "Last": sSKU = (lvSKUCollection.Count - 1).ToString(); break;
                case "Unpick": sSKU = ""; break;
                case "Goto": pnlGoto.IsVisible = true; return;
            }
            GetSKUtoPick();
        }
        #endregion

        #region SQL & Picking Logic
        private void AcceptItem()
        {
            if (!SQLConnect()) return;
            using var sqlCmd = new SqlCommand("", sql_Con);

            double dQty = (double.Parse(txtpCase.Tag.ToString()) * double.Parse(txtCase.Text)) + double.Parse(txtEach.Text);
            int lvIndex = string.IsNullOrEmpty(sSKU) ? 0 : int.Parse(sSKU);
            var lvItem = lvSKUCollection[lvIndex];

            lvItem.PickedQty = dQty;

            if (!isStarted)
            {
                isStarted = true;
                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickHdr SET isUpdate=1, TimeStart='{GetDateTime()}' WHERE ID={ID_SumHdr}";
            }
            else
            {
                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickHdr SET isUpdate=1 WHERE ID={ID_SumHdr}";
            }
            sqlCmd.ExecuteNonQuery();

            // Update PickDtl for summary or per transfer
            if (isSummary == 1)
            {
                sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET PickBy={ID_User}, ConfBy={ID_Stocker}, PickTime='{GetDateTime()}' WHERE SKU={lvItem.SKU} AND ID_SumHdr={ID_SumHdr}";
                sqlCmd.ExecuteNonQuery();
            }
            else
            {
                var ds = new DataSet();
                WorkQuery($"SELECT ID, Qty FROM tbl{pPickNo}PickDtl WHERE SKU={lvItem.SKU} AND ID_SumHdr={ID_SumHdr}", ds);

                double qtyRemaining = dQty;
                for (int i = 0; i < ds.Tables[0].Rows.Count; i++)
                {
                    var row = ds.Tables[0].Rows[i];
                    double rowQty = Convert.ToDouble(row["Qty"]);
                    double sortQty = i == ds.Tables[0].Rows.Count - 1 ? qtyRemaining : Math.Min(rowQty, qtyRemaining);

                    sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickDtl SET SortQty={sortQty}, isSorted=1, isUpdate=1, SortTime='{GetDateTime()}', PickBy={ID_User}, ConfBy={ID_Stocker}, PickTime='{GetDateTime()}' WHERE ID={row["ID"]}";
                    sqlCmd.ExecuteNonQuery();

                    qtyRemaining -= sortQty;
                }
            }

            // Update PickQty table
            sqlCmd.CommandText = $"UPDATE tbl{pPickNo}PickQty SET isPicked=1, PickQty={lvItem.PickedQty} WHERE ID={lvItem.ID}";
            sqlCmd.ExecuteNonQuery();

            // Move to next SKU
            ID_Stocker = 0;
            if (btnNext.IsEnabled) BtnNavigate_Clicked(btnNext, null);
            else
            {
                sSKU = "";
                GetSKUtoPick();
            }
        }

        private void GetSKUDescr()
        {
            if (string.IsNullOrWhiteSpace(txtBarcode.Text)) return;

            ClearScan(false);

            if (txtpSKU.Tag?.ToString().Contains(txtBarcode.Text) ?? false)
            {
                txtSKU.Text = txtpSKU.Text;
                txtCase.Text = txtpCase.Text;
                txtEach.Text = txtpEach.Text;
                txtBarcode.SelectionLength = 0;

                if (double.Parse(txtCase.Text) == 0)
                    txtEach.Focus();
                else
                    txtCase.Focus();
            }
            else
            {
                DisplayAlert("Mismatch!", "Wrong scanned item!", "OK");
                ClearScan();
            }
        }

        private void GetSetPickNo()
        {
            pbReq.IsRunning = true;

            if (!SQLConnect()) { DisplayAlert("Error", "Cannot connect to server", "OK"); return; }

            using var sqlCmd = new SqlCommand($"SELECT ID_SumHdr, PickRef FROM tblUsers WHERE ID={ID_User}", sql_Con);
            using var reader = sqlCmd.ExecuteReader();
            if (reader.Read())
            {
                ID_SumHdr = Convert.ToInt32(reader["ID_SumHdr"]);
                if (reader["PickRef"] != DBNull.Value)
                    pPickNo = reader["PickRef"].ToString();
            }
            reader.Close();

            AddSKUtoList();
        }

        private void AddSKUtoList()
        {
            if (!SQLConnect()) return;
            using var sqlCmd = new SqlCommand($"SELECT * FROM tbl{pPickNo}PickQty WHERE ID_SumHdr={ID_SumHdr} ORDER BY Slot, SKU", sql_Con);
            using var reader = sqlCmd.ExecuteReader();
            lvSKUCollection.Clear();
            while (reader.Read())
            {
                lvSKUCollection.Add(new SKUItem
                {
                    ID = reader["ID"].ToString(),
                    Slot = reader["Slot"].ToString(),
                    SKU = reader["SKU"].ToString(),
                    Descr = reader["Descr"].ToString(),
                    Qty = Convert.ToDouble(reader["Qty"]),
                    PickedQty = Convert.ToDouble(reader["isPicked"].ToString() == "1" ? reader["PickQty"] : 0),
                    UPC = reader["UPC"].ToString(),
                    BUM = Convert.ToDouble(reader["BUM"])
                });
            }
            reader.Close();

            sSKU = "";
            GetSKUtoPick();
        }

        private void GetSKUtoPick()
        {
            ClearScan();
            SKUItem lvSKUtoPick = null;

            if (string.IsNullOrEmpty(sSKU))
            {
                lvSKUtoPick = lvSKUCollection.FirstOrDefault(x => x.PickedQty == 0);
                sSKU = lvSKUCollection.IndexOf(lvSKUtoPick).ToString();
            }
            else
            {
                lvSKUtoPick = lvSKUCollection[int.Parse(sSKU)];
            }

            txtpSlot.Text = lvSKUtoPick.Slot.Contains(",") ? "<< Multiple Slots >>" : lvSKUtoPick.Slot;
            txtpSlot.Tag = lvSKUtoPick.Slot;
            llblSlot.Text = txtpSlot.Text;
            txtpSKU.Text = lvSKUtoPick.SKU;
            txtpSKU.Tag = lvSKUtoPick.UPC;
            txtpDescr.Text = lvSKUtoPick.Descr;
            llblDescr.Text = lvSKUtoPick.Descr;

            txtpEach.Tag = lvSKUtoPick.Qty;
            txtpCase.Tag = lvSKUtoPick.BUM;

            double dSetQty = lvSKUtoPick.BUM;
            txtCase.IsEnabled = dSetQty != 1;

            if (dSetQty == 1 || lvSKUtoPick.Qty < dSetQty)
            {
                txtpCase.Text = "0";
                txtpEach.Text = lvSKUtoPick.Qty.ToString(fmtNumber2);
            }
            else
            {
                double dQty = lvSKUtoPick.Qty;
                txtpCase.Text = Math.Floor(dQty / dSetQty).ToString(fmtNumber2);
                txtpEach.Text = (dQty % dSetQty).ToString(fmtNumber2);
            }

            // If picked already
            if (lvSKUtoPick.PickedQty > 0)
            {
                txtSKU.Text = lvSKUtoPick.SKU;
                double dQty = lvSKUtoPick.PickedQty;
                if (dSetQty == 1 || dQty < dSetQty)
                {
                    txtCase.Text = "0";
                    txtEach.Text = dQty.ToString(fmtNumber2);
                }
                else
                {
                    txtCase.Text = Math.Floor(dQty / dSetQty).ToString(fmtNumber2);
                    txtEach.Text = (dQty % dSetQty).ToString(fmtNumber2);
                }
            }

            btnFirst.IsEnabled = int.Parse(sSKU) != 0;
            btnPrev.IsEnabled = int.Parse(sSKU) != 0;
            btnNext.IsEnabled = int.Parse(sSKU) != lvSKUCollection.Count - 1;
            btnLast.IsEnabled = int.Parse(sSKU) != lvSKUCollection.Count - 1;

            CountPicked();
        }

        private void CountPicked()
        {
            int picked = lvSKUCollection.Count(x => x.PickedQty > 0);
            txtDone.Text = $"{picked}/{lvSKUCollection.Count}";
            btnFinished.IsVisible = picked == lvSKUCollection.Count;
            pbReq.IsRunning = false;
        }

        private void ClearScan(bool withBarcode = true)
        {
            if (withBarcode) txtBarcode.Text = "";
            txtSKU.Text = "";
            txtEach.Text = "0";
            txtCase.Text = "0";
            txtBarcode.Focus();
        }

        private void ConfirmStocker()
        {
            if (!SQLConnect()) return;
            using var sqlCmd = new SqlCommand($"SELECT ID, (LName + ', ' + FName + ' ' + MI) AS FullName FROM tblUsers WHERE EENo={txtStocker.Text} AND isStocker=1 AND isActive=1", sql_Con);
            using var reader = sqlCmd.ExecuteReader();
            if (reader.Read())
            {
                txtStocker.Tag = reader["ID"];
                DisplayAlert("Stocker Name:", reader["FullName"].ToString(), "OK");
                btnConfirm.Focus();
            }
            else
            {
                txtStocker.Tag = null;
                DisplayAlert("Not Found!", "Stocker ID not found!", "OK");
                txtStocker.Focus();
            }
            reader.Close();
        }

        private bool SQLConnect()
        {
            if (sql_Con == null) sql_Con = new SqlConnection("your_connection_string_here");
            if (sql_Con.State != ConnectionState.Open)
            {
                try { sql_Con.Open(); return true; }
                catch { return false; }
            }
            return true;
        }

        private string GetDateTime(bool dateOnly = false) => dateOnly ? DateTime.Now.ToString("yyyy-MM-dd") : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        private bool CheckOptionStocker() => true;

        private void WorkQuery(string query, DataSet ds)
        {
            using var adapter = new SqlDataAdapter(query, sql_Con);
            adapter.Fill(ds);
        }
        #endregion
    }

    public class SKUItem
    {
        public string ID { get; set; }
        public string Slot { get; set; }
        public string SKU { get; set; }
        public string Descr { get; set; }
        public double Qty { get; set; }
        public double PickedQty { get; set; }
        public string UPC { get; set; }
        public double BUM { get; set; }
    }
}
