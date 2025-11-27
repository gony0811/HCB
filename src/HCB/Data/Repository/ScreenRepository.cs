using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;

namespace HCB.Data.Repository
{
    [Service(Lifetime.Scoped)]
    public class ScreenRepository : DbRepository<Screen, AppDb>
    {
        private AppDb _db;
        public ScreenRepository(AppDb db) : base(db)
        {
            _db = db;
        }

        public async Task<bool> SetGrantAsync(int managerRoleId, int targetRoleId, int screenId, bool grant, CancellationToken ct = default(CancellationToken))
        {
            // 1) manager가 target을 관리할 수 있는지
            var canManage = await _db.Set<RoleManageRole>()
                .Where(x => x.ManagerRoleId == managerRoleId && x.TargetRoleId == targetRoleId && x.CanManage)
                .AnyAsync(ct)
                .ConfigureAwait(false);

            if (!canManage) return false;

            // 2) 관리자 자신도 해당 화면 권한을 갖고 있고, 화면이 활성 상태인지
            var managerHas = await _db.Set<RoleScreenAccess>()
                .Where(x => x.RoleId == managerRoleId && x.ScreenId == screenId && x.Granted
                            && x.Screen != null && x.Screen.IsEnabled)
                .AnyAsync(ct)
                .ConfigureAwait(false);

            if (!managerHas) return false;

            // 3) 대상 권한 부여/해제
            var sa = await _db.Set<RoleScreenAccess>()
                .SingleOrDefaultAsync(x => x.RoleId == targetRoleId && x.ScreenId == screenId, ct)
                .ConfigureAwait(false);

            if (sa == null)
            {
                _db.Set<RoleScreenAccess>().Add(new RoleScreenAccess
                {
                    RoleId = targetRoleId,
                    ScreenId = screenId,
                    Granted = grant
                });
            }
            else
            {
                sa.Granted = grant;
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }
    }
}
