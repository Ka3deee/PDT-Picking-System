using PDTPickingSystem.Views;

namespace PDTPickingSystem
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(SetRefPage), typeof(Views.SetRefPage));
        }
    }
}
