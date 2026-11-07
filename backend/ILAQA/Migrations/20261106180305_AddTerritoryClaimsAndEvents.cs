using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace ILAQA.Migrations
{
    /// <inheritdoc />
    public partial class AddTerritoryClaimsAndEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:extensions.postgis", ",,")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "TerritoryClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartSequenceNo = table.Column<int>(type: "integer", nullable: false),
                    EndSequenceNo = table.Column<int>(type: "integer", nullable: false),
                    ResultTerritoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProposedBoundary = table.Column<Polygon>(type: "geometry(Polygon,4326)", nullable: false),
                    ProposedAreaSquareMeters = table.Column<double>(type: "double precision", nullable: false),
                    NewlyClaimedAreaSquareMeters = table.Column<double>(type: "double precision", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecisionReason = table.Column<string>(type: "text", nullable: true),
                    XpAwarded = table.Column<long>(type: "bigint", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerritoryClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TerritoryClaims_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerritoryClaims_Territories_ResultTerritoryId",
                        column: x => x.ResultTerritoryId,
                        principalTable: "Territories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TerritoryEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TerritoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedTerritoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousOwnerUserId = table.Column<string>(type: "text", nullable: true),
                    NewOwnerUserId = table.Column<string>(type: "text", nullable: true),
                    EventType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BoundaryBefore = table.Column<Geometry>(type: "geometry(Geometry,4326)", nullable: true),
                    BoundaryAfter = table.Column<Geometry>(type: "geometry(Geometry,4326)", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerritoryEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TerritoryEvents_AspNetUsers_NewOwnerUserId",
                        column: x => x.NewOwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerritoryEvents_AspNetUsers_PreviousOwnerUserId",
                        column: x => x.PreviousOwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerritoryEvents_Territories_RelatedTerritoryId",
                        column: x => x.RelatedTerritoryId,
                        principalTable: "Territories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerritoryEvents_Territories_TerritoryId",
                        column: x => x.TerritoryId,
                        principalTable: "Territories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerritoryEvents_TerritoryClaims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "TerritoryClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryClaims_ResultTerritoryId",
                table: "TerritoryClaims",
                column: "ResultTerritoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryClaims_RunId_StartSequenceNo_EndSequenceNo",
                table: "TerritoryClaims",
                columns: new[] { "RunId", "StartSequenceNo", "EndSequenceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryClaims_RunId_SubmittedAt",
                table: "TerritoryClaims",
                columns: new[] { "RunId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryEvents_ClaimId",
                table: "TerritoryEvents",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryEvents_NewOwnerUserId",
                table: "TerritoryEvents",
                column: "NewOwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryEvents_PreviousOwnerUserId",
                table: "TerritoryEvents",
                column: "PreviousOwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryEvents_RelatedTerritoryId",
                table: "TerritoryEvents",
                column: "RelatedTerritoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryEvents_TerritoryId_OccurredAt",
                table: "TerritoryEvents",
                columns: new[] { "TerritoryId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TerritoryEvents");

            migrationBuilder.DropTable(
                name: "TerritoryClaims");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:extensions.postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");
        }
    }
}
