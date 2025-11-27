using HCB.IoC;
using HCB.Data.Entity;
using HCB.Data.Interface;

namespace HCB.Data.Repository
{
    [Service(Lifetime.Scoped)]
    public class AlarmRepository : DbRepository<Alarm, AppDb>
    {
        public AlarmRepository(AppDb db) : base(db)
        {
        }
    }
}
