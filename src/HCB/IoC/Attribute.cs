using System;

namespace HCB.IoC
{
    public enum Lifetime { Singleton, Scoped, Transient }


    /// <summary>
    /// Marker Attribute ( 자동 스캔 및 컨테이너에 등록 )
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class HelperAttribute : Attribute
    {
        public Lifetime Lifetime { get; }
        public HelperAttribute(Lifetime lifetime = Lifetime.Scoped) => Lifetime = lifetime;
    }

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
}
