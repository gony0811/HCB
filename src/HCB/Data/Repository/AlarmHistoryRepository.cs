using System;
using System.Collections.Generic;
using System.Linq;
using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.IoC;


namespace HCB.Data.Repository
{
    [Service(Lifetime.Scoped)]
    public class AlarmHistoryRepository : DbRepository<AlarmHistory, AppDb>
    {
        public AlarmHistoryRepository(AppDb db) : base(db)
        {
        }

        public IReadOnlyList<AlarmHistory> GetAlarmHistoryListByTimeRange(
            DateTime from, DateTime to, bool includeUpperBound = false, bool orderByTimeAsc = true)
        {
            if (from > to)
            {
                var t = from; from = to; to = t;
            }

            IEnumerable<AlarmHistory> q = _set;

            if (includeUpperBound)
                q = q.Where(h => h.UpdateTime >= from && h.UpdateTime <= to);
            else
                q = q.Where(h => h.UpdateTime >= from && h.UpdateTime < to);

            q = orderByTimeAsc ? q.OrderBy(h => h.UpdateTime) : q.OrderByDescending(h => h.UpdateTime);
            return q.ToList();
        }
    }
}
