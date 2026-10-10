using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ILAQA.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActiveSessionId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveSessionId",
                table: "AspNetUsers");
        }
    }
}
