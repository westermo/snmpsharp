using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace SnmpSharpNet.Mib.SourceGenerator.IntegrationTests;

internal static class AssemblyResolution
{
    // SnmpSharpNet.Mib is copied to the output folder but is missing from deps.json (the generator references it
    // with PrivateAssets="all"), so resolve it from the output folder for code that uses it at runtime.
    [ModuleInitializer]
    internal static void Initialize()
    {
        AssemblyLoadContext.Default.Resolving += static (context, name) =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, $"{name.Name}.dll");
            return name.Name == "SnmpSharpNet.Mib" && File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
    }
}
