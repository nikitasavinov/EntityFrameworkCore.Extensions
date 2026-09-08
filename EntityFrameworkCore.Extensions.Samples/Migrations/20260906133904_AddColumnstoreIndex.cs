using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntityFrameworkCore.Extensions.Samples.Migrations
{
    /// <inheritdoc />
    public partial class AddColumnstoreIndex : Migration
    {
        private static readonly string[] ReportingColumns = ["Created", "Amount"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "Order",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "NCCI_Orders_Reporting",
                table: "Order",
                columns: ReportingColumns,
                filter: "[Amount] > 0")
                .Annotation("EntityFrameworkCore.Extensions:ColumnstoreIndex", true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "NCCI_Orders_Reporting",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "Order");
        }
    }
}
