using HCB.Data.Entity;
using HCB.Data.Interface;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;


namespace HCB.Data.Repository
{
    [Service(Lifetime.Transient)]
    public class RecipeRepository : DbRepository<Recipe, AppDb>
    {
        private readonly AppDb _db;
        public RecipeRepository(IDbContextFactory<AppDb> factory, AppDb db) : base(factory, db)
        {
            _db = db;
        }

        public async Task SetActiveAsync(int recipeId, CancellationToken ct = default(CancellationToken))
        {

            // 트랜잭션 (EF Core 2.1: sync Commit)
            using (var tx = _db.Database.BeginTransaction())
            {
                // 1) 기존 활성 해제
                await _db.Database.ExecuteSqlRawAsync(
                    "UPDATE Recipes SET IsActive = 0 WHERE IsActive = 1"
                ).ConfigureAwait(false);

                // 2) 대상 찾기
                var target = await _db.Set<Recipe>().FindAsync(
                    new object[] { recipeId }, ct
                ).ConfigureAwait(false);

                if (target == null)
                    throw new InvalidOperationException("Recipe not found.");

                // 3) 활성화
                target.IsActive = true;
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);

                // 4) 커밋
                tx.Commit();
            }
        }

        public async Task<Recipe> CloneAsync(int sourceRecipeId, string newName = null, CancellationToken ct = default(CancellationToken))
        {
            
            using (var tx = _db.Database.BeginTransaction())
            {
                // 1) 원본 레시피 로드 (No Tracking)
                var source = await _db.Set<Recipe>()
                    .AsNoTracking()
                    .SingleOrDefaultAsync(r => r.Id == sourceRecipeId, ct)
                    .ConfigureAwait(false);

                if (source == null)
                    throw new InvalidOperationException("원본 레시피가 없습니다.");

                // 2) 새 이름 결정
                var targetName = string.IsNullOrWhiteSpace(newName)
                    ? await GenerateCopyNameAsync(source.Name, ct).ConfigureAwait(false)
                    : newName;

                // 3) 새 레시피 생성
                var newRecipe = new Recipe
                {
                    Name = targetName,
                    IsActive = false,
                    // 필요한 다른 필드 복사 시 여기에 할당
                };

                _db.Set<Recipe>().Add(newRecipe);
                await _db.SaveChangesAsync(ct).ConfigureAwait(false); // Id 확보

                // 4) 원본 파라미터 → 새 레시피로 복제
                var ps = await _db.Set<RecipeParam>()
                    .AsNoTracking()
                    .Where(p => p.RecipeId == sourceRecipeId)
                    .Select(p => new RecipeParam
                    {
                        RecipeId = newRecipe.Id,
                        Name = p.Name,
                        Value = p.Value,
                        Minimum = p.Minimum,
                        Maximum = p.Maximum,
                        ValueType = p.ValueType,
                        UnitType = p.UnitType,
                        Description = p.Description
                    })
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                if (ps.Count > 0)
                {
                    _db.Set<RecipeParam>().AddRange(ps);
                    await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                }

                tx.Commit();
                return newRecipe;
            }
        }

        private async Task<string> GenerateCopyNameAsync(string baseName, CancellationToken ct)
        {
            var name = baseName + " - 복사";
            var i = 2;

            while (await _db.Set<Recipe>().AsNoTracking()
                                    .AnyAsync(r => r.Name == name, ct)
                                    .ConfigureAwait(false))
            {
                name = baseName + " - 복사 (" + (i++) + ")";
            }
            return name;
        }
    }
}
