using System.Reflection;
using System.Runtime.Loader;
using AsistOff.MES.Shared.Abstractions.Modules;

namespace AsistOff.MES.Gateway;

internal static class ModuleLoader
{
    public static IList<IModule> LoadModules()
        => LoadApplicationAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(x => typeof(IModule).IsAssignableFrom(x) && !x.IsInterface)
            .OrderBy(x => x.Name)
            .Select(Activator.CreateInstance)
            .Cast<IModule>()
            .ToList();

    public static IList<Assembly> LoadAssemblies()
        => LoadApplicationAssemblies().ToList();

    private static IEnumerable<Assembly> LoadApplicationAssemblies()
    {
        var binPath = AppDomain.CurrentDomain.BaseDirectory;
        var applicationDlls = Directory.GetFiles(binPath, "AsistOff.MES.*.dll", SearchOption.TopDirectoryOnly);

        foreach (var dll in applicationDlls)
        {
            try
            {
                Assembly.LoadFrom(dll);
            }
            catch { /* ignore load errors */ }
        }

        return AssemblyLoadContext.Default.Assemblies
            .Where(x => x.GetName().Name?.StartsWith("AsistOff.MES.", StringComparison.Ordinal) == true);
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null)!;
        }
    }
}
