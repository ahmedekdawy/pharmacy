using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDrugBarcodeRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "drug_barcodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    barcode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_en = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    scientific_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    manufacturer = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    strips_per_box = table.Column<int>(type: "integer", nullable: false),
                    units_per_strip = table.Column<int>(type: "integer", nullable: false),
                    public_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    confirmations = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drug_barcodes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_drug_barcodes_barcode",
                table: "drug_barcodes",
                column: "barcode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "drug_barcodes");
        }
    }
}
