using System;

namespace HCB.IoC
{
    public enum Lifetime { Singleton, Scoped, Transient }

    /// <summary>
    /// Marker Attribute ( 자동 스캔 및 컨테이너에 등록 )
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class ServiceAttribute : Attribute
    {
        public Lifetime Lifetime { get; }
        public ServiceAttribute(Lifetime lifetime = Lifetime.Scoped) => Lifetime = lifetime;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class RepositoryAttribute : Attribute
    {
        public Lifetime Lifetime { get; }
        public RepositoryAttribute(Lifetime lifetime = Lifetime.Scoped) => Lifetime = lifetime;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class ViewModelAttribute : Attribute
    {
        public Lifetime Lifetime { get; }
        public ViewModelAttribute(Lifetime lifetime = Lifetime.Scoped) => Lifetime = lifetime;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class ViewAttribute : Attribute
    {
        public Lifetime Lifetime { get; }
        public ViewAttribute(Lifetime lifetime = Lifetime.Scoped) => Lifetime = lifetime;
    }

    // .NET Framework 4.7.2 에서는 SqliteDbOptions 클래스가 필요하므로 추가합니다.
    // 필요에 맞게 수정하여 사용하세요.
    public class SqliteDbOptions
    {
        public string DbPath { get; set; }
        public string MigrationsAssembly { get; set; }

        public string BuildPath()
        {
            // 예시: 간단한 경로 반환. 실제 프로젝트에서는 더 복잡한 로직이 필요할 수 있습니다.
            return DbPath;
        }
    }
}
