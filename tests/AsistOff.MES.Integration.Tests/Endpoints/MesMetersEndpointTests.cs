using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for the MES business meters (issue
/// #253, slice 2/2): posting a confirmation, capturing scrap/downtime and
/// querying the OEE snapshot increments the <c>mes_*_total</c> counters and
/// duration histograms observed on the Prometheus scrape endpoint, with the
/// Work Center, reason and tenant labels. Each test boots an isolated host
/// with <c>Observability__PrometheusEnabled=true</c> (the shared host leaves
/// the scrape endpoint disabled) and uses unique machine ids, because meters
/// are process-wide and accumulate across the suite.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class MesMetersEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Confirm_PostIncrementsCounter_AndRecordsDuration()
    {
        // Arrange
        Environment.SetEnvironmentVariable("Observability__PrometheusEnabled", "true");
        try
        {
            await using var factory = new MesWebApplicationFactory(Fixture.PostgresConnectionString);
            using var client = await SignInAsync(factory);
            var order = await CreateReleasedOrderAsync(client);
            var machineId = Guid.NewGuid();

            // Act
            var confirm = await client.PostAsJsonAsync("/api/production-confirmations", new
            {
                productionOrderId = order.Id,
                machineId,
                reportedByOperatorId = (Guid?)null,
                reportedAt = DateTime.UtcNow,
                goodQuantity = 10m,
                scrapQuantity = 0m,
                notes = (string?)null
            });
            confirm.StatusCode.Should().Be(HttpStatusCode.Created);

            var body = await ScrapeAsync(factory);

            // Assert — counter incremented once with the Work Center label,
            // tenant tag present, duration histogram recorded, HELP/TYPE
            // exposition intact and no 500 from the scrape endpoint.
            body.Should().Contain("# HELP mes_confirmations_total ");
            body.Should().Contain("# TYPE mes_confirmations_total counter");
            var counter = SeriesLines(body, "mes_confirmations_total")
                .Should().ContainSingle(l => l.Contains($"work_center_id=\"{machineId:D}\"")).Subject;
            SampleValue(counter).Should().Be(1);
            TenantLabel(counter).Should().MatchRegex("[0-9a-fA-F-]{36}");

            var duration = SeriesLines(body, "mes_confirmation_duration_seconds_count")
                .Should().ContainSingle(l => l.Contains($"work_center_id=\"{machineId:D}\"")).Subject;
            SampleValue(duration).Should().Be(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Observability__PrometheusEnabled", null);
        }
    }

    [Fact]
    public async Task Scrap_PostIncrementsCounter_WithReasonLabel()
    {
        // Arrange
        Environment.SetEnvironmentVariable("Observability__PrometheusEnabled", "true");
        try
        {
            await using var factory = new MesWebApplicationFactory(Fixture.PostgresConnectionString);
            using var client = await SignInAsync(factory);
            var machineId = Guid.NewGuid();
            var reason = await CreateReasonAsync(client);

            // Act
            var scrap = await client.PostAsJsonAsync("/api/scrap-events", new
            {
                machineId,
                reasonCodeId = reason.Id,
                quantity = 4m,
                reportedAt = DateTime.UtcNow,
                notes = (string?)null,
                reportedByOperatorId = (Guid?)null,
                productionOrderId = (Guid?)null
            });
            scrap.StatusCode.Should().Be(HttpStatusCode.Created);

            var body = await ScrapeAsync(factory);

            // Assert
            body.Should().Contain("# HELP mes_scrap_total ");
            body.Should().Contain("# TYPE mes_scrap_total counter");
            var line = SeriesLines(body, "mes_scrap_total")
                .Should().ContainSingle(l =>
                    l.Contains($"work_center_id=\"{machineId:D}\"")
                    && l.Contains($"reason_code=\"{reason.Code}\"")).Subject;
            SampleValue(line).Should().Be(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Observability__PrometheusEnabled", null);
        }
    }

    [Fact]
    public async Task Downtime_StartIncrementsCounter_WithReasonLabel()
    {
        // Arrange
        Environment.SetEnvironmentVariable("Observability__PrometheusEnabled", "true");
        try
        {
            await using var factory = new MesWebApplicationFactory(Fixture.PostgresConnectionString);
            using var client = await SignInAsync(factory);
            var machineId = Guid.NewGuid();
            var reason = await CreateReasonAsync(client);

            // Act
            var downtime = await client.PostAsJsonAsync("/api/downtime-events", new
            {
                machineId,
                reasonCodeId = reason.Id,
                startedAt = DateTime.UtcNow.AddMinutes(-5),
                notes = (string?)null,
                reportedByOperatorId = (Guid?)null
            });
            downtime.StatusCode.Should().Be(HttpStatusCode.Created);

            var body = await ScrapeAsync(factory);

            // Assert
            body.Should().Contain("# HELP mes_downtime_events_total ");
            body.Should().Contain("# TYPE mes_downtime_events_total counter");
            var line = SeriesLines(body, "mes_downtime_events_total")
                .Should().ContainSingle(l =>
                    l.Contains($"work_center_id=\"{machineId:D}\"")
                    && l.Contains($"reason_code=\"{reason.Code}\"")).Subject;
            SampleValue(line).Should().Be(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Observability__PrometheusEnabled", null);
        }
    }

    [Fact]
    public async Task OeeSnapshot_QueryRecordsDurationHistogram()
    {
        // Arrange
        Environment.SetEnvironmentVariable("Observability__PrometheusEnabled", "true");
        try
        {
            await using var factory = new MesWebApplicationFactory(Fixture.PostgresConnectionString);
            using var client = await SignInAsync(factory);
            var machine = await CreateMachineAsync(client);

            // Act
            var snapshot = await client.GetAsync(
                $"/api/oee/snapshot?machineId={machine.Id}" +
                $"&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}" +
                $"&idealCycleTimeSeconds=60");
            snapshot.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await ScrapeAsync(factory);

            // Assert
            body.Should().Contain("# HELP mes_oee_snapshot_duration_seconds ");
            body.Should().Contain("# TYPE mes_oee_snapshot_duration_seconds histogram");
            var line = SeriesLines(body, "mes_oee_snapshot_duration_seconds_count")
                .Should().ContainSingle(l => l.Contains($"work_center_id=\"{machine.Id:D}\"")).Subject;
            SampleValue(line).Should().Be(1);
            TenantLabel(line).Should().MatchRegex("[0-9a-fA-F-]{36}");
        }
        finally
        {
            Environment.SetEnvironmentVariable("Observability__PrometheusEnabled", null);
        }
    }

    [Fact]
    public async Task MetricsExposition_ContainsAllFiveSeries_WithHelpAndType()
    {
        // Arrange — drive every meter once on a single Work Center, then
        // assert the whole label-documented surface in one scrape.
        Environment.SetEnvironmentVariable("Observability__PrometheusEnabled", "true");
        try
        {
            await using var factory = new MesWebApplicationFactory(Fixture.PostgresConnectionString);
            using var client = await SignInAsync(factory);
            var machine = await CreateMachineAsync(client);
            var reason = await CreateReasonAsync(client);
            var order = await CreateReleasedOrderAsync(client);

            var confirm = await client.PostAsJsonAsync("/api/production-confirmations", new
            {
                productionOrderId = order.Id,
                machineId = machine.Id,
                reportedByOperatorId = (Guid?)null,
                reportedAt = DateTime.UtcNow,
                goodQuantity = 5m,
                scrapQuantity = 0m,
                notes = (string?)null
            });
            confirm.StatusCode.Should().Be(HttpStatusCode.Created);

            var scrap = await client.PostAsJsonAsync("/api/scrap-events", new
            {
                machineId = machine.Id,
                reasonCodeId = reason.Id,
                quantity = 1m,
                reportedAt = DateTime.UtcNow,
                notes = (string?)null,
                reportedByOperatorId = (Guid?)null,
                productionOrderId = (Guid?)null
            });
            scrap.StatusCode.Should().Be(HttpStatusCode.Created);

            var downtime = await client.PostAsJsonAsync("/api/downtime-events", new
            {
                machineId = machine.Id,
                reasonCodeId = reason.Id,
                startedAt = DateTime.UtcNow.AddMinutes(-5),
                notes = (string?)null,
                reportedByOperatorId = (Guid?)null
            });
            downtime.StatusCode.Should().Be(HttpStatusCode.Created);

            var snapshot = await client.GetAsync(
                $"/api/oee/snapshot?machineId={machine.Id}" +
                $"&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}" +
                $"&idealCycleTimeSeconds=60");
            snapshot.StatusCode.Should().Be(HttpStatusCode.OK);

            // Act
            var body = await ScrapeAsync(factory);

            // Assert — all five series with HELP and TYPE, each carrying the
            // Work Center label and no raw lot/serial/operator/token labels.
            body.Should().Contain("# HELP mes_confirmations_total ");
            body.Should().Contain("# TYPE mes_confirmations_total counter");
            body.Should().Contain("# HELP mes_scrap_total ");
            body.Should().Contain("# TYPE mes_scrap_total counter");
            body.Should().Contain("# HELP mes_downtime_events_total ");
            body.Should().Contain("# TYPE mes_downtime_events_total counter");
            body.Should().Contain("# HELP mes_oee_snapshot_duration_seconds ");
            body.Should().Contain("# TYPE mes_oee_snapshot_duration_seconds histogram");
            body.Should().Contain("# HELP mes_confirmation_duration_seconds ");
            body.Should().Contain("# TYPE mes_confirmation_duration_seconds histogram");

            var meterLines = MeterLines(body).ToList();
            meterLines.Should().NotBeEmpty();

            // Bounded label set: only the Work Center / reason / tenant tags
            // (plus the histogram bucket bound) may appear — never lot,
            // serial, operator or token labels. The otel_scope_* keys are
            // standard bounded OpenTelemetry Prometheus-exporter scope
            // labels, not business cardinality leakage.
            var allowedKeys = new HashSet<string>(
                new[] { "work_center_id", "reason_code", "tenant_id", "le", "otel_scope_name", "otel_scope_version" },
                StringComparer.Ordinal);
            foreach (var line in meterLines)
            {
                LabelKeys(line).Should().BeSubsetOf(allowedKeys);
            }

            foreach (var series in new[]
            {
                "mes_confirmations_total",
                "mes_scrap_total",
                "mes_downtime_events_total",
                "mes_oee_snapshot_duration_seconds_count",
                "mes_confirmation_duration_seconds_count"
            })
            {
                SeriesLines(body, series)
                    .Should().ContainSingle(l => l.Contains($"work_center_id=\"{machine.Id:D}\""));
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("Observability__PrometheusEnabled", null);
        }
    }

    [Fact]
    public async Task CrossTenantSnapshotRead_DoesNotIncrementOtherTenantSeries()
    {
        // Arrange — tenant A owns the Work Center and queries it once.
        Environment.SetEnvironmentVariable("Observability__PrometheusEnabled", "true");
        try
        {
            await using var factory = new MesWebApplicationFactory(Fixture.PostgresConnectionString);
            using var clientA = await SignInAsync(factory);
            var machine = await CreateMachineAsync(clientA);
            var first = await clientA.GetAsync(
                $"/api/oee/snapshot?machineId={machine.Id}" +
                $"&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}" +
                $"&idealCycleTimeSeconds=60");
            first.StatusCode.Should().Be(HttpStatusCode.OK);

            // Act — tenant B reads across the boundary: 404, and meters must
            // stay silent for the other tenant.
            var (emailB, passwordB) = await Fixture.CreateTenantAsync();
            var (clientB, tenantB) = await SignInWithTenantAsync(factory, emailB, passwordB);
            using (clientB)
            {
                var crossTenant = await clientB.GetAsync(
                    $"/api/oee/snapshot?machineId={machine.Id}" +
                    $"&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}" +
                    $"&idealCycleTimeSeconds=60");
                crossTenant.StatusCode.Should().Be(HttpStatusCode.NotFound);
            }

            var body = await ScrapeAsync(factory);

            // Assert — the failed cross-tenant read added no sample: the
            // Work Center series still counts exactly tenant A's query, and
            // no histogram sample carries tenant B's id.
            var line = SeriesLines(body, "mes_oee_snapshot_duration_seconds_count")
                .Should().ContainSingle(l => l.Contains($"work_center_id=\"{machine.Id:D}\"")).Subject;
            SampleValue(line).Should().Be(1);
            SeriesLines(body, "mes_oee_snapshot_duration_seconds_count")
                .Should().NotContain(l => l.Contains($"tenant_id=\"{tenantB}\""));
        }
        finally
        {
            Environment.SetEnvironmentVariable("Observability__PrometheusEnabled", null);
        }
    }

    /// <summary>Scrapes <c>/metrics</c> from a Prometheus-enabled host.</summary>
    private static async Task<string> ScrapeAsync(MesWebApplicationFactory factory)
    {
        using var scraper = factory.CreateClient();
        var response = await scraper.GetAsync("/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");
        return await response.Content.ReadAsStringAsync();
    }

    private static IReadOnlyList<string> SeriesLines(string body, string series) =>
        body.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.StartsWith(series + "{", StringComparison.Ordinal))
            .ToList();

    private static IEnumerable<string> MeterLines(string body) =>
        body.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.StartsWith("mes_", StringComparison.Ordinal) && l.Contains('{'));

    /// <summary>
    /// Label keys of one exposition sample. Reads up to the first closing
    /// brace so a trailing trace exemplar (<c># {trace_id=…}</c>) is ignored.
    /// </summary>
    private static IEnumerable<string> LabelKeys(string line)
    {
        var start = line.IndexOf('{');
        var end = line.IndexOf('}');
        var labels = line.Substring(start + 1, end - start - 1);
        return System.Text.RegularExpressions.Regex
            .Matches(labels, "(\\w+)=\"[^\"]*\"")
            .Select(m => m.Groups[1].Value);
    }

    private static double SampleValue(string line) =>
        double.Parse(line[(line.LastIndexOf(' ') + 1)..], System.Globalization.CultureInfo.InvariantCulture);

    private static string TenantLabel(string line)
    {
        const string marker = "tenant_id=\"";
        var start = line.IndexOf(marker, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);
        var end = line.IndexOf('"', start + marker.Length);
        return line.Substring(start + marker.Length, end - start - marker.Length);
    }

    private static string Qs(DateTime value) => Uri.EscapeDataString(value.ToString("O"));

    private static async Task<HttpClient> SignInAsync(
        MesWebApplicationFactory factory,
        string? email = null,
        string? password = null)
    {
        var (client, _) = await SignInWithTenantAsync(
            factory,
            email ?? IntegrationTestData.AdminEmail,
            password ?? IntegrationTestData.AdminPassword);
        return client;
    }

    private static async Task<(HttpClient Client, string TenantId)> SignInWithTenantAsync(
        MesWebApplicationFactory factory, string email, string password)
    {
        var client = factory.CreateClient();
        using var signIn = await client.PostAsJsonAsync(
            "/api/auth/sign-in", new { email, password });
        signIn.EnsureSuccessStatusCode();

        var accessToken = AuthCookieHelper.GetAccessToken(signIn);
        accessToken.Should().NotBeNullOrWhiteSpace();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        return (client, ReadClaim(accessToken!, "tenant_id"));
    }

    private static string ReadClaim(string jwt, string claimType)
        => ReadAllClaims(jwt).First(c => c.Type == claimType).Value;

    private static List<Claim> ReadAllClaims(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var json = Encoding.UTF8.GetString(Base64UrlDecode(payload));
        var result = new List<Claim>();
        using var doc = JsonDocument.Parse(json);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                {
                    result.Add(new Claim(property.Name, item.GetString() ?? string.Empty));
                }
            }
            else
            {
                result.Add(new Claim(property.Name, property.Value.ToString()));
            }
        }

        return result;
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }

    private static async Task<MachineDto> CreateMachineAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/machines", new
        {
            code = $"MET-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            name = "Meter Work Center",
            description = (string?)null,
            departmentId = (Guid?)null,
            isActive = true
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<MachineDto>(response);
    }

    private static async Task<ReasonCodeDto> CreateReasonAsync(HttpClient client)
    {
        var code = $"MET-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var response = await client.PostAsJsonAsync("/api/reason-codes", new
        {
            code,
            name = code,
            description = (string?)null,
            category = (short)1,
            isActive = true,
            sortIndex = 0
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<ReasonCodeDto>(response);
    }

    private static string UniqueCode() => $"PO-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private static async Task<ProductionOrderDto> CreateReleasedOrderAsync(HttpClient client)
    {
        var recipe = await CreateRecipeAsync(client);
        var versionId = recipe.Versions.Single().Id;

        var operationResponse = await client.PostAsJsonAsync("/api/operations", new
        {
            versionId,
            code = $"OP-{Guid.NewGuid():N}"[..8],
            name = "Cutting",
            description = (string?)null,
            operationType = (string?)null,
            sortIndex = 0,
            setupTimeMinutes = (decimal?)null,
            runTimeMode = 1,
            runTimePerUnitSeconds = 10m,
            runTimePerBatchMinutes = (decimal?)null,
            teardownTimeMinutes = (decimal?)null,
            queueTimeMinutes = (decimal?)null,
            isOptional = false,
            allowParallelExecution = false,
            expectedQuantity = (decimal?)null
        });
        operationResponse.EnsureSuccessStatusCode();

        var releaseVersionResponse = await client.PostAsync($"/api/recipe-versions/{versionId}/release", null);
        releaseVersionResponse.EnsureSuccessStatusCode();

        var order = await CreateOrderAsync(client, recipe.Id, versionId);

        var releaseOrderResponse = await client.PostAsync($"/api/production-orders/{order.Id}/release", null);
        releaseOrderResponse.EnsureSuccessStatusCode();
        return await ReadAsync<ProductionOrderDto>(releaseOrderResponse);
    }

    private static async Task<ProductionOrderDto> CreateOrderAsync(
        HttpClient client, Guid? recipeId = null, Guid? recipeVersionId = null)
    {
        var response = await client.PostAsJsonAsync("/api/production-orders", new
        {
            code = UniqueCode(),
            productId = Guid.NewGuid(),
            recipeId = recipeId ?? Guid.NewGuid(),
            recipeVersionId = recipeVersionId ?? Guid.NewGuid(),
            plannedQuantity = 100m,
            measureUnitId = (Guid?)null,
            priority = 0,
            dueDate = (DateTime?)null,
            notes = (string?)null,
            syncId = (string?)null
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<ProductionOrderDto>(response);
    }

    private static async Task<RecipeDto> CreateRecipeAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/recipes", new
        {
            code = $"R-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            name = "Integration recipe",
            description = (string?)null,
            isActive = true,
            primaryProductId = (Guid?)null,
            syncId = (string?)null
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<RecipeDto>(response);
    }
}
