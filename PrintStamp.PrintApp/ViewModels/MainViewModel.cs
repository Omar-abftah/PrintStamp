using CommunityToolkit.Mvvm.ComponentModel;

namespace PrintStamp.PrintApp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Greeting { get; set; } = "Welcome to Avalonia!";
}
