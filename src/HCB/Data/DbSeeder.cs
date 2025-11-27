using HCB.Data.Entity;
using Microsoft.EntityFrameworkCore;


namespace HCB.Data
{
    public static class DbSeeder
    {
        public static async Task EnsureSeededAsync(AppDb db, CancellationToken ct = default(CancellationToken))
        {
            db.Database.Migrate();

            // 이미 데이터가 있으면 시드 스킵 (예: Roles)
            if (await db.Set<Role>().AnyAsync(ct).ConfigureAwait(false))
                return;

            // EF Core 2.1: 동기 트랜잭션 사용
            using (var tx = db.Database.BeginTransaction())
            {
                // 1) 역할
                var op = await UpsertRoleAsync(db, "OPERATOR", rank: 100, passwordRaw: "", ct).ConfigureAwait(false);
                var engineer = await UpsertRoleAsync(db, "ENGINEER", rank: 3, passwordRaw: "", ct).ConfigureAwait(false);
                var admin = await UpsertRoleAsync(db, "ADMIN", rank: 2, passwordRaw: "", ct).ConfigureAwait(false);
                var service = await UpsertRoleAsync(db, "SERVICE_ENGINEER", rank: 1, passwordRaw: "", ct).ConfigureAwait(false);

                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                // 2) 화면(Screen)
                // Code는 고정 키(영문/대문자 권장), Name은 표시명(한글)
                var screens = new List<Screen>
                {
                    await UpsertScreenAsync(db, code:"MAIN",      name:"메인",     displayOrder: 10, isEnabled:true, ct).ConfigureAwait(false),
                    await UpsertScreenAsync(db, code:"PARAMETER", name:"파라미터", displayOrder: 20, isEnabled:true, ct).ConfigureAwait(false),
                    await UpsertScreenAsync(db, code:"USER",      name:"사용자",   displayOrder: 30, isEnabled:true, ct).ConfigureAwait(false),
                    await UpsertScreenAsync(db, code:"LOG",       name:"로그",     displayOrder: 40, isEnabled:true, ct).ConfigureAwait(false),
                    await UpsertScreenAsync(db, code:"ALARM",     name:"알람 정의", displayOrder: 50, isEnabled:true, ct).ConfigureAwait(false),
                    await UpsertScreenAsync(db, code:"MOTION",     name:"모션",     displayOrder: 70, isEnabled:true, ct).ConfigureAwait(false),
                    await UpsertScreenAsync(db, code:"IO",    name:"I/O",      displayOrder: 80, isEnabled:true, ct).ConfigureAwait(false),
                };

                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                // 3) 화면 접근권한(기본값)
                //    - 비상승 규칙을 고려해 ADMIN / SERVICE_ENGINEER 에는 모든 화면 권한을 미리 부여
                foreach (var s in screens)
                {
                    await EnsureScreenAccessAsync(db, op.Id, s.Id, grant: true, ct).ConfigureAwait(false);
                    await EnsureScreenAccessAsync(db, engineer.Id, s.Id, grant: true, ct).ConfigureAwait(false);
                    await EnsureScreenAccessAsync(db, admin.Id, s.Id, grant: true, ct).ConfigureAwait(false);
                    await EnsureScreenAccessAsync(db, service.Id, s.Id, grant: true, ct).ConfigureAwait(false);
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                // 4) 역할 간 관리권한 (누가 누구를 수정할 수 있는가)
                //    - ADMIN → {OPERATOR, ENGINEER}
                //    - SERVICE_ENGINEER → {OPERATOR, ENGINEER}
                await EnsureManageRoleAsync(db, admin.Id, op.Id, canManage: true, ct).ConfigureAwait(false);
                await EnsureManageRoleAsync(db, admin.Id, engineer.Id, canManage: true, ct).ConfigureAwait(false);
                await EnsureManageRoleAsync(db, service.Id, op.Id, canManage: true, ct).ConfigureAwait(false);
                await EnsureManageRoleAsync(db, service.Id, engineer.Id, canManage: true, ct).ConfigureAwait(false);

                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                tx.Commit();
            }
        }

        // ---------- helpers ----------

        private static async Task<Role> UpsertRoleAsync(AppDb db, string name, int rank, string passwordRaw, CancellationToken ct)
        {
            var r = await db.Set<Role>().SingleOrDefaultAsync(x => x.Name == name, ct).ConfigureAwait(false);
            if (r == null)
            {
                r = new Role { Name = name, Password = passwordRaw, Rank = rank, IsActive = true };
                db.Set<Role>().Add(r);
            }
            else
            {
                // 필요 시 업데이트
                r.Rank = rank;
                r.IsActive = true;
                if (!string.IsNullOrEmpty(passwordRaw))
                    r.Password = passwordRaw;
            }
            return r;
        }

        private static async Task<Screen> UpsertScreenAsync(AppDb db, string code, string name, int displayOrder, bool isEnabled, CancellationToken ct)
        {
            var s = await db.Set<Screen>().SingleOrDefaultAsync(x => x.Code == code, ct).ConfigureAwait(false);
            if (s == null)
            {
                s = new Screen { Code = code, Name = name, DisplayOrder = displayOrder, IsEnabled = isEnabled };
                db.Set<Screen>().Add(s);
            }
            else
            {
                s.Name = name;
                s.DisplayOrder = displayOrder;
                s.IsEnabled = isEnabled;
            }
            return s;
        }

        private static async Task EnsureScreenAccessAsync(AppDb db, int roleId, int screenId, bool grant, CancellationToken ct)
        {
            var sa = await db.Set<RoleScreenAccess>()
                             .SingleOrDefaultAsync(x => x.RoleId == roleId && x.ScreenId == screenId, ct)
                             .ConfigureAwait(false);

            if (grant)
            {
                if (sa == null)
                    db.Set<RoleScreenAccess>().Add(new RoleScreenAccess { RoleId = roleId, ScreenId = screenId, Granted = true });
                else
                    sa.Granted = true;
            }
            else
            {
                if (sa != null)
                    db.Set<RoleScreenAccess>().Remove(sa); // 또는 sa.Granted = false;
            }
        }

        private static async Task EnsureManageRoleAsync(AppDb db, int managerRoleId, int targetRoleId, bool canManage, CancellationToken ct)
        {
            if (managerRoleId == targetRoleId) return; // 자기 자신은 스킵

            var mr = await db.Set<RoleManageRole>()
                             .SingleOrDefaultAsync(x => x.ManagerRoleId == managerRoleId && x.TargetRoleId == targetRoleId, ct)
                             .ConfigureAwait(false);

            if (mr == null && canManage)
            {
                db.Set<RoleManageRole>().Add(new RoleManageRole { ManagerRoleId = managerRoleId, TargetRoleId = targetRoleId, CanManage = true });
            }
            else if (mr != null)
            {
                mr.CanManage = canManage;
                if (!canManage)
                    db.Set<RoleManageRole>().Remove(mr); // 불허 시 제거(혹은 남겨두고 false 세팅)
            }
        }
    }
}
