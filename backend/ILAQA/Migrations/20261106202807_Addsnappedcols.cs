using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ILAQA.Migrations
{
    /// <inheritdoc />
    public partial class Addsnappedcols : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "SnappedLatitude",
                table: "RunGpsPoints",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SnappedLongitude",
                table: "RunGpsPoints",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SnappedLatitude",
                table: "RunGpsPoints");

            migrationBuilder.DropColumn(
                name: "SnappedLongitude",
                table: "RunGpsPoints");
        }
    }
}
