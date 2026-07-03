using CommunityToolkit.Mvvm.ComponentModel;
using HCB.IoC;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class WaferSeqTabViewModel : ObservableObject
    {
        public RecipeService RecipeService { get; }

        public WaferSeqTabViewModel(RecipeService recipeService)
        {
            RecipeService = recipeService;
        }
    }
}
