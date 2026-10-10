using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ILAQA.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenSessionId : Migration
    {
        /// <inheritdoc />

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the SessionId column
            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            // Revoke all existing refresh tokens
            // because they were created without a SessionId.
            migrationBuilder.Sql(@"
        UPDATE ""RefreshTokens""
        SET ""RevokedAt"" = NOW()
        WHERE ""RevokedAt"" IS NULL;
    ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "RefreshTokens");
        }
    }
}
