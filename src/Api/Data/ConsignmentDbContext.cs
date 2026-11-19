using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed class ConsignmentDbContext(DbContextOptions<ConsignmentDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Consignment> Consignments => Set<Consignment>();
    public DbSet<StatusHistory> StatusHistory => Set<StatusHistory>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Tenant>(e =>
        {
            e.ToTable("tenants", table =>
                table.HasCheckConstraint("ck_tenants_code_nonblank", "length(btrim(code)) > 0"));
            e.HasKey(x => x.Id).HasName("pk_tenants");
            e.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_tenants_code");
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid");
            e.Property(x => x.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        });

        model.Entity<Consignment>(e =>
        {
            e.ToTable("consignments", table =>
            {
                table.HasCheckConstraint("ck_consignments_item_count_nonnegative", "item_count >= 0");
                table.HasCheckConstraint("ck_consignments_weight_nonnegative", "weight_kg >= 0");
                table.HasCheckConstraint("ck_consignments_version_positive", "version >= 1");
                table.HasCheckConstraint(
                    "ck_consignments_status_valid",
                    "status IN ('booked', 'picked_up', 'in_transit', 'out_for_delivery', 'delivered', 'failed_delivery', 'returned', 'cancelled')");
                table.HasCheckConstraint(
                    "ck_consignments_required_text_nonblank",
                    "length(btrim(consignment_number)) > 0 AND length(btrim(customer_name)) > 0 AND " +
                    "length(btrim(receiver_name)) > 0 AND length(btrim(receiver_suburb)) > 0 AND " +
                    "length(btrim(receiver_state)) > 0 AND length(btrim(receiver_postcode)) > 0 AND " +
                    "length(btrim(service_type)) > 0");
            });
            e.HasKey(x => x.Id).HasName("pk_consignments");
            e.HasAlternateKey(x => new { x.TenantId, x.Id }).HasName("ak_consignments_tenant_id_id");
            e.HasIndex(x => new { x.TenantId, x.ConsignmentNumber })
                .IsUnique()
                .HasDatabaseName("ux_consignments_tenant_number");
            e.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt })
                .IsDescending(false, false, true)
                .HasDatabaseName("ix_consignments_tenant_status_created_at");

            e.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsConcurrencyToken();
            e.Property(x => x.ConsignmentNumber).HasColumnName("consignment_number").HasMaxLength(100).IsRequired();
            e.Property(x => x.CustomerName).HasColumnName("customer_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.ReceiverName).HasColumnName("receiver_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.ReceiverSuburb).HasColumnName("receiver_suburb").HasMaxLength(200).IsRequired();
            e.Property(x => x.ReceiverState).HasColumnName("receiver_state").HasMaxLength(100).IsRequired();
            e.Property(x => x.ReceiverPostcode).HasColumnName("receiver_postcode").HasMaxLength(20).IsRequired();
            e.Property(x => x.ItemCount).HasColumnName("item_count").HasColumnType("integer");
            e.Property(x => x.WeightKg).HasColumnName("weight_kg").HasPrecision(10, 2);
            e.Property(x => x.ServiceType).HasColumnName("service_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            e.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1).IsConcurrencyToken();
            e.HasOne(x => x.Tenant)
                .WithMany(x => x.Consignments)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_consignments_tenants_tenant_id");
        });

        model.Entity<StatusHistory>(e =>
        {
            e.ToTable("status_history", table =>
            {
                table.HasCheckConstraint(
                    "ck_status_history_statuses_valid",
                    "from_status IN ('booked', 'picked_up', 'in_transit', 'out_for_delivery', 'delivered', 'failed_delivery', 'returned', 'cancelled') AND " +
                    "to_status IN ('booked', 'picked_up', 'in_transit', 'out_for_delivery', 'delivered', 'failed_delivery', 'returned', 'cancelled')");
                table.HasCheckConstraint(
                    "ck_status_history_transition_valid",
                    "(from_status = 'booked' AND to_status IN ('picked_up', 'cancelled')) OR " +
                    "(from_status = 'picked_up' AND to_status = 'in_transit') OR " +
                    "(from_status = 'in_transit' AND to_status = 'out_for_delivery') OR " +
                    "(from_status = 'out_for_delivery' AND to_status IN ('delivered', 'failed_delivery')) OR " +
                    "(from_status = 'failed_delivery' AND to_status IN ('out_for_delivery', 'returned'))");
                table.HasCheckConstraint(
                    "ck_status_history_failure_reason_valid",
                    "failure_reason IS NULL OR (to_status = 'failed_delivery' AND failure_reason IN ('no_one_home', 'address_incorrect', 'refused', 'access_issue'))");
                table.HasCheckConstraint(
                    "ck_status_history_delivery_fields_paired",
                    "(signed_by IS NULL) = (delivery_time IS NULL)");
                table.HasCheckConstraint(
                    "ck_status_history_outcome_details",
                    "(to_status = 'delivered' AND from_status = 'out_for_delivery' AND " +
                    "signed_by IS NOT NULL AND length(btrim(signed_by)) > 0 AND delivery_time IS NOT NULL AND failure_reason IS NULL) OR " +
                    "(to_status = 'failed_delivery' AND from_status = 'out_for_delivery' AND " +
                    "signed_by IS NULL AND delivery_time IS NULL AND failure_reason IS NOT NULL) OR " +
                    "(to_status NOT IN ('delivered', 'failed_delivery') AND signed_by IS NULL AND " +
                    "delivery_time IS NULL AND failure_reason IS NULL)");
            });
            e.HasKey(x => x.Id).HasName("pk_status_history");
            e.HasIndex(x => new { x.ConsignmentId, x.ChangedAt })
                .IsDescending(false, true)
                .HasDatabaseName("ix_status_history_consignment_changed_at");
            e.HasIndex(x => new { x.TenantId, x.ConsignmentId, x.ChangedAt })
                .IsDescending(false, false, true)
                .HasDatabaseName("ix_status_history_tenant_consignment_changed_at");

            e.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            e.Property(x => x.ConsignmentId).HasColumnName("consignment_id").HasColumnType("uuid");
            e.Property(x => x.FromStatus).HasColumnName("from_status").HasMaxLength(30).IsRequired();
            e.Property(x => x.ToStatus).HasColumnName("to_status").HasMaxLength(30).IsRequired();
            e.Property(x => x.ChangedAt).HasColumnName("changed_at").HasColumnType("timestamp with time zone");
            e.Property(x => x.SignedBy).HasColumnName("signed_by").HasMaxLength(200);
            e.Property(x => x.DeliveryTime).HasColumnName("delivery_time").HasColumnType("timestamp with time zone");
            e.Property(x => x.FailureReason).HasColumnName("failure_reason").HasMaxLength(40);
            e.HasOne(x => x.Consignment)
                .WithMany(x => x.History)
                .HasForeignKey(x => new { x.TenantId, x.ConsignmentId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_status_history_consignments_tenant_id_consignment_id");
        });
    }
}
