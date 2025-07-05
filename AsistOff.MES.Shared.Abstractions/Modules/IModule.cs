using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Shared.Abstractions.Modules;

public interface IModule
{
    string Name { get; }
    string Path { get; }
    IEnumerable<string> Policies { get; }
    void Register(IServiceCollection services);
    void Use(IApplicationBuilder app);
}