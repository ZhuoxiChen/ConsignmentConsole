using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

public sealed class DatabaseModelTests
{
    [Fact]
    public void StatusHistoryUsesCompositeTenantConsignmentForeignKey()
    {
        using var db = CreateContext();
        var history = db.Model.FindEntityType(typeof(StatusHistory))!;
        var foreignKey = Assert.Single(history.GetForeignKeys());

        Assert.Equal(
            new[] { nameof(StatusHistory.TenantId), nameof(StatusHistory.ConsignmentId) },
            foreignKey.Properties.Select(property => property.Name));
        Assert.Equal(
            new[] { nameof(Consignment.TenantId), nameof(Consignment.Id) },
            foreignKey.PrincipalKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void ConsignmentVersionAndTenantScopedUniqueIndexAreConfigured()
    {
        using var db = CreateContext();
        var consignment = db.Model.FindEntityType(typeof(Consignment))!;
        var version = consignment.FindProperty(nameof(Consignment.Version))!;
        var tenantId = consignment.FindProperty(nameof(Consignment.TenantId))!;
        var tenantNumberIndex = Assert.Single(consignment.GetIndexes().Where(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(Consignment.TenantId), nameof(Consignment.ConsignmentNumber) })));

        Assert.Equal(typeof(int), version.ClrType);
        Assert.Equal(1, version.GetDefaultValue());
        Assert.True(version.IsConcurrencyToken);
        Assert.True(tenantId.IsConcurrencyToken);
        Assert.NotNull(tenantNumberIndex);
    }

    private static ConsignmentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ConsignmentDbContext>()
            .UseNpgsql("Host=localhost;Database=model_tests;Username=postgres;Password=postgres")
            .Options;
        return new ConsignmentDbContext(options);
    }
}
