using Autofac;
using Autofac.Extensions.DependencyInjection; // 필수
using HCB.Data;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Formatting.Display;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace HCB.UI
{
    public class StartUp
    {
        public static IHost BuildHost(string[] args)
        {
            // HostedService 기반 백그라운드 서비스를 위해 MS.Extenstions.Host와 Autofac Extensions 사용
            return Host.CreateDefaultBuilder()
                .ConfigureLogging(logging => logging.ClearProviders()) // 기본 로깅 공급자 제거
                .UseSerilog((context, services, loggerConfiguration) =>
                {
                    // 원하시는 포맷
                    //var outputTemplate = "[{Timestamp:yyMMddTHH:mm:ss.fffffffz}][{Level}] {Message:lj}  { NewLine}     {Exception}";

                    loggerConfiguration
                            .ReadFrom.Configuration(context.Configuration)
                            .Enrich.FromLogContext()
                            .WriteTo.Sink(new GridLogSink());
                })
                // 1. Autofac 서비스 공급자 사용 선언
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                

                // 2. MS 표준 서비스 등록 (HostedService)
                .ConfigureServices((context, services) =>
                {
                    services.Configure<DataOptions>(context.Configuration.GetSection(DataOptions.Data));
                    // (필요하다면) DB Context 등 MS 친화적인 것들 등록
                    // services.AddDbContext<AppDb>(...); 
                })

                // 3. Autofac 컨테이너 설정 
                .ConfigureContainer<ContainerBuilder>((context, builder) =>
                {
                    var dataOptions = context.Configuration.GetSection(DataOptions.Data).Get<DataOptions>()
                           ?? new DataOptions();
                    builder.RegisterInstance(dataOptions).As<DataOptions>().SingleInstance();


                    var scans = new[] {
                        Assembly.GetExecutingAssembly(),
                        Assembly.Load("HCB"),
                    };

                    // === DB 경로 & 연결문자열 ===
                    var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                    var dbPath = Path.Combine(exeDir, dataOptions.Db);
                    var dbDir = Path.GetDirectoryName(dbPath) ?? AppDomain.CurrentDomain.BaseDirectory;
                    Directory.CreateDirectory(dbDir);
                    var connStr = $"Data Source={dbPath};Cache=Shared";

                    builder.Register(ctx =>
                    {
                        var opts = new DbContextOptionsBuilder<AppDb>()
                            .UseSqlite(connStr)
                            .Options;
                        return new AppDb(opts);
                    })
                    .AsSelf()
                    .InstancePerLifetimeScope();

                    // ★ 기존에 만드신 강력한 Convention 등록 코드 사용!
                    builder.RegisterByConvention(scans);

                    // 만약 IEmailService 구현체가 스캔 범위에 없다면 수동 등록
                    // builder.RegisterType<EmailService>().As<IEmailService>();
                })
                .Build();
        }


        // ★ DB 초기화 로직을 별도 메서드로 분리 (비동기 처리 완벽 지원)
        public static async Task InitDatabaseAsync(IHost host)
        {
            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();

                // 1. 마이그레이션 및 시딩
                await DbSeeder.EnsureSeededAsync(db);

                // 2. SQLite 튜닝 (Async/Await 사용 가능!)
                await db.Database.OpenConnectionAsync();
                try
                {
                    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                    await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");
                }
                finally
                {
                    await db.Database.CloseConnectionAsync();
                }
            }
        }
    }
}
