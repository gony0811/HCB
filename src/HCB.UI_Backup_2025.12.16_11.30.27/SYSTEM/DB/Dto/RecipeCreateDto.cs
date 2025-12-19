using CommunityToolkit.Mvvm.ComponentModel;

namespace HCB.UI
{
    public partial class RecipeCreateDto : ObservableObject
    {
        [ObservableProperty] private string name;
        [ObservableProperty] private bool isActive;
    }
}
