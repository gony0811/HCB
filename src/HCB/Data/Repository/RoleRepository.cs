
using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;

namespace HCB.Data.Repository
{
    [Service(Lifetime.Scoped)]
    public class RoleRepository : DbRepository<Role, AppDb>
    {
        private readonly AppDb db;
        public RoleRepository(AppDb db) : base(db)
        {
            this.db = db;
        }

        public async Task<Role> GetRoleAsync(string roleName, string password, CancellationToken ct = default(CancellationToken))
        {
            // EF Core 2.1은 Filtered Include/AsSplitQuery 미지원
            // 1) 기본 Role + 관련 네비게이션 전부 로드
            var role = await _set
                .Where(r => r.IsActive && r.Name == roleName && r.Password == password)
                .Include(r => r.ScreenAccesses)
                    .ThenInclude(sa => sa.Screen)
                .Include(r => r.ManageTargets)
                    .ThenInclude(m => m.Target)
                .AsNoTracking()
                .SingleOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (role == null) return null;

            // 2) 메모리에서 필터링(Granted/CanManage만 남김, 비활성 Screen 제거)
            if (role.ScreenAccesses != null)
            {
                role.ScreenAccesses = role.ScreenAccesses
                    .Where(sa => sa.Granted
                                   && sa.Screen != null
                                   && sa.Screen.IsEnabled)
                    .ToList();
            }

            if (role.ManageTargets != null)
            {
                role.ManageTargets = role.ManageTargets
                    .Where(m => m.CanManage && m.Target != null && m.Target.IsActive)
                    .ToList();
            }

            return role;
        }

        //public async Task<List<RoleScreensGroupDto>> GetManagedRolesScreensAsync(
        //    int managerRoleId,
        //    bool onlyEnabled = true,    // 전역 비활성 스크린 숨김 여부
        //    CancellationToken ct = default(CancellationToken))
        //{
        //    // 0) 관리자(배우)가 보유한 스크린 집합 → 비상승 판단용
        //    var managerScreenIds = await db.Set<RoleScreenAccess>()
        //        .Where(sa => sa.RoleId == managerRoleId
        //                     && sa.Granted
        //                     && sa.Screen != null
        //                     && sa.Screen.IsEnabled)
        //        .Select(sa => sa.ScreenId)
        //        .Distinct()
        //        .ToListAsync(ct)
        //        .ConfigureAwait(false);

        //    // 1) 내가 관리할 수 있는 대상 역할 Id 들
        //    var targetRoleIds = await db.Set<RoleManageRole>()
        //        .Where(m => m.ManagerRoleId == managerRoleId && m.CanManage)
        //        .Select(m => m.TargetRoleId)
        //        .Distinct()
        //        .ToListAsync(ct)
        //        .ConfigureAwait(false);

        //    if (targetRoleIds.Count == 0)
        //        return new List<RoleScreensGroupDto>();

        //    // 2) LEFT JOIN: 대상 역할 × (필터된) 모든 스크린
        //    var flat = await (
        //        from t in db.Set<Role>()
        //        where targetRoleIds.Contains(t.Id) && t.IsActive

        //        from s in db.Set<Screen>()
        //        where !onlyEnabled || s.IsEnabled

        //        join sa0 in db.Set<RoleScreenAccess>() on new { RoleId = t.Id, ScreenId = s.Id }
        //            equals new { sa0.RoleId, sa0.ScreenId } into grp
        //        from sa in grp.DefaultIfEmpty() // 미부여면 null

        //        orderby t.Name, s.DisplayOrder, s.Name
        //        select new RoleScreenFlat(
        //            t.Id, t.Name,
        //            s.Id, s.Code, s.Name,
        //            sa != null && sa.Granted,     // Granted: null → false
        //            s.IsEnabled,
        //            managerScreenIds.Contains(s.Id)
        //        )
        //    )
        //    .AsNoTracking()
        //    .ToListAsync(ct)
        //    .ConfigureAwait(false);

        //    // 3) 역할별 그룹으로 변환
        //    var groups = flat
        //        .GroupBy(f => new { f.RoleId, f.RoleName })
        //        .OrderBy(g => g.Key.RoleName)
        //        .Select(g =>
        //            new RoleScreensGroupDto(
        //                g.Key.RoleId,
        //                g.Key.RoleName,
        //                g.Select(f => new ScreenItemDto(
        //                    f.ScreenId, f.Code, f.Name, f.Granted, f.IsEnabled, f.CanEdit
        //                )).ToList()
        //            )
        //        )
        //        .ToList();

        //    return groups;
        //}
    }

}
