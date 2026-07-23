using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCollaborationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "RefreshTokens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE refreshToken
                SET WorkspaceId = membership.WorkspaceId
                FROM RefreshTokens AS refreshToken
                CROSS APPLY (
                    SELECT TOP (1) WorkspaceId
                    FROM WorkspaceMembers AS member
                    WHERE member.UserId = refreshToken.UserId
                    ORDER BY CASE WHEN member.Role = 'Owner' THEN 0 ELSE 1 END, member.CreatedAtUtc, member.Id
                ) AS membership;
                """);

            migrationBuilder.Sql("""
                DELETE r
                FROM RefreshTokens AS r
                LEFT JOIN WorkspaceMembers AS m ON m.UserId = r.UserId
                WHERE r.WorkspaceId IS NULL AND m.UserId IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "WorkspaceId",
                table: "RefreshTokens",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "WorkspaceInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AcceptedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkspaceInvitations_AspNetUsers_AcceptedByUserId",
                        column: x => x.AcceptedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkspaceInvitations_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkspaceInvitations_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_WorkspaceId",
                table: "RefreshTokens",
                columns: new[] { "UserId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_WorkspaceId",
                table: "RefreshTokens",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceInvitations_AcceptedByUserId",
                table: "WorkspaceInvitations",
                column: "AcceptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceInvitations_CreatedByUserId",
                table: "WorkspaceInvitations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceInvitations_TokenHash",
                table: "WorkspaceInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceInvitations_WorkspaceId_CreatedAtUtc",
                table: "WorkspaceInvitations",
                columns: new[] { "WorkspaceId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceInvitations_WorkspaceId_NormalizedEmail",
                table: "WorkspaceInvitations",
                columns: new[] { "WorkspaceId", "NormalizedEmail" },
                unique: true,
                filter: "[AcceptedAtUtc] IS NULL AND [RevokedAtUtc] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_Workspaces_WorkspaceId",
                table: "RefreshTokens",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_Workspaces_WorkspaceId",
                table: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "WorkspaceInvitations");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserId_WorkspaceId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_WorkspaceId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "RefreshTokens");
        }
    }
}
