using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ILAQA.Migrations
{
    /// <inheritdoc />
    public partial class AddTerritoryRunLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
           

            migrationBuilder.AddColumn<int>(
                name: "LoopIndex",
                table: "Territories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "RunId",
                table: "Territories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Territories_RunId_LoopIndex",
                table: "Territories",
                columns: new[] { "RunId", "LoopIndex" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Territories_Runs_RunId",
                table: "Territories",
                column: "RunId",
                principalTable: "Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Territories_Runs_RunId",
                table: "Territories");

            migrationBuilder.DropIndex(
                name: "IX_Territories_RunId_LoopIndex",
                table: "Territories");

            migrationBuilder.DropColumn(
                name: "LoopIndex",
                table: "Territories");

            migrationBuilder.DropColumn(
                name: "RunId",
                table: "Territories");

            
        }
    }
}
