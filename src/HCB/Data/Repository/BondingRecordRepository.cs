using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;

namespace HCB.Data.Repository
{
    [Service(Lifetime.Transient)]
    public class BondingRecordRepository : DbRepository<BondingRecord, AppDb>
    {
        public BondingRecordRepository(IDbContextFactory<AppDb> factory) : base(factory)
        {
        }

        /// <summary>본딩 레코드 1건을 6개 자식 테이블(측정/설비/설정/분석/통합좌표/결과)까지 모두 로드한다.</summary>
        public async Task<BondingRecord?> FindWithDetailsAsync(int id, CancellationToken ct = default)
        {
            using var db = CreateDb();
            return await db.Set<BondingRecord>()
                .AsNoTracking()
                .Include(x => x.Measurement)
                .Include(x => x.Equipment)
                .Include(x => x.Setting)
                .Include(x => x.Analysis)
                .Include(x => x.Coordinate)
                .Include(x => x.Result)
                .Include(x => x.CamDist)
                .Include(x => x.HcroPoints)
                .Include(x => x.ReMeasures)
                .FirstOrDefaultAsync(x => x.Id == id, ct)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 기간(start~end) 내 본딩 레코드를 시간 역순으로 페이지 단위 조회한다.
        /// 6개 자식 테이블을 모두 로드하며, 전체 건수(total)도 함께 반환한다.
        /// </summary>
        public async Task<(IReadOnlyList<BondingRecord> Items, int Total)> ListPageWithDetailsAsync(
            DateTime start, DateTime end, int skip, int take, CancellationToken ct = default)
        {
            using var db = CreateDb();

            var filtered = db.Set<BondingRecord>()
                .AsNoTracking()
                .Where(x => x.Time >= start && x.Time <= end);

            int total = await filtered.CountAsync(ct).ConfigureAwait(false);

            var items = await filtered
                .OrderByDescending(x => x.Time)
                .ThenByDescending(x => x.Id)
                .Skip(skip)
                .Take(take)
                .Include(x => x.Measurement)
                .Include(x => x.Equipment)
                .Include(x => x.Setting)
                .Include(x => x.Analysis)
                .Include(x => x.Coordinate)
                .Include(x => x.Result)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return (items, total);
        }

        /// <summary>
        /// 기간(start~end) 내 본딩 레코드 "전체"를 시간 역순으로 조회한다(페이징 없음).
        /// 6개 자식 테이블을 모두 로드한다. CSV 전체 출력에 사용.
        /// </summary>
        public async Task<IReadOnlyList<BondingRecord>> ListAllWithDetailsAsync(
            DateTime start, DateTime end, CancellationToken ct = default)
        {
            using var db = CreateDb();

            return await db.Set<BondingRecord>()
                .AsNoTracking()
                .Where(x => x.Time >= start && x.Time <= end)
                .OrderByDescending(x => x.Time)
                .ThenByDescending(x => x.Id)
                .Include(x => x.Measurement)
                .Include(x => x.Equipment)
                .Include(x => x.Setting)
                .Include(x => x.Analysis)
                .Include(x => x.Coordinate)
                .Include(x => x.Result)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 기간(start~end) 내 카메라 거리(CamDist) 또는 회전중심(HcroPoints) 측정이 있는 본딩 레코드를
        /// 최신순으로 조회한다(부모 Time 확보용). HCRO/카메라거리 전용 탭 표시·CSV에 사용.
        /// </summary>
        public async Task<IReadOnlyList<BondingRecord>> ListWithCamHcroAsync(
            DateTime start, DateTime end, CancellationToken ct = default)
        {
            using var db = CreateDb();

            return await db.Set<BondingRecord>()
                .AsNoTracking()
                .Where(x => x.Time >= start && x.Time <= end
                            && (x.CamDist != null || x.HcroPoints.Any()))
                .OrderByDescending(x => x.Time)
                .ThenByDescending(x => x.Id)
                .Include(x => x.CamDist)
                .Include(x => x.HcroPoints)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }
    }
}
