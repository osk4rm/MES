using AsistOff.MES.Shared.Abstractions.Contracts.Sorting;
using AsistOff.MES.Shared.Abstractions.Contracts.Validators;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Infrastructure.Behaviors;
using AsistOff.MES.Shared.Infrastructure.Errors;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace AsistOff.MES.Shared.Tests.Sorting;

/// <summary>
/// Security tests for issue #311: client-controlled sort input must never
/// reach Dynamic LINQ unless it matches the strict grammar and the
/// per-request <c>SupportedSortFields</c> whitelist.
/// </summary>
public sealed class SortWhitelistTests
{
    private sealed class Widget
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    private sealed class WidgetSort : ISortable
    {
        public List<string> RawSort { get; set; } = new();
        public IReadOnlyCollection<string> SupportedSortFields { get; } = ["Code", "Name", "IsActive"];
    }

    private sealed class EmptyWhitelistSort : ISortable
    {
        public List<string> RawSort { get; set; } = new();
        public IReadOnlyCollection<string> SupportedSortFields { get; } = [];
    }

    private sealed record BrowseWidgetsQuery(WidgetSort Sorting) : IRequest<string>, ISortable
    {
        public List<string> RawSort { get; set; } = Sorting.RawSort;
        public IReadOnlyCollection<string> SupportedSortFields => Sorting.SupportedSortFields;
    }

    private static IQueryable<Widget> Widgets() => new List<Widget>
    {
        new() { Code = "B", Name = "Beta", IsActive = true },
        new() { Code = "A", Name = "Alpha", IsActive = false },
        new() { Code = "C", Name = "Gamma", IsActive = true },
    }.AsQueryable();

    // --- SortableResolver: strict grammar ---------------------------------

    [Theory]
    [InlineData("Code", "Code", SortOrder.Ascending)]
    [InlineData("Code,asc", "Code", SortOrder.Ascending)]
    [InlineData("Code,desc", "Code", SortOrder.Descending)]
    [InlineData("Code,DESC", "Code", SortOrder.Descending)]
    [InlineData("Code,Asc", "Code", SortOrder.Ascending)]
    public void ResolveSortFields_ValidInput_ParsesFieldAndOrder(
        string raw, string expectedField, SortOrder expectedOrder)
    {
        // Act
        var result = SortableResolver.ResolveSortFields([raw]);

        // Assert
        result.Should().ContainSingle().Which.Should().Be(new SortField(expectedField, expectedOrder));
    }

