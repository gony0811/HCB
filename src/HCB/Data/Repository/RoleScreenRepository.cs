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
    [Repository(Lifetime.Singleton)]
    public class RoleScreenRepository : DbRepository<RoleScreenAccess, AppDb>
    {
        public RoleScreenRepository(IDbContextFactory<AppDb> factory, AppDb trackingContext = null) : base(factory, trackingContext)
        {
        }
    }
}
