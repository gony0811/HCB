using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;

namespace HCB.Data.Repository
{
    [Repository(Lifetime.Scoped)] // Convention 기반 등록
    public class LogRepository : DbRepository<LogModel, AppDb>
    {
        public LogRepository(IDbContextFactory<AppDb> factory, AppDb db) : base(factory, db)
        {
        }

        // DB 저장 로직은 AddAsync/AddRangeAsync를 상속받아 사용합니다.
    }
}
