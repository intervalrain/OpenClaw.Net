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
            // Use DO block to safely handle case where tables/columns may not exist yet
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    UPDATE "Conversations" AS c
                    SET "WorkspaceId" = w."Id"
                    FROM "workspaces" AS w
                    WHERE w."owner_user_id" = c."UserId" AND w."is_personal" = true;
                EXCEPTION WHEN undefined_column OR undefined_table THEN
                    -- Skip backfill if columns/tables don't exist (fresh DB)
                    NULL;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    UPDATE "cron_jobs" AS j
                    SET "WorkspaceId" = w."Id"
                    FROM "workspaces" AS w
                    WHERE w."owner_user_id" = j."created_by_user_id" AND w."is_personal" = true;
                EXCEPTION WHEN undefined_column OR undefined_table THEN
                    NULL;
                END $$;
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
