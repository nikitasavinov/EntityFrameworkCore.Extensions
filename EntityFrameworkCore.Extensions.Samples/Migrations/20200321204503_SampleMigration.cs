using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EntityFrameworkCore.Extensions.Samples.Migrations
{
    public partial class SampleMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(nullable: true)
                        .Annotation("DynamicDataMasking", "default()"),
                    Surname = table.Column<string>(nullable: true)
                        .Annotation("DynamicDataMasking", "default()"),
                    Phone = table.Column<string>(nullable: true)
                        .Annotation("DynamicDataMasking", "partial(2, \"XX-XX\", 1)"),
                    DiscountCardNumber = table.Column<int>(nullable: false)
                        .Annotation("DynamicDataMasking", "random(10, 100)"),
                    SampleProperty1 = table.Column<string>(nullable: true),
                    SampleProperty2 = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Order",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Created = table.Column<DateTime>(nullable: false),
                    CustomerId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Order", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Order_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Order_CustomerId",
                table: "Order",
                column: "CustomerId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Order");

            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
