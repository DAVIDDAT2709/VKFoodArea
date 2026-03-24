using VKFoodArea.Features.Auth;

namespace VKFoodArea;

public partial class App : Application
{
    public App(LoginPage loginPage)
    {
        InitializeComponent();

        MainPage = new NavigationPage(loginPage);
    }
}