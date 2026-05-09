using PDTPickingSystem.Views;

namespace PDTPickingSystem
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(SetRefPage), typeof(SetRefPage));
            Routing.RegisterRoute(nameof(SetUserPage), typeof(SetUserPage));
            Routing.RegisterRoute(nameof(ConfirmCheckPage), typeof(ConfirmCheckPage));
        }
    }
}

