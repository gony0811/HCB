
using HCB.Data;
using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.Data.Repository;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;

namespace HCB
{
    [Service(Lifetime.Singleton)]
    public class MotionRepository : DbRepository<MotionEntity, AppDb>
    {
        public MotionRepository(IDbContextFactory<AppDb> factory, AppDb db) : base(factory, db)
        {
        }
    }
}
