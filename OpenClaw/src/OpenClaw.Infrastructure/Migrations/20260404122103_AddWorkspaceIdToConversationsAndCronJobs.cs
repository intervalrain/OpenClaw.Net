using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenClaw.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceIdToConversationsAndCronJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "cron_jobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "Conversations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Backfill: set WorkspaceId to the user's personal workspace
            migrationBuilder.Sql("""
                UPDATE "Conversations" c
                SET "WorkspaceId" = w.id
                FROM workspaces w
                WHERE w.owner_user_id = c."UserId" AND w.is_personal = true
                """);

            migrationBuilder.Sql("""
                UPDATE cron_jobs j
                SET "WorkspaceId" = w.id
                FROM workspaces w
                WHERE w.owner_user_id = j.created_by_user_id AND w.is_personal = true
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "cron_jobs");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "Conversations");
        }
    }
}
