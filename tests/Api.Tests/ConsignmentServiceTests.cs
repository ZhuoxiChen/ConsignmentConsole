using Api.Controllers;
using Api.Data;
using Api.Domain;
using Api.Middleware;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Api.Tests;

public sealed class ConsignmentServiceTests
{
    [Theory]
    [InlineData("booked", "picked_up")]
    [InlineData("booked", "cancelled")]
    [InlineData("picked_up", "in_transit")]
    [InlineData("in_transit", "out_for_delivery")]
    [InlineData("out_for_delivery", "delivered")]
    [InlineData("out_for_delivery", "failed_delivery")]
    [InlineData("failed_delivery", "out_for_delivery")]
    [InlineData("failed_delivery", "returned")]
    public async Task Allows_each_valid_transition_and_increments_version_once(string from, string to)
    {
        await using var db = CreateContext();
        var (tenant, consignment) = await SeedAsync(db, from);
        var result = await CreateService(db).UpdateAsync(
            tenant.Id,
            consignment.Id,
            CreateRequest(to, 1));

        Assert.Equal(to, result.Status);
        Assert.Equal(2, result.Version);
        var history = await db.StatusHistory.SingleAsync();
        Assert.Equal(tenant.Id, history.TenantId);
        Assert.Equal(from, history.FromStatus);
        Assert.Equal(to, history.ToStatus);
    }

    [Theory]
    [InlineData("booked", "delivered")]
    [InlineData("picked_up", "booked")]
    [InlineData("delivered", "booked")]
    [InlineData("returned", "out_for_delivery")]
    [InlineData("cancelled", "picked_up")]
    [InlineData("failed_delivery", "delivered")]
    public async Task Rejects_invalid_transition_without_changing_state(string from, string to)
    {
        await using var db = CreateContext();
        var (tenant, consignment) = await SeedAsync(db, from);
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => CreateService(db).UpdateAsync(tenant.Id, consignment.Id, CreateRequest(to, 1)));

