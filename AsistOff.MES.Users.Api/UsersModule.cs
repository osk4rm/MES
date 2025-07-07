using AsistOff.MES.Shared.Abstractions.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Users.Api;

internal class UsersModule : IModule
{
    public const string BasePath = "users";
    public string Name => "Users";
    public string Path => BasePath;
    public IEnumerable<string> Policies { get; } = [];
    
    public void Use(IApplicationBuilder app)
    {
    }

    public void Register(IServiceCollection services)
    {
    }
}