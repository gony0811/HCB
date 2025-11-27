
using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.IoC;

namespace HCB.Data.Repository
{
    [Service(Lifetime.Singleton)]
    public class MotionParameterRepository : DbRepository<MotionParameter, AppDb>
    {
        public MotionParameterRepository(AppDb context) : base(context)
        {
        }
    }
}
