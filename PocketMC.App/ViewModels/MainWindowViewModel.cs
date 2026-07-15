using CommunityToolkit.Mvvm.ComponentModel;

namespace PocketMC.App.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableObject _currentViewModel;

        public MainWindowViewModel(DashboardViewModel dashboardViewModel)
        {
            _currentViewModel = dashboardViewModel;
        }
    }
}
