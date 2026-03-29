using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenClaw.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserModelProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allow_user_override",
                table: "model_providers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "model_providers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_model_providers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    global_model_provider_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    encrypted_api_key = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_model_providers", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_model_providers_model_providers_global_model_provider_~",
                        column: x => x.global_model_provider_id,
                        principalTable: "model_providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_model_providers_global_model_provider_id",
                table: "user_model_providers",
                column: "global_model_provider_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_model_providers_user_id",
                table: "user_model_providers",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_model_providers_user_id_name",
                table: "user_model_providers",
                columns: new[] { "user_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_model_providers");

            migrationBuilder.DropColumn(
                name: "allow_user_override",
                table: "model_providers");

            migrationBuilder.DropColumn(
                name: "description",
                table: "model_providers");

        }
    }
}
