using Autofac;
using Autofac.Extensions.DependencyInjection; // 필수
using HCB.Data;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
            return Host.CreateDefaultBuilder(args)

                // ==============
                // Logging
                // ==============
                .ConfigureLogging(logging => logging.ClearProviders())
                .UseSerilog((context, services, config) =>
                {
                    config.ReadFrom.Configuration(context.Configuration)
                          .Enrich.FromLogContext()
                          .WriteTo.Sink(new GridLogSink());
                })

                // Autofac Provider 적용
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())

                // =============================
                // 1) Microsoft DI 등록 (DbContextFactory 포함)
                // =============================
                .ConfigureServices((context, services) =>
                {
                    // ---------- DataOptions ----------
                    services.Configure<DataOptions>(
                        context.Configuration.GetSection(DataOptions.Data));

                    // DataOptions.Singleton (MS DI)
                    services.AddSingleton(provider =>
                    {
                        var opt = provider.GetRequiredService<IOptions<DataOptions>>().Value;
                        return opt;
                    });

                    //services.AddHostedService<SequenceService>();
                    //services.AddHostedService<InterlockService>();

                    // ---------- EF DbContext Factory ----------
                    services.AddDbContextFactory<AppDb>((sp, options) =>
                    {
                        var dataOpt = sp.GetRequiredService<DataOptions>();

                        // DB 경로 생성
                        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                        var dbPath = Path.Combine(exeDir, dataOpt.Db);

                        var dbDir = Path.GetDirectoryName(dbPath);
                        if (string.IsNullOrWhiteSpace(dbDir))
                            dbDir = exeDir;

                        Directory.CreateDirectory(dbDir);

                        // SQLite 연결 문자열
                        options.UseSqlite($"Data Source={dbPath};Cache=Shared");
                    });
                })

                // =============================
                // 2) Autofac 등록
                // =============================
                .ConfigureContainer<ContainerBuilder>((context, builder) =>
                {
                    // ---------- DataOptions: Autofac에서 MS DI 값을 재사용 ----------
                    builder.Register(ctx =>
                        ctx.Resolve<IOptions<DataOptions>>().Value
                    )
                    .As<DataOptions>()
                    .SingleInstance();

                    // ---------- Convention Scan ----------
                    var scans = new[]
                    {
                        Assembly.GetExecutingAssembly(),
                        Assembly.Load("HCB")
                    };

                    builder.RegisterByConvention(scans);
                })

                .Build();
        }

        // =============================
        // DB 초기화 함수 (Migration/Seed)
        // =============================
        public static async Task InitDatabaseAsync(IHost host)
        {
            using var scope = host.Services.CreateScope();

            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDb>>();
            using var db = await factory.CreateDbContextAsync();

            // Seed
            await DbSeeder.EnsureSeededAsync(db);

            // PRAGMA 설정
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
