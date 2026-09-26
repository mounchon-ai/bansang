using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bansang.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_locations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_counts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    threshold_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_counts", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_counts_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "skus",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    spec = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    base_unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tracking_pattern = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    avg_cost_per_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skus", x => x.id);
                    table.ForeignKey(
                        name: "fk_skus_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "price_rule",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    min_qty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_rule", x => x.id);
                    table.ForeignKey(
                        name: "fk_price_rule_skus_sku_id",
                        column: x => x.sku_id,
                        principalTable: "skus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sku_units",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    factor_to_base = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    is_exact = table.Column<bool>(type: "boolean", nullable: false),
                    form = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    barcode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    can_sell = table.Column<bool>(type: "boolean", nullable: false),
                    can_buy = table.Column<bool>(type: "boolean", nullable: false),
                    is_base = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sku_units", x => x.id);
                    table.ForeignKey(
                        name: "fk_sku_units_skus_sku_id",
                        column: x => x.sku_id,
                        principalTable: "skus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_buckets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    form = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    qty_on_hand_base = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    qty_reserved_base = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    needs_count = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_buckets", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_buckets_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_buckets_skus_sku_id",
                        column: x => x.sku_id,
                        principalTable: "skus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_count_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    count_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    form = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    system_qty_base = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    counted_qty_base = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    counted_unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    counted_input_qty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    variance_base = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    variance_percent = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    counted_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    counted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    adjustment_movement_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_count_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_count_lines_skus_sku_id",
                        column: x => x.sku_id,
                        principalTable: "skus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_count_lines_stock_counts_count_id",
                        column: x => x.count_id,
                        principalTable: "stock_counts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bucket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    form = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    qty_base = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    input_unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    input_qty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    factor_snapshot = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: true),
                    is_approximate = table.Column<bool>(type: "boolean", nullable: false),
                    cost_per_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ref_document = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reversal_of_id = table.Column<Guid>(type: "uuid", nullable: true),
                    balance_after_base = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_movements", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_movements_stock_buckets_bucket_id",
                        column: x => x.bucket_id,
                        principalTable: "stock_buckets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_locations_code",
                table: "locations",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_price_rule_sku_id_unit_name_tier_min_qty",
                table: "price_rule",
                columns: new[] { "sku_id", "unit_name", "tier", "min_qty" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_name",
                table: "products",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_sku_units_barcode",
                table: "sku_units",
                column: "barcode",
                unique: true,
                filter: "barcode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sku_units_sku_id_unit_name",
                table: "sku_units",
                columns: new[] { "sku_id", "unit_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skus_code",
                table: "skus",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skus_product_id",
                table: "skus",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_buckets_location_id_needs_count",
                table: "stock_buckets",
                columns: new[] { "location_id", "needs_count" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_buckets_sku_id_location_id_form",
                table: "stock_buckets",
                columns: new[] { "sku_id", "location_id", "form" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_count_lines_count_id",
                table: "stock_count_lines",
                column: "count_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_count_lines_sku_id_counted_at",
                table: "stock_count_lines",
                columns: new[] { "sku_id", "counted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_counts_location_id_created_at",
                table: "stock_counts",
                columns: new[] { "location_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_bucket_id",
                table: "stock_movements",
                column: "bucket_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_correlation_id",
                table: "stock_movements",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_ref_document",
                table: "stock_movements",
                column: "ref_document");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_reversal_of_id",
                table: "stock_movements",
                column: "reversal_of_id",
                unique: true,
                filter: "reversal_of_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_sku_id_at",
                table: "stock_movements",
                columns: new[] { "sku_id", "at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "price_rule");

            migrationBuilder.DropTable(
                name: "sku_units");

            migrationBuilder.DropTable(
                name: "stock_count_lines");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropTable(
                name: "stock_counts");

            migrationBuilder.DropTable(
                name: "stock_buckets");

            migrationBuilder.DropTable(
                name: "locations");

            migrationBuilder.DropTable(
                name: "skus");

            migrationBuilder.DropTable(
                name: "products");
        }
    }
}
