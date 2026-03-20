using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenClaw.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workflow_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    definition_json = table.Column<string>(type: "text", nullable: false),
                    schedule_json = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_executions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    trigger = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    input_json = table.Column<string>(type: "text", nullable: true),
                    output_json = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pending_approval_node_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_executions", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflow_executions_workflow_definitions_workflow_definitio~",
                        column: x => x.workflow_definition_id,
                        principalTable: "workflow_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_node_executions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    node_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    input_json = table.Column<string>(type: "text", nullable: true),
                    output_json = table.Column<string>(type: "text", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_node_executions", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflow_node_executions_workflow_executions_workflow_execu~",
                        column: x => x.workflow_execution_id,
                        principalTable: "workflow_executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_definitions_created_by_user_id",
                table: "workflow_definitions",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_definitions_is_active",
                table: "workflow_definitions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_executions_started_at",
                table: "workflow_executions",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_executions_status",
                table: "workflow_executions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_executions_workflow_definition_id",
                table: "workflow_executions",
                column: "workflow_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_node_executions_workflow_execution_id",
                table: "workflow_node_executions",
                column: "workflow_execution_id");

            migrationBuilder.CreateIndex(
                name: "uq_workflow_node_executions_execution_node",
                table: "workflow_node_executions",
                columns: new[] { "workflow_execution_id", "node_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workflow_node_executions");

            migrationBuilder.DropTable(
                name: "workflow_executions");

            migrationBuilder.DropTable(
                name: "workflow_definitions");
        }
    }
}
