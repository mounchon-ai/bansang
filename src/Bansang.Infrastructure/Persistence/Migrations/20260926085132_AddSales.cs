using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bansang.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    tax_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_sequences",
                columns: table => new
                {
                    prefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    period = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    last_value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_sequences", x => new { x.prefix, x.period });
                });

            migrationBuilder.CreateTable(
                name: "quotations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    bill_discount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_rate_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sales_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quotations", x => x.id);
                    table.ForeignKey(
                        name: "fk_quotations_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    bill_discount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_rate_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    change_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    quotation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_orders", x => x.id);
                    table.ForeignKey(
                        name: "fk_sales_orders_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_orders_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quotation_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quotation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sku_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    qty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    line_discount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quotation_line", x => x.id);
                    table.ForeignKey(
                        name: "fk_quotation_line_quotations_quotation_id",
                        column: x => x.quotation_id,
                        principalTable: "quotations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_quotation_line_skus_sku_id",
                        column: x => x.sku_id,
                        principalTable: "skus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fulfillment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fulfillment", x => x.id);
                    table.ForeignKey(
                        name: "fk_fulfillment_sales_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_refund = table.Column<bool>(type: "boolean", nullable: false),
                    return_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment", x => x.id);
                    table.ForeignKey(
                        name: "fk_payment_sales_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sku_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    qty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    price_source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    line_discount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tracks_stock = table.Column<bool>(type: "boolean", nullable: false),
                    qty_base = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    factor_snapshot = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: true),
                    is_approximate = table.Column<bool>(type: "boolean", nullable: false),
                    form = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    fulfilled_qty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    fulfilled_base = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    returned_qty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    returned_base = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    refunded_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_line", x => x.id);
                    table.ForeignKey(
                        name: "fk_sales_line_sales_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sales_line_skus_sku_id",
                        column: x => x.sku_id,
                        principalTable: "skus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_return",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_return", x => x.id);
                    table.ForeignKey(
                        name: "fk_sales_return_sales_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fulfillment_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    qty_base = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fulfillment_line", x => x.id);
                    table.ForeignKey(
                        name: "fk_fulfillment_line_fulfillment_fulfillment_id",
                        column: x => x.fulfillment_id,
                        principalTable: "fulfillment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_fulfillment_line_sales_line_sales_line_id",
                        column: x => x.sales_line_id,
                        principalTable: "sales_line",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_return_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    qty_base = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_return_line", x => x.id);
                    table.ForeignKey(
                        name: "fk_sales_return_line_sales_line_sales_line_id",
                        column: x => x.sales_line_id,
                        principalTable: "sales_line",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_return_line_sales_return_return_id",
                        column: x => x.return_id,
                        principalTable: "sales_return",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customers_code",
                table: "customers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customers_name",
                table: "customers",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_customers_phone",
                table: "customers",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "ix_fulfillment_number",
                table: "fulfillment",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fulfillment_order_id",
                table: "fulfillment",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_fulfillment_line_fulfillment_id",
                table: "fulfillment_line",
                column: "fulfillment_id");

            migrationBuilder.CreateIndex(
                name: "ix_fulfillment_line_sales_line_id",
                table: "fulfillment_line",
                column: "sales_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_at",
                table: "payment",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "ix_payment_order_id",
                table: "payment",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotation_line_quotation_id",
                table: "quotation_line",
                column: "quotation_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotation_line_sku_id",
                table: "quotation_line",
                column: "sku_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotations_customer_id",
                table: "quotations",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotations_number",
                table: "quotations",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_line_order_id_line_no",
                table: "sales_line",
                columns: new[] { "order_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_line_sku_id",
                table: "sales_line",
                column: "sku_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_orders_customer_id",
                table: "sales_orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_orders_location_id",
                table: "sales_orders",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_orders_number",
                table: "sales_orders",
                column: "number",
                unique: true,
                filter: "number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sales_orders_status_created_at",
                table: "sales_orders",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_number",
                table: "sales_return",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_order_id",
                table: "sales_return",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_line_return_id",
                table: "sales_return_line",
                column: "return_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_line_sales_line_id",
                table: "sales_return_line",
                column: "sales_line_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_sequences");

            migrationBuilder.DropTable(
                name: "fulfillment_line");

            migrationBuilder.DropTable(
                name: "payment");

            migrationBuilder.DropTable(
                name: "quotation_line");

            migrationBuilder.DropTable(
                name: "sales_return_line");

            migrationBuilder.DropTable(
                name: "fulfillment");

            migrationBuilder.DropTable(
                name: "quotations");

            migrationBuilder.DropTable(
                name: "sales_line");

            migrationBuilder.DropTable(
                name: "sales_return");

            migrationBuilder.DropTable(
                name: "sales_orders");

            migrationBuilder.DropTable(
                name: "customers");
        }
    }
}
