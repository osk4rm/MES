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
            .SelectMany(x => x.GetTypes())
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
}