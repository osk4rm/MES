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
}