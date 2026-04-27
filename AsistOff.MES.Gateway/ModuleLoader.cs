using System.Reflection;
using System.Runtime.Loader;
using AsistOff.MES.Shared.Abstractions.Modules;

namespace AsistOff.MES.Gateway;

internal static class ModuleLoader
{
    public static IList<IModule> LoadModules()
        => AssemblyLoadContext.Default.Assemblies
            .Union(Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll")
                .Select(Assembly.LoadFrom)) 
            .SelectMany(SafeGetTypes)
            .Where(x => typeof(IModule).IsAssignableFrom(x) && !x.IsInterface)
            .OrderBy(x => x.Name)
            .Select(Activator.CreateInstance)
            .Cast<IModule>()
            .ToList();

    public static IList<Assembly> LoadAssemblies()
    {
        var binPath = AppDomain.CurrentDomain.BaseDirectory;
        var allDlls = Directory.GetFiles(binPath, "*.dll", SearchOption.AllDirectories);
        foreach (var dll in allDlls)
        {
            try
            {
                Assembly.LoadFrom(dll);
            }
            catch { /* ignore load errors */ }
        }
        return AppDomain.CurrentDomain.GetAssemblies().ToList();
    }

    /// <summary>
    /// <see cref="Assembly.GetTypes"/> can throw <see cref="ReflectionTypeLoadException"/> when an
    /// assembly references types that fail to load (e.g. native runtime mismatches in transitive
    /// test dependencies). Fall back to whatever types did load so module discovery keeps working.
    /// </summary>
    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }
}