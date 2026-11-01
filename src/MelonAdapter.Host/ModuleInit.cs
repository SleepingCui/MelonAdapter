using System.Runtime.CompilerServices;

namespace MelonAdapter.Host
{
    /// <summary>
    /// Runs before any adapter type is touched. The UnityModManager mod folder is not on the
    /// AppDomain probing path, so the adapter has to resolve its own assemblies.
    /// </summary>
    internal static class ModuleInit
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            AdapterInstall.InstallAssemblyResolver();
        }
    }
}

namespace System.Runtime.CompilerServices
{
    /// <summary>Polyfill for the C# 9 module initializer attribute (the .NET Framework does not ship it).</summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute
    {
    }
}
