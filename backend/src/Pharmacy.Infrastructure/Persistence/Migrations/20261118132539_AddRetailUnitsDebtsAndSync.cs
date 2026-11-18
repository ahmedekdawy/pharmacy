using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRetailUnitsDebtsAndSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "device_id",
                table: "sales",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "discount_amount",
                table: "sales",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "due_amount",
                table: "sales",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "has_stock_warning",
                table: "sales",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "paid_amount",
                table: "sales",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "payment_method",
                table: "sales",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Cash");

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "sales",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Online");

            migrationBuilder.AddColumn<decimal>(
                name: "sub_total",
                table: "sales",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "credit_amount",
                table: "sale_returns",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "refund_amount",
                table: "sale_returns",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "base_quantity",
                table: "sale_return_items",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "base_quantity",
                table: "sale_items",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "unit",
                table: "sale_items",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "unit_factor",
                table: "sale_items",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "base_quantity",
                table: "purchase_items",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "allow_strip_sale",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "allow_unit_sale",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "strip_price",
                table: "products",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "strips_per_box",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "unit_price",
                table: "products",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "units_per_strip",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "customers",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "credit_limit",
                table: "customers",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "customer_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    payment_method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    notes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    device_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_payments", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sales_tenant_id_customer_id",
                table: "sales",
                columns: new[] { "tenant_id", "customer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_tenant_id_sold_at",
                table: "sales",
                columns: new[] { "tenant_id", "sold_at" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_payments_tenant_customer",
                table: "customer_payments",
                columns: new[] { "tenant_id", "customer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_payments_tenant_paid_at",
                table: "customer_payments",
                columns: new[] { "tenant_id", "paid_at" });

            // Backfill existing data: until now everything was sold/purchased per box with factor 1
            // and every sale was fully paid in cash.
            migrationBuilder.Sql("UPDATE sales SET sub_total = total_amount, paid_amount = total_amount, due_amount = 0;");
            migrationBuilder.Sql("UPDATE sale_items SET base_quantity = quantity, unit_factor = 1, unit = 1;");
            migrationBuilder.Sql("UPDATE sale_return_items SET base_quantity = quantity;");
            migrationBuilder.Sql("UPDATE sale_returns SET refund_amount = total_amount, credit_amount = 0;");
            migrationBuilder.Sql("UPDATE purchase_items SET base_quantity = quantity;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_payments");

            migrationBuilder.DropIndex(
                name: "ix_sales_tenant_id_customer_id",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "ix_sales_tenant_id_sold_at",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "device_id",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "discount_amount",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "due_amount",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "has_stock_warning",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "paid_amount",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "source",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "sub_total",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "credit_amount",
                table: "sale_returns");

            migrationBuilder.DropColumn(
                name: "refund_amount",
                table: "sale_returns");

            migrationBuilder.DropColumn(
                name: "base_quantity",
                table: "sale_return_items");

            migrationBuilder.DropColumn(
                name: "base_quantity",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "unit",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "unit_factor",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "base_quantity",
                table: "purchase_items");

            migrationBuilder.DropColumn(
                name: "allow_strip_sale",
                table: "products");

            migrationBuilder.DropColumn(
                name: "allow_unit_sale",
                table: "products");

            migrationBuilder.DropColumn(
                name: "strip_price",
                table: "products");

            migrationBuilder.DropColumn(
                name: "strips_per_box",
                table: "products");

            migrationBuilder.DropColumn(
                name: "unit_price",
                table: "products");

            migrationBuilder.DropColumn(
                name: "units_per_strip",
                table: "products");

            migrationBuilder.DropColumn(
                name: "address",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "credit_limit",
                table: "customers");
        }
    }
}
