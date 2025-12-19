using HCB.Data.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public sealed class RecipeCreateVM : PromptDialogVM<Recipe>
    {

        public RecipeCreateVM(string title = null, Recipe initial = default)
            : base(initial ?? new Recipe(), title ?? "레시피 생성")
        {
        }
    }
}
