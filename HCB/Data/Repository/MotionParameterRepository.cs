
using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;

namespace HCB.Data.Repository
{
    [Service(Lifetime.Singleton)]
    public class MotionParameterRepository : DbRepository<MotionParameter, AppDb>
    {
        public MotionParameterRepository(IDbContextFactory<AppDb> factory, AppDb db) : base(factory, db)
        {
        }
    }
}
