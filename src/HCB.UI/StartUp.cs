using Autofac;
using HCB.Data;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public class StartUp
    {
        public static IContainer Build()
        {
            var cb = new ContainerBuilder();
            var scans = new[] {
                Assembly.GetExecutingAssembly(),
                Assembly.Load("HCB"),
            };

            // === DB 경로 & 연결문자열 ===
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var dbPath = Path.Combine(exeDir, "ecb.db");
            var dbDir = Path.GetDirectoryName(dbPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            Directory.CreateDirectory(dbDir);
            var connStr = $"Data Source={dbPath};Cache=Shared";

            cb.Register(ctx =>
            {
                var opts = new DbContextOptionsBuilder<AppDb>()
                    .UseSqlite(connStr)
                    .Options;
                return new AppDb(opts);
            })
            .AsSelf()
            .InstancePerLifetimeScope();

            // === 앱 시작 시 DB 준비 ===
            cb.RegisterBuildCallback(c =>
            {
                using var s = c.BeginLifetimeScope();
                var db = s.Resolve<AppDb>();

                // 마이그레이션 적용(없으면 DB 생성)
                DbSeeder.EnsureSeededAsync(db).GetAwaiter().GetResult();

                // SQLite 튜닝
                db.Database.OpenConnection();
                try
                {
                    db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                    db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");
                }
                finally
                {
                    db.Database.CloseConnection();
                }
            });

            cb.RegisterByConvention(scans);
            return cb.Build();
        }
    }
}
