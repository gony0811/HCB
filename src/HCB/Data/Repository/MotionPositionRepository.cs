using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;

namespace HCB.Data.Repository
{
    [Service(Lifetime.Singleton)]
    public class MotionPositionRepository : DbRepository<MotionPosition, AppDb>
    {
        public MotionPositionRepository(IDbContextFactory<AppDb> factory, AppDb db) : base(factory, db)
        {
        }
    }
}
