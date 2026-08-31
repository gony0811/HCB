using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;

namespace HCB.Data.Repository
{
    [Service(Lifetime.Transient)]
    public class PlacementResultRepository : DbRepository<PlacementResult, AppDb>
    {
        public PlacementResultRepository(IDbContextFactory<AppDb> factory) : base(factory)
        {
        }

        /// <summary>지정 Die(Row,Col)의 가장 최근 배치 검증 결과 1건을 조회한다. 없으면 null.</summary>
        public async Task<PlacementResult?> FindLatestByDieAsync(int row, int col, CancellationToken ct = default)
        {
            using var db = CreateDb();
            return await db.Set<PlacementResult>()
                .AsNoTracking()
                .Where(x => x.Row == row && x.Col == col)
                .OrderByDescending(x => x.Time)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }

        /// <summary>기간(start~end) 내 배치 검증 결과 "전체"를 최신순 조회한다(페이징 없음). CSV 출력에 사용.</summary>
        public async Task<IReadOnlyList<PlacementResult>> ListAllAsync(
            DateTime start, DateTime end, CancellationToken ct = default)
        {
            using var db = CreateDb();
            return await db.Set<PlacementResult>()
                .AsNoTracking()
                .Where(x => x.Time >= start && x.Time <= end)
                .OrderByDescending(x => x.Time)
                .ThenByDescending(x => x.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }
    }
}
