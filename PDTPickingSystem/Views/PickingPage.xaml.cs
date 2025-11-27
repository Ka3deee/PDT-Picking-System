// Inside your PickingPage class

using PDTPickingSystem.Helpers;

using System.Data;

/// <summary>
/// Loads SKUs for the current PickNo into SKUList
/// </summary>
private async Task _AddSKUtoList()
{
    if (!await AppGlobal.ConnectSqlAsync()) return;

    try
    {
        var ds = new DataSet();
        string sql = $"SELECT ID, SKU, Descr, Qty, PickedQty, Slot FROM tbl{pPickNo}PickQty ORDER BY ID";
        using (var cmd = new SqlCommand(sql, AppGlobal.SqlCon))
        using (var da = new SqlDataAdapter(cmd))
        {
            da.Fill(ds);
        }

        SKUList.Clear();
        foreach (DataRow row in ds.Tables[0].Rows)
        {
            SKUList.Add(new PickQtyItem
            {
                Id = row["ID"].ToString(),
                SKU = row["SKU"].ToString(),
                Descr = row["Descr"].ToString(),
                Qty = Convert.ToDouble(row["Qty"]),
                PickedQty = row["PickedQty"].ToString(),
                Slot = row["Slot"].ToString()
            });
        }
    }
    catch (Exception ex)
    {
        await DisplayAlert("Error", $"Failed to load SKUs: {ex.Message}", "OK");
    }
}

/// <summary>
/// Loads the SKU at sSKUIndex for picking
/// </summary>
private async Task _GetSKUtoPick()
{
    if (sSKUIndex < 0 || sSKUIndex >= SKUList.Count) return;

    var item = SKUList[sSKUIndex];
    txtSKU.Text = item.SKU;
    txtDescr.Text = item.Descr;
    txtCase.Text = "0";
    txtpEach.Text = "0";
    txtBarcode.Text = "";

    // Optionally focus barcode scanner or first input
    txtBarcode.Focus();

    await Task.CompletedTask; // Placeholder if you want to await DB calls later
}
