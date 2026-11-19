using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialBackendFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                    table.CheckConstraint("ck_tenants_code_nonblank", "length(btrim(code)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "consignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consignment_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    receiver_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    receiver_suburb = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    receiver_state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    receiver_postcode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    item_count = table.Column<int>(type: "integer", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    service_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consignments", x => x.id);
                    table.UniqueConstraint("ak_consignments_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_consignments_item_count_nonnegative", "item_count >= 0");
                    table.CheckConstraint("ck_consignments_required_text_nonblank", "length(btrim(consignment_number)) > 0 AND length(btrim(customer_name)) > 0 AND length(btrim(receiver_name)) > 0 AND length(btrim(receiver_suburb)) > 0 AND length(btrim(receiver_state)) > 0 AND length(btrim(receiver_postcode)) > 0 AND length(btrim(service_type)) > 0");
                    table.CheckConstraint("ck_consignments_status_valid", "status IN ('booked', 'picked_up', 'in_transit', 'out_for_delivery', 'delivered', 'failed_delivery', 'returned', 'cancelled')");
                    table.CheckConstraint("ck_consignments_version_positive", "version >= 1");
                    table.CheckConstraint("ck_consignments_weight_nonnegative", "weight_kg >= 0");
                    table.ForeignKey(
                        name: "fk_consignments_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    signed_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    delivery_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_status_history", x => x.id);
                    table.CheckConstraint("ck_status_history_delivery_fields_paired", "(signed_by IS NULL) = (delivery_time IS NULL)");
                    table.CheckConstraint("ck_status_history_failure_reason_valid", "failure_reason IS NULL OR (to_status = 'failed_delivery' AND failure_reason IN ('no_one_home', 'address_incorrect', 'refused', 'access_issue'))");
                    table.CheckConstraint("ck_status_history_outcome_details", "(to_status = 'delivered' AND from_status = 'out_for_delivery' AND signed_by IS NOT NULL AND length(btrim(signed_by)) > 0 AND delivery_time IS NOT NULL AND failure_reason IS NULL) OR (to_status = 'failed_delivery' AND from_status = 'out_for_delivery' AND signed_by IS NULL AND delivery_time IS NULL AND failure_reason IS NOT NULL) OR (to_status NOT IN ('delivered', 'failed_delivery') AND signed_by IS NULL AND delivery_time IS NULL AND failure_reason IS NULL)");
                    table.CheckConstraint("ck_status_history_statuses_valid", "from_status IN ('booked', 'picked_up', 'in_transit', 'out_for_delivery', 'delivered', 'failed_delivery', 'returned', 'cancelled') AND to_status IN ('booked', 'picked_up', 'in_transit', 'out_for_delivery', 'delivered', 'failed_delivery', 'returned', 'cancelled')");
                    table.CheckConstraint("ck_status_history_transition_valid", "(from_status = 'booked' AND to_status IN ('picked_up', 'cancelled')) OR (from_status = 'picked_up' AND to_status = 'in_transit') OR (from_status = 'in_transit' AND to_status = 'out_for_delivery') OR (from_status = 'out_for_delivery' AND to_status IN ('delivered', 'failed_delivery')) OR (from_status = 'failed_delivery' AND to_status IN ('out_for_delivery', 'returned'))");
                    table.ForeignKey(
                        name: "fk_status_history_consignments_tenant_id_consignment_id",
                        columns: x => new { x.tenant_id, x.consignment_id },
                        principalTable: "consignments",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consignments_tenant_status_created_at",
                table: "consignments",
                columns: new[] { "tenant_id", "status", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ux_consignments_tenant_number",
                table: "consignments",
                columns: new[] { "tenant_id", "consignment_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_status_history_consignment_changed_at",
                table: "status_history",
                columns: new[] { "consignment_id", "changed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_status_history_tenant_consignment_changed_at",
                table: "status_history",
                columns: new[] { "tenant_id", "consignment_id", "changed_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ux_tenants_code",
                table: "tenants",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "status_history");

            migrationBuilder.DropTable(
                name: "consignments");

            migrationBuilder.DropTable(
                name: "tenants");
        }
    }
}
