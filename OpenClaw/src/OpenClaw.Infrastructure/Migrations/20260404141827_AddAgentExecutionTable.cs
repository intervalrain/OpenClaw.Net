using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenClaw.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentExecutionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_executions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_execution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    agent_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    task_graph_json = table.Column<string>(type: "text", nullable: true),
                    node_states_json = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total_tokens_used = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_executions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_executions_agent_name",
                table: "agent_executions",
                column: "agent_name");

            migrationBuilder.CreateIndex(
                name: "ix_agent_executions_parent_id",
                table: "agent_executions",
                column: "parent_execution_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_executions_status",
                table: "agent_executions",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_executions");
        }
    }
}
