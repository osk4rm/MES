using System.Reflection;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Api.Controllers;
using AsistOff.MES.Production.Application.Features.ShiftHandovers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// The handover logbook is append-only (issue #293): corrections are new
/// entries, so no update/delete MediatR request may exist and the controller
/// must expose no PUT/PATCH/DELETE route.
/// </summary>
public class ShiftHandoverAppendOnlyTests
{
    private static readonly Assembly ApplicationAssembly =
        typeof(AsistOff.MES.Production.Application.Features.ShiftHandovers.ShiftHandoverResponse).Assembly;

    [Fact]
    public void NoUpdateOrDeleteRequest_ExistsInShiftHandoversFeatures()
    {
        // Arrange & Act
        var mutating = ApplicationAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && typeof(IBaseRequest).IsAssignableFrom(t)
                && (t.Namespace?.Contains("ShiftHandovers", StringComparison.Ordinal) ?? false)
                && (t.Name.Contains("Update", StringComparison.OrdinalIgnoreCase)
                    || t.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
                    || t.Name.Contains("Modify", StringComparison.OrdinalIgnoreCase)))
            .Select(t => t.FullName)
            .ToList();

        // Assert
        mutating.Should().BeEmpty("handover history is append-only; corrections are new entries");
    }

    [Fact]
    public void Controller_ExposesNoPutPatchOrDeleteRoutes()
    {
        // Arrange & Act
        var mutating = typeof(ShiftHandoversController).GetMethods()
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>())
            .Select(a => a.HttpMethods)
            .SelectMany(m => m)
            .Distinct()
            .ToList();

        // Assert
        mutating.Should().OnlyContain(m => m == "GET" || m == "POST");
    }

    [Fact]
    public void CreateRequest_ImplementsTenantRequest()
    {
        // Arrange & Act
        var requestType = ApplicationAssembly
            .GetType("AsistOff.MES.Production.Application.Features.ShiftHandovers.Create.CreateShiftHandoverRequest");

        // Assert
        requestType.Should().NotBeNull();
        typeof(ITenantRequest<ShiftHandoverResponse>).IsAssignableFrom(requestType).Should().BeTrue(
            "every new MediatR request implements exactly one of ITenantRequest or IAllowAnonymousRequest");
    }
}
