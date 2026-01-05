using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HCB.Data.Repository
{
    [Service(Lifetime.Scoped)]
    public class AlarmHistoryRepository : DbRepository<AlarmHistory, AppDb>
    {
        public AlarmHistoryRepository(
            IDbContextFactory<AppDb> factory,
            AppDb? trackingContext = null)         // trackingContext는 optional
            : base(factory, trackingContext)
        {
        }

        /// <summary>
        /// 시간 범위 조건으로 AlarmHistory 조회
        /// 항상 fresh DbContext를 사용하므로 안전함
        /// </summary>
        public IReadOnlyList<AlarmHistory> GetAlarmHistoryListByTimeRange(
            DateTime from, DateTime to, bool includeUpperBound = false, bool orderByTimeAsc = true)
        {
            if (from > to)
            {
                var t = from; from = to; to = t;
            }

            // === fresh DbContext 사용 ===
            using var db = CreateDb();

            IQueryable<AlarmHistory> q = db.Set<AlarmHistory>();

            // where 조건 적용
            if (includeUpperBound)
                q = q.Where(h => h.UpdateTime >= from && h.UpdateTime <= to);
            else
                q = q.Where(h => h.UpdateTime >= from && h.UpdateTime < to);

            // 정렬 적용
            q = orderByTimeAsc
                ? q.OrderBy(h => h.UpdateTime)
                : q.OrderByDescending(h => h.UpdateTime);

            return q.ToList();
        }
    }
}
