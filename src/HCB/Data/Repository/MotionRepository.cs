
using HCB.Data;
using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.Data.Repository;
using HCB.IoC;

namespace HCB
{
    [Service(Lifetime.Singleton)]
    public class MotionRepository : DbRepository<MotionEntity, AppDb>
    {
        public MotionRepository(AppDb context) : base(context)
        {
        }
    }
}
