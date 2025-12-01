using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.IoC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.Data.Repository
{
    [Service(Lifetime.Singleton)]
    public class IoDataRepository : DbRepository<IoDataEntity, AppDb>
    {
        public IoDataRepository(AppDb context) : base(context)
        {
        }
    }
}
