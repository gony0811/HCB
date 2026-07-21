using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;

namespace HCB.UI
{
    public partial class RecipeCreateDto : ObservableObject
    {
        [ObservableProperty] private string name;
        [ObservableProperty] private bool isActive;
        [ObservableProperty] private ComponentType component;
    }
}
