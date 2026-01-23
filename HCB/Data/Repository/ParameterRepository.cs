using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;

namespace HCB.Data.Repository
{
    [Service(Lifetime.Transient)]
    public class ParameterRepository : DbRepository<RecipeParam, AppDb> 
    {
        public ParameterRepository(IDbContextFactory<AppDb> factory, AppDb db) : base(factory, db)
        {
        }

    }
}
