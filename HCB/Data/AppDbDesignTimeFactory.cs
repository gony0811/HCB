using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;


namespace HCB.Data
{
    // EF Core Tools가 디자인타임에 AppDb를 생성할 때 사용
    public sealed class AppDbDesignTimeFactory : IDesignTimeDbContextFactory<AppDb>
    {
        public AppDb CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<AppDb>();

            // 사용 중인 Provider/연결 문자열로 교체
            builder.UseSqlite("Data Source=HCB.Data.Entiry");

            return new AppDb(builder.Options);
        }
    }
}
