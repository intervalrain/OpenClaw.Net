using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenClaw.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    system_prompt = table.Column<string>(type: "text", nullable: false),
                    tools_json = table.Column<string>(type: "text", nullable: false),
                    sub_agent_ids_json = table.Column<string>(type: "text", nullable: false),
                    max_iterations = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_definitions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_definitions_user_id",
                table: "agent_definitions",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_definitions_ws_name",
                table: "agent_definitions",
                columns: new[] { "workspace_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_definitions");
        }
    }
}
