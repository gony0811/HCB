using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.IoC;

namespace HCB.Data.Repository
{
    [Service(Lifetime.Singleton)]
    public class MotionPositionRepository : DbRepository<MotionPosition, AppDb>
    {
        public MotionPositionRepository(AppDb context) : base(context)
        {
        }
    }
}