        Assert.Equal("INVALID_TRANSITION", exception.Error.Code);
        var stored = await db.Consignments.SingleAsync();
        Assert.Equal(from, stored.Status);
        Assert.Equal(1, stored.Version);
        Assert.Empty(await db.StatusHistory.ToListAsync());
    }

    [Fact]
    public async Task Rejects_delivered_without_signer_or_delivery_time()
    {
        await using var db = CreateContext();
        var (tenant, consignment) = await SeedAsync(db, "out_for_delivery");

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => CreateService(db).UpdateAsync(
                tenant.Id,
                consignment.Id,
                new StatusUpdateRequest("delivered", 1, null, null, null)));

        Assert.Equal("VALIDATION", exception.Error.Code);
        Assert.Contains("signedBy", exception.Error.Errors!.Keys);
        Assert.Contains("deliveryTime", exception.Error.Errors.Keys);
        await AssertNoUpdate(db, "out_for_delivery", 1);
    }

    [Fact]
    public async Task Rejects_failed_delivery_without_reason()
    {
        await using var db = CreateContext();
        var (tenant, consignment) = await SeedAsync(db, "out_for_delivery");

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => CreateService(db).UpdateAsync(
                tenant.Id,
                consignment.Id,
                new StatusUpdateRequest("failed_delivery", 1, null, null, null)));

        Assert.Contains("failureReason", exception.Error.Errors!.Keys);
        await AssertNoUpdate(db, "out_for_delivery", 1);
    }

    [Fact]
    public async Task Rejects_unknown_failed_delivery_reason()
    {
        await using var db = CreateContext();
        var (tenant, consignment) = await SeedAsync(db, "out_for_delivery");

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => CreateService(db).UpdateAsync(
                tenant.Id,
                consignment.Id,
                new StatusUpdateRequest("failed_delivery", 1, null, null, "other")));

        Assert.Contains("failureReason", exception.Error.Errors!.Keys);
        await AssertNoUpdate(db, "out_for_delivery", 1);
    }

    [Fact]
    public async Task Tenant_queries_do_not_expose_another_tenants_id_or_number()
    {
        await using var db = CreateContext();
        var (tenantB, consignmentB) = await SeedAsync(db, "booked", "CN-TENANT-B", "tenant-b");
        var tenantA = new Tenant { Id = Guid.NewGuid(), Code = "tenant-a", Name = "Tenant A" };
        db.Tenants.Add(tenantA);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(tenantA.Id, consignmentB.Id));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateAsync(tenantA.Id, consignmentB.Id, CreateRequest("picked_up", 1)));
        var search = await service.ListAsync(tenantA.Id, null, "CN-TENANT-B", 1, 20);
        Assert.Equal(0, search.Total);
        Assert.Empty(search.Items);
        Assert.Equal(tenantB.Id, consignmentB.TenantId);
    }

    [Fact]
    public async Task List_filters_searches_and_pages_only_within_the_tenant()
    {
        await using var db = CreateContext();
        var tenant = new Tenant { Id = Guid.NewGuid(), Code = "tenant-a", Name = "Tenant A" };
        var first = CreateConsignment(tenant, "booked", "CN-1");
        var second = CreateConsignment(tenant, "booked", "CN-2");
        var third = CreateConsignment(tenant, "in_transit", "CN-3");
        db.Consignments.AddRange(first, second, third);
        await db.SaveChangesAsync();
        first.CreatedAt = new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero);
        second.CreatedAt = new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero);
        third.ReceiverName = "Other Receiver";
        await db.SaveChangesAsync();

        var firstPage = await CreateService(db).ListAsync(tenant.Id, "booked", "Receiver", 1, 1);
        var secondPage = await CreateService(db).ListAsync(tenant.Id, "booked", "Receiver", 2, 1);

        Assert.Equal(2, firstPage.Total);
        Assert.Single(firstPage.Items);
        Assert.Equal("CN-2", firstPage.Items[0].ConsignmentNumber);
        Assert.Equal(2, secondPage.Total);
        Assert.Single(secondPage.Items);
        Assert.Equal("CN-1", secondPage.Items[0].ConsignmentNumber);
    }

    [Fact]
    public async Task Detail_includes_tenant_scoped_status_history()
    {
        await using var db = CreateContext();
        var (tenant, consignment) = await SeedAsync(db, "picked_up");
        await CreateService(db).UpdateAsync(tenant.Id, consignment.Id, CreateRequest("in_transit", 1));

        var detail = await CreateService(db).GetAsync(tenant.Id, consignment.Id);

        Assert.Single(detail.History);
        Assert.Equal(tenant.Id, detail.History[0].TenantId);
        Assert.Equal("picked_up", detail.History[0].FromStatus);
        Assert.Equal("in_transit", detail.History[0].ToStatus);
    }

    [Fact]
    public async Task Missing_and_unknown_tenant_headers_return_expected_statuses()
    {
        await using var db = CreateContext();

        var missingHeader = await InvokeListWithTenantHeaderAsync(db, null);
        var unknownHeader = await InvokeListWithTenantHeaderAsync(db, "not-a-tenant");

        Assert.Equal(StatusCodes.Status400BadRequest, missingHeader.Response.StatusCode);
        Assert.Equal("TENANT_REQUIRED", await ReadErrorCodeAsync(missingHeader));
        Assert.Equal(StatusCodes.Status404NotFound, unknownHeader.Response.StatusCode);
        Assert.Equal("NOT_FOUND", await ReadErrorCodeAsync(unknownHeader));
    }

    [Fact]
    public async Task Stale_update_returns_latest_state_without_mutating_or_adding_history()
    {
        await using var db = CreateContext();
        var (tenant, consignment) = await SeedAsync(db, "picked_up");
        var service = CreateService(db);
        await service.UpdateAsync(tenant.Id, consignment.Id, CreateRequest("in_transit", 1));

        var response = await InvokeStatusUpdateAsync(
            db,
            tenant.Code,
            consignment.Id,
            CreateRequest("out_for_delivery", 1));

        Assert.Equal(StatusCodes.Status409Conflict, response.Response.StatusCode);
        response.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(response.Response.Body);
        var latest = document.RootElement.GetProperty("latest");
        Assert.Equal("CONFLICT", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(2, latest.GetProperty("version").GetInt32());
        Assert.Equal("in_transit", latest.GetProperty("status").GetString());
        var stored = await db.Consignments.AsNoTracking().SingleAsync();
        Assert.Equal("in_transit", stored.Status);
        Assert.Equal(2, stored.Version);
        Assert.Single(await db.StatusHistory.AsNoTracking().ToListAsync());
    }

    private static async Task<(Tenant Tenant, Consignment Consignment)> SeedAsync(
        ConsignmentDbContext db,
        string status,
        string number = "CN-1",
        string tenantCode = "tenant-a")
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Code = tenantCode, Name = tenantCode };
        var consignment = CreateConsignment(tenant, status, number);
        db.Consignments.Add(consignment);
        await db.SaveChangesAsync();
        return (tenant, consignment);
    }

    private static Consignment CreateConsignment(Tenant tenant, string status, string number) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Tenant = tenant,
            ConsignmentNumber = number,
            CustomerName = "Customer",
            ReceiverName = "Receiver",
            ReceiverSuburb = "Sydney",
            ReceiverState = "NSW",
            ReceiverPostcode = "2000",
            ItemCount = 1,
            WeightKg = 1,
            ServiceType = "standard",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow
        };

    private static StatusUpdateRequest CreateRequest(string status, int version) =>
        new(
            status,
            version,
            status == "delivered" ? "Pat Taylor" : null,
            status == "delivered" ? DateTimeOffset.UtcNow : null,
            status == "failed_delivery" ? "no_one_home" : null);

    private static ConsignmentService CreateService(ConsignmentDbContext db) => new(db);

    private static ConsignmentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ConsignmentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ConsignmentDbContext(options);
    }

    private static async Task AssertNoUpdate(ConsignmentDbContext db, string status, int version)
    {
        var stored = await db.Consignments.SingleAsync();
        Assert.Equal(status, stored.Status);
        Assert.Equal(version, stored.Version);
        Assert.Empty(await db.StatusHistory.ToListAsync());
    }

    private static async Task<DefaultHttpContext> InvokeListWithTenantHeaderAsync(
        ConsignmentDbContext db,
        string? tenantHeader)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        if (tenantHeader is not null)
        {
            context.Request.Headers["X-Tenant"] = tenantHeader;
        }

        var controller = new ConsignmentsController(CreateService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
        var middleware = new ApiExceptionMiddleware(
            async _ => { await controller.List(null, null, 1, 20); },
            NullLogger<ApiExceptionMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        return context;
    }

    private static async Task<DefaultHttpContext> InvokeStatusUpdateAsync(
        ConsignmentDbContext db,
        string tenantHeader,
        Guid id,
        StatusUpdateRequest request)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers["X-Tenant"] = tenantHeader;
        var controller = new ConsignmentsController(CreateService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
        var middleware = new ApiExceptionMiddleware(
            async _ =>
            {
                await controller.UpdateStatus(
                    id,
                    new UpdateStatusDto(
                        request.Status,
                        request.Version,
                        request.SignedBy,
                        request.DeliveryTime,
                        request.FailureReason));
            },
            NullLogger<ApiExceptionMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        return context;
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement.GetProperty("code").GetString();
    }
}
