using Autofac;
using Autofac.Builder;
using Autofac.Core;
using Autofac.Features.Scanning;
using System;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace HCB.IoC
{
    public static class ContainerExtensions
    {
        private enum Bind { AsImplementedInterfaces, AsSelf }
        public static void RegisterByConvention(this ContainerBuilder b, params Assembly[] assemblies)
        {
            // 1) 클래스 마커(기존)
            RegisterByAttribute<ServiceAttribute>(b, assemblies, Bind.AsImplementedInterfaces);
            RegisterByAttribute<RepositoryAttribute>(b, assemblies, Bind.AsImplementedInterfaces);
            RegisterByAttribute<ViewModelAttribute>(b, assemblies, Bind.AsSelf);

            // 2) View (기존)
            RegisterViews(b, assemblies);

            // 3) 네이밍 규칙(보조, 기존)
            RegisterByName(b, assemblies, "Service", Bind.AsImplementedInterfaces);
            RegisterByName(b, assemblies, "Repository", Bind.AsImplementedInterfaces);
            RegisterByName(b, assemblies, "ViewModel", Bind.AsSelf);
            RegisterViewsByName(b, assemblies);

            // 4) **인터페이스 마커 지원(신규)**:
            RegisterInterfaceContractsByAttribute<ServiceAttribute>(b, assemblies);
            RegisterInterfaceContractsByAttribute<RepositoryAttribute>(b, assemblies);
        }

        // ---------- 공통: Service / Repository / ViewModel ----------
        private static void RegisterByAttribute<TAttr>(
    ContainerBuilder b, Assembly[] assemblies, Bind bind)
    where TAttr : Attribute
        {
            // 공통: 특정 Attribute(TAttr)가 붙은 concrete class만 대상으로
            Func<Type, bool> hasAttr = t =>
                t.IsClass && !t.IsAbstract &&
                t.GetCustomAttributes(typeof(TAttr), false).Any();

            // 공통: Lifetime 매칭
            Func<Lifetime, IRegistrationBuilder<object, ScanningActivatorData, DynamicRegistrationStyle>> reg =
                life =>
                {
                    var rb = b.RegisterAssemblyTypes(assemblies)
                              .Where(t => hasAttr(t) && MatchAttrLifetime<TAttr>(t, life));

                   
                        if (bind == Bind.AsImplementedInterfaces)
                            rb = rb.AsImplementedInterfaces().AsSelf();
                        else
                            rb = rb.AsSelf();

                    switch (life)
                    {
                        case Lifetime.Singleton: return rb.SingleInstance();
                        case Lifetime.Scoped: return rb.InstancePerLifetimeScope();
                        default: return rb.InstancePerDependency();
                    }
                };

            // 세 번 호출해서 각각의 lifetime으로 등록
            reg(Lifetime.Singleton);
            reg(Lifetime.Scoped);
            reg(Lifetime.Transient);
        }



        private static IRegistrationBuilder<object, ScanningActivatorData, DynamicRegistrationStyle>
            ApplyBind(IRegistrationBuilder<object, ScanningActivatorData, DynamicRegistrationStyle> rb, Bind bind)
            => bind == Bind.AsImplementedInterfaces ? rb.AsImplementedInterfaces() : rb.AsSelf();

        private static bool MatchAttrLifetime<TAttr>(Type t, Lifetime target) where TAttr : Attribute
        {
            if (!t.IsClass || t.IsAbstract) return false;
            var attr = (TAttr)t.GetCustomAttributes(typeof(TAttr), false).FirstOrDefault();
            if (attr == null) return false;

            // 모든 마커가 동일한 프로퍼티 이름(Lifetime)을 사용한다는 전제
            var prop = typeof(TAttr).GetProperty(nameof(ServiceAttribute.Lifetime));
            var value = prop != null ? (Lifetime)prop.GetValue(attr) : Lifetime.Scoped;
            return value == target;
        }

        private static void RegisterByName(ContainerBuilder b, Assembly[] assemblies, string suffix, Bind bind)
        {
            ApplyBind(
                b.RegisterAssemblyTypes(assemblies)
                 .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith(suffix)),
                bind
            ).InstancePerLifetimeScope();
        }

        // ---------- View 전용 처리 ----------
        private static void RegisterViews(ContainerBuilder b, Assembly[] assemblies)
        {
            // Singleton
            b.RegisterAssemblyTypes(assemblies)
             .Where(t => IsView(t) && MatchAttrLifetime<ViewAttribute>(t, Lifetime.Singleton))
             .AsSelf()
             .SingleInstance()
             .OnActivating(WireViewModelOnActivating(assemblies));

            // Scoped
            b.RegisterAssemblyTypes(assemblies)
             .Where(t => IsView(t) && MatchAttrLifetime<ViewAttribute>(t, Lifetime.Scoped))
             .AsSelf()
             .InstancePerLifetimeScope()
             .OnActivating(WireViewModelOnActivating(assemblies));

            // Transient
            b.RegisterAssemblyTypes(assemblies)
             .Where(t => IsView(t) && MatchAttrLifetime<ViewAttribute>(t, Lifetime.Transient))
             .AsSelf()
             .InstancePerDependency()
             .OnActivating(WireViewModelOnActivating(assemblies));
        }

        private static void RegisterViewsByName(ContainerBuilder b, Assembly[] assemblies)
        {
            b.RegisterAssemblyTypes(assemblies)
             .Where(t => IsView(t) && t.Name.EndsWith("View"))
             .AsSelf()
             .InstancePerDependency()
             .OnActivating(WireViewModelOnActivating(assemblies));
        }

        private static bool IsView(Type t)
        {
            if (!t.IsClass || t.IsAbstract) return false;

            // WPF View (Window/UserControl/FrameworkElement) 또는 [View] 마커
            var hasViewAttr = t.GetCustomAttributes(typeof(ViewAttribute), false).Any();
            var isFrameworkElement = typeof(FrameworkElement).IsAssignableFrom(t);
            return hasViewAttr || isFrameworkElement;
        }

        // View 활성화 시 ViewModel 자동 주입(FooView -> FooViewModel)
        private static Action<IActivatingEventArgs<object>> WireViewModelOnActivating(Assembly[] assemblies)
        {
            var allTypes = assemblies.SelectMany(SafeGetTypes).ToArray();

            return e =>
            {
                if (e.Instance is FrameworkElement fe && fe.DataContext == null)
                {
                    var vmType = FindMatchingViewModelType(e.Instance.GetType(), allTypes);
                    if (vmType != null)
                    {
                        var vm = e.Context.Resolve(vmType); // ViewModel은 AsSelf()로 등록되어 있어야 함
                        fe.DataContext = vm;
                    }
                }
            };
        }

        private static Type FindMatchingViewModelType(Type viewType, Type[] allTypes)
        {
            var viewName = viewType.Name;
            var vmName = viewName.EndsWith("View")
                ? viewName.Substring(0, viewName.Length - "View".Length) + "ViewModel"
                : viewName + "Model"; // 안전망

            // [ViewModel] 마커 우선
            var vm = allTypes.FirstOrDefault(t =>
                t.IsClass && !t.IsAbstract &&
                t.GetCustomAttributes(typeof(ViewModelAttribute), false).Any() &&
                t.Name.Equals(vmName, StringComparison.Ordinal));

            if (vm != null) return vm;

            // 보조: 단순 네이밍 일치
            vm = allTypes.FirstOrDefault(t =>
                t.IsClass && !t.IsAbstract &&
                t.Name.Equals(vmName, StringComparison.Ordinal));

            return vm;
        }

        private static Type[] SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null).ToArray(); }
        }
        private static void RegisterInterfaceContractsByAttribute<TAttr>(
            ContainerBuilder b, Assembly[] assemblies, Lifetime defaultLifetime = Lifetime.Scoped)
            where TAttr : Attribute
        {
            var allTypes = assemblies.SelectMany(SafeGetTypes).ToArray();
            var ifaces = allTypes.Where(t =>
                    t.IsInterface &&
                    t.GetCustomAttributes(typeof(TAttr), false).Any())
                .ToArray();

            foreach (var itf in ifaces)
            {
                // 후보 구현: 같은 어셈블리 집합에서 public, concrete, itf 구현
                var impls = allTypes.Where(t =>
                        t.IsClass && !t.IsAbstract && itf.IsAssignableFrom(t))
                    .ToArray();

                if (impls.Length == 0)
                    throw new InvalidOperationException($"인터페이스 {itf.Name} 의 구현을 찾지 못했습니다.");

                // 1) 네이밍 매치 우선: IRecipeService → RecipeService
                var expected = itf.Name.StartsWith("I") ? itf.Name.Substring(1) : itf.Name;
                var byName = impls.FirstOrDefault(t => t.Name.Equals(expected, StringComparison.Ordinal));

                // 2) [Service(Implementation=...)] 같은 속성을 쓰고 싶다면 여기서 읽어 우선 적용 가능
                //   (예: var implHint = GetImplementationFromAttr<TAttr>(itf); if (implHint!=null) ...)

                var chosen = byName ?? (impls.Length == 1 ? impls[0] : null);
                if (chosen == null)
                {
                    var names = string.Join(", ", impls.Select(t => t.FullName));
                    throw new InvalidOperationException($"인터페이스 {itf.Name} 의 구현이 여러 개입니다: {names}");
                }

                var life = GetLifetimeFromAttr<TAttr>(itf) ?? defaultLifetime;
                var rb = b.RegisterType(chosen).As(itf).AsSelf();
                ApplyLifetime(rb, life);
            }
        }
        // ========== ② 클래스/인터페이스 마커 공통 유틸 ==========

        private static Lifetime? GetLifetimeFromAttr<TAttr>(MemberInfo t) where TAttr : Attribute
        {
            var attr = (TAttr)t.GetCustomAttributes(typeof(TAttr), false).FirstOrDefault();
            if (attr == null) return null;

            var prop = typeof(TAttr).GetProperty(nameof(ServiceAttribute.Lifetime));
            return prop != null ? (Lifetime?)prop.GetValue(attr) : null;
        }

        private static void ApplyLifetime(
            IRegistrationBuilder<object, IConcreteActivatorData, SingleRegistrationStyle> rb,
            Lifetime life)
        {
            switch (life)
            {
                case Lifetime.Singleton: rb.SingleInstance(); break;
                case Lifetime.Scoped: rb.InstancePerLifetimeScope(); break;
                default: rb.InstancePerDependency(); break;
            }
        }
        /// <summary>
        /// SQLite DbContext를 AsSelf + InstancePerLifetimeScope로 등록.
        /// 사용자는 DbPath 옵션과 DbContext 타입만 지정하면 됨.
        /// </summary>
        //public static void RegisterSqliteDbContext<TContext>(
        //    this ContainerBuilder b,
        //    SqliteDbOptions opt,
        //    Action<DbContextOptionsBuilder<TContext>> configure = null)
        //    where TContext : DbContext
        //{
        //    b.Register(ctx =>
        //    {
        //        var path = opt.BuildPath();

        //        var ob = new DbContextOptionsBuilder<TContext>();
        //        ob.UseSqlite($"Data Source={path}", sqlite =>
        //        {
        //            if (!string.IsNullOrWhiteSpace(opt.MigrationsAssembly))
        //                sqlite.MigrationsAssembly(opt.MigrationsAssembly);
        //        });

        //        // 추가 옵션(로깅/캐시/동시성 등)
        //        if (configure != null)
        //        {
        //            configure.Invoke(ob);
        //        }

        //        var options = ob.Options;

        //        // (DbContextOptions<TContext>) → (DbContextOptions) → 매개변수 없는 생성자
        //        var t = typeof(TContext);
        //        var ctorTyped = t.GetConstructor(new[] { typeof(DbContextOptions<TContext>) });
        //        if (ctorTyped != null) return (TContext)ctorTyped.Invoke(new object[] { options });

        //        var ctorUntyped = t.GetConstructor(new[] { typeof(DbContextOptions) });
        //        if (ctorUntyped != null) return (TContext)ctorUntyper.Invoke(new object[] { options });

        //        var ctorEmpty = t.GetConstructor(Type.EmptyTypes);
        //        if (ctorEmpty != null) return (TContext)ctorEmpty.Invoke(null);

        //        throw new InvalidOperationException(
        //            $"{t.Name} 생성자에 DbContextOptions<{t.Name}> 또는 DbContextOptions가 필요합니다.");
        //    })
        //    .AsSelf()
        //    .InstancePerLifetimeScope();

        //    // 필요하면 Func<TContext>도 제공 (요청마다 새 컨텍스트)
        //    b.Register(ctx => new Func<TContext>(() => ctx.Resolve<TContext>()))
        //     .As<Func<TContext>>()
        //     .InstancePerLifetimeScope();
        //}

        ///// <summary>앱 시작 시 최신 마이그레이션 자동 적용 (선택)</summary>
        //public static void AutoMigrate<TContext>(this IContainer container)
        //    where TContext : DbContext
        //{
        //    using (var scope = container.BeginLifetimeScope())
        //    {
        //        var db = scope.Resolve<TContext>();
        //        db.Database.Migrate();
        //    }
        //}
    }
}
