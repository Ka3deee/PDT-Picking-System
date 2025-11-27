using PDTPickingSystem.Services;

namespace PDTPickingSystem;

public partial class MainPage : ContentPage
{
    private readonly DatabaseService _db = new();

    public MainPage()
    {
        InitializeComponent();
    }

    private void ConnectSql_Clicked(object sender, EventArgs e)
    {
        var server = ServerEntry.Text?.Trim() ?? "";
        var user = UserEntry.Text?.Trim() ?? "";
        var pass = PasswordEntry.Text?.Trim() ?? "";

        if (_db.ConnectSql(server, user, pass))
            ResultLabel.Text = "✅ Connected successfully!";
        else
            ResultLabel.Text = "❌ Connection failed. Check details.";
    }
}
