using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ILAQA.Migrations
{
    /// <inheritdoc />
    public partial class MakeClaimRangeIndexNonUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TerritoryClaims_RunId_StartSequenceNo_EndSequenceNo",
                table: "TerritoryClaims");

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryClaims_RunId_StartSequenceNo_EndSequenceNo",
                table: "TerritoryClaims",
                columns: new[] { "RunId", "StartSequenceNo", "EndSequenceNo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TerritoryClaims_RunId_StartSequenceNo_EndSequenceNo",
                table: "TerritoryClaims");

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryClaims_RunId_StartSequenceNo_EndSequenceNo",
                table: "TerritoryClaims",
                columns: new[] { "RunId", "StartSequenceNo", "EndSequenceNo" },
                unique: true);
        }
    }
}
