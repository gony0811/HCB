using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;

namespace HCB.Data.Repository
{
    [Service(Lifetime.Transient)]
    public class VernierLogRepository : DbRepository<VernierLog, AppDb>
    {
        public VernierLogRepository(IDbContextFactory<AppDb> factory) : base(factory)
        {
        }

        /// <summary>기간 필터 + 페이징으로 버니어 로그를 최신순 조회한다.</summary>
        public async Task<(IReadOnlyList<VernierLog> Items, int Total)> ListPageAsync(
            DateTime start, DateTime end, int skip, int take, CancellationToken ct = default)
        {
            using var db = CreateDb();

            var filtered = db.Set<VernierLog>()
                .AsNoTracking()
                .Where(x => x.Time >= start && x.Time <= end);

            int total = await filtered.CountAsync(ct).ConfigureAwait(false);

            var items = await filtered
                .OrderByDescending(x => x.Time)
                .ThenByDescending(x => x.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return (items, total);
        }

        /// <summary>기간(start~end) 내 버니어 로그 "전체"를 최신순 조회한다(페이징 없음). CSV 전체 출력에 사용.</summary>
        public async Task<IReadOnlyList<VernierLog>> ListAllAsync(
            DateTime start, DateTime end, CancellationToken ct = default)
        {
            using var db = CreateDb();

            return await db.Set<VernierLog>()
                .AsNoTracking()
                .Where(x => x.Time >= start && x.Time <= end)
                .OrderByDescending(x => x.Time)
                .ThenByDescending(x => x.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }
    }
}
