using ShopList.Views;

namespace ShopList;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(
            "register",
            typeof(RegisterPage));
    }
}