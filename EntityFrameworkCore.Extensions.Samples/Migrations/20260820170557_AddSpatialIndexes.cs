using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace EntityFrameworkCore.Extensions.Samples.Migrations
{
    /// <inheritdoc />
    public partial class AddSpatialIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Places",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Location = table.Column<Point>(type: "geography", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Places", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Regions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Boundary = table.Column<Polygon>(type: "geometry", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "SIX_Places_Location",
                table: "Places",
                column: "Location")
                .Annotation("EntityFrameworkCore.Extensions:SpatialIndex", true)
                .Annotation("EntityFrameworkCore.Extensions:SpatialIndexType", "geography");

            migrationBuilder.CreateIndex(
                name: "SIX_Regions_Boundary",
                table: "Regions",
                column: "Boundary")
                .Annotation("EntityFrameworkCore.Extensions:SpatialIndex", true)
                .Annotation("EntityFrameworkCore.Extensions:SpatialIndexBoundingBoxXMax", 180.0)
                .Annotation("EntityFrameworkCore.Extensions:SpatialIndexBoundingBoxXMin", -180.0)
                .Annotation("EntityFrameworkCore.Extensions:SpatialIndexBoundingBoxYMax", 90.0)
                .Annotation("EntityFrameworkCore.Extensions:SpatialIndexBoundingBoxYMin", -90.0)
                .Annotation("EntityFrameworkCore.Extensions:SpatialIndexCellsPerObject", 32)
                .Annotation("EntityFrameworkCore.Extensions:SpatialIndexType", "geometry");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Places");

            migrationBuilder.DropTable(
                name: "Regions");
        }
    }
}
