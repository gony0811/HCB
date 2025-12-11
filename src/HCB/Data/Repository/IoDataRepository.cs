using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;
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
        public IoDataRepository(IDbContextFactory<AppDb> factory, AppDb db) : base(factory, db)
        {
        }
    }
}
