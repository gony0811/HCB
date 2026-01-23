using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace HCB.Data.Interface
{
    public interface IDbRepository<T> where T : class
    {
        // PK(단일/복합 모두) 조회
        Task<T> FindAsync(CancellationToken ct = default(CancellationToken), params object[] keyValues);

        // 목록 조회(필터/정렬/페이징)
        Task<IReadOnlyList<T>> ListAsync(
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            int? skip = null,
            int? take = null,
            bool asNoTracking = true,
            Func<IQueryable<T>, IQueryable<T>>? include = null,
            CancellationToken ct = default(CancellationToken));

        // 단순 카운트
        Task<int> CountAsync(
            Expression<Func<T, bool>>? predicate = null,
            CancellationToken ct = default(CancellationToken));

        // 쓰기
        Task<T> AddAsync(T entity, CancellationToken ct = default(CancellationToken));
        Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default(CancellationToken));
        Task<T> Update(T entity, CancellationToken ct = default(CancellationToken));

        // id 기반 삭제(정수 PK일 때만 사용)
        Task<bool> Remove(int id, CancellationToken ct = default(CancellationToken));

        // 복합키/비정수 PK 삭제
        Task<bool> RemoveByIdAsync(CancellationToken ct = default(CancellationToken), params object[] keyValues);

        // 커밋 (Unit of Work)
        Task<int> SaveAsync(CancellationToken ct = default(CancellationToken));

        Task DiscardChangesAsync(CancellationToken ct = default(CancellationToken));

        EntityEntry Attach(T entity);

        // C#7: 이벤트는 기본적으로 null 허용이므로 '?' 불필요
        event EventHandler ChangeTrackerChanged;

        bool HasChanges();
    }
}