    [Theory]
    [InlineData("Code desc, TenantId")] // space-separated payload, comma splits field with space
    [InlineData("Code.Equals(\"x\")")] // Dynamic LINQ method call
    [InlineData("1==1")] // expression
    [InlineData("Product.Code")] // navigation traversal via dot
    [InlineData("Code; DROP")] // statement separator / whitespace
    [InlineData("Code,desc,extra")] // extra segment
    [InlineData("Code,sideways")] // invalid order token
    [InlineData("Code,Ascending")] // only asc/desc shorthands are accepted
    [InlineData("")] // empty is skipped, never parsed
    [InlineData("   ")] // whitespace is skipped, never parsed
    public void ResolveSortFields_InjectionOrInvalidInput_NeverProducesSortField(string raw)
    {
        // Act
        var result = SortableResolver.ResolveSortFields([raw]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ResolveSortFields_Null_ReturnsEmpty()
    {
        // Act
        var result = SortableResolver.ResolveSortFields(null);

        // Assert
        result.Should().BeEmpty();
    }

    // --- QueryableExtensions.Sort: whitelist + fail closed ------------------

    [Theory]
    [InlineData("Code,asc", new[] { "A", "B", "C" })]
    [InlineData("Code,desc", new[] { "C", "B", "A" })]
    [InlineData("Name,asc", new[] { "A", "B", "C" })]
    public void Sort_WhitelistedField_OrdersResults(string rawSort, string[] expectedCodes)
    {
        // Arrange
        var request = new WidgetSort { RawSort = [rawSort] };

        // Act
        var result = Widgets().Sort(request).ToList();

        // Assert
        result.Select(w => w.Code).Should().Equal(expectedCodes);
    }

    [Theory]
    [InlineData("TenantId")] // real property but never whitelisted: isolation bypass probe
    [InlineData("TenantId,asc")]
    [InlineData("NoSuchField,asc")] // syntactically valid but unsupported
    [InlineData("Code desc, TenantId")] // injection payload from the issue
    [InlineData("Code.Equals(\"x\")")] // Dynamic LINQ method call
    [InlineData("Product.Code")] // navigation traversal
    [InlineData("Code,desc,extra")]
    [InlineData("Code,sideways")]
    public void Sort_UnknownOrMaliciousField_ThrowsBeforeOrdering(string rawSort)
    {
        // Arrange
        var request = new WidgetSort { RawSort = [rawSort] };

        // Act
        var act = () => Widgets().Sort(request).ToList();

        // Assert - ValidationException surfaces as HTTP 400 and no ordered
        // query executes because the throw happens before OrderBy/ThenBy.
        act.Should().Throw<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey("RawSort"));
    }

    [Fact]
    public void Sort_UnsupportedField_NamesTheField()
    {
        // Arrange
        var request = new WidgetSort { RawSort = ["NoSuchField,asc"] };

        // Act
        var act = () => Widgets().Sort(request).ToList();

        // Assert
        act.Should().Throw<ValidationException>()
            .Where(ex => ex.Errors["RawSort"].Any(m => m.Contains("NoSuchField")));
    }

    [Fact]
    public void Sort_LowercaseWhitelistedField_UsesCanonicalCasing()
    {
        // Arrange
        var request = new WidgetSort { RawSort = ["code,desc"] };

        // Act
        var result = Widgets().Sort(request).ToList();

        // Assert
        result.Select(w => w.Code).Should().Equal("C", "B", "A");
    }

    [Fact]
    public void Sort_EmptyWhitelistWithRealProperty_SortsForServerOwnedPaging()
    {
        // Arrange - internal FixedPaging (empty whitelist, handler-owned sort
        // such as capped typeahead ["Code"]) must keep working.
        var request = new EmptyWhitelistSort { RawSort = ["Code"] };

        // Act
        var result = Widgets().Sort(request).ToList();

        // Assert
        result.Select(w => w.Code).Should().Equal("A", "B", "C");
    }

    [Fact]
    public void Sort_EmptyWhitelistWithUnknownProperty_Throws()
    {
        // Arrange
        var request = new EmptyWhitelistSort { RawSort = ["NoSuchField,asc"] };

        // Act
        var act = () => Widgets().Sort(request).ToList();

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Sort_EmptyRawSort_ReturnsSourceUnordered()
    {
        // Arrange
        var request = new WidgetSort { RawSort = [] };

        // Act
        var result = Widgets().Sort(request).ToList();

        // Assert
        result.Select(w => w.Code).Should().Equal("B", "A", "C");
    }

    // --- Validators ----------------------------------------------------------

    [Fact]
    public async Task SortableValidator_ValidSort_Passes()
    {
        // Arrange
        var validator = new SortableValidator();
        var request = new WidgetSort { RawSort = ["Code,desc", "Name,asc"] };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task SortableValidator_UnsupportedField_FailsNamingField()
    {
        // Arrange
        var validator = new SortableValidator();
        var request = new WidgetSort { RawSort = ["NoSuchField,asc"] };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("NoSuchField"));
    }

    [Fact]
    public async Task SortableValidator_InjectionPayload_Fails()
    {
        // Arrange
        var validator = new SortableValidator();
        var request = new WidgetSort { RawSort = ["Code desc, TenantId"] };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task SortableValidator_DuplicateSortFields_Fails()
    {
        // Arrange
        var validator = new SortableValidator();
        var request = new WidgetSort { RawSort = ["Code,asc", "Code,asc"] };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("unique"));
    }

    [Fact]
    public async Task SortableValidator_InvalidOrderToken_Fails()
    {
        // Arrange
        var validator = new SortableValidator();
        var request = new WidgetSort { RawSort = ["Code,sideways"] };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Invalid sort order"));
    }

    // --- ValidationBehavior: whitelist runs for concrete requests ------------

    [Fact]
    public async Task ValidationBehavior_ConcreteSortableRequestWithInjection_ThrowsWithoutCallingNext()
    {
        // Arrange - a concrete browse request whose own validator would pass,
        // proving the ISortable whitelist is enforced by the behavior itself.
        var sorting = new WidgetSort { RawSort = ["Code desc, TenantId"] };
        var request = new BrowseWidgetsQuery(sorting);
        var behavior = new ValidationBehavior<BrowseWidgetsQuery, string>(validator: null);
        var nextCalled = false;
        Task<string> Next(CancellationToken _) { nextCalled = true; return Task.FromResult("ok"); }

        // Act
        var act = () => behavior.Handle(request, Next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ValidationBehavior_ConcreteSortableRequestWithValidSort_CallsNext()
    {
        // Arrange
        var sorting = new WidgetSort { RawSort = ["Code,asc"] };
        var request = new BrowseWidgetsQuery(sorting);
        var behavior = new ValidationBehavior<BrowseWidgetsQuery, string>(validator: null);
        Task<string> Next(CancellationToken _) => Task.FromResult("ok");

        // Act
        var result = await behavior.Handle(request, Next, CancellationToken.None);

        // Assert
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handler_SortRejectionBody_NamesTheField()
    {
        // Arrange - envelope proof for issue #311 AC2: the HTTP 400 body
        // produced by GlobalExceptionHandler for a sort whitelist rejection
        // must name the offending field. Regression guard: serializing the
        // envelope as the static ProblemDetails type dropped the
        // ValidationProblemDetails.Errors dictionary, so the body carried
        // only the generic "One or more validation failures have occurred."
        // detail and the endpoint assertion on the field name failed.
        var request = new BrowseWidgetsQuery(new WidgetSort { RawSort = ["NoSuchField,asc"] });
        var behavior = new ValidationBehavior<BrowseWidgetsQuery, string>(validator: null);
        Task<string> Next(CancellationToken _) => Task.FromResult("ok");
        ValidationException? thrown = null;
        try
        {
            await behavior.Handle(request, Next, CancellationToken.None);
        }
        catch (ValidationException ex)
        {
            thrown = ex;
        }

        thrown.Should().NotBeNull();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        // Act
        var handled = await handler.TryHandleAsync(context, thrown!, CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(400);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var payload = await JsonDocument.ParseAsync(context.Response.Body);
        payload.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.GetRawText().Should().Contain("NoSuchField");
    }
}
