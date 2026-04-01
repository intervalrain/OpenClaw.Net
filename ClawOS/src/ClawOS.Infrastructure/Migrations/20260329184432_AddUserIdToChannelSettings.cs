using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClawOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToChannelSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_channel_settings_channel_type",
                table: "channel_settings");

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "channel_settings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_channel_settings_user_id_channel_type",
                table: "channel_settings",
                columns: new[] { "user_id", "channel_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_channel_settings_user_id_channel_type",
                table: "channel_settings");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "channel_settings");

            migrationBuilder.CreateIndex(
                name: "ix_channel_settings_channel_type",
                table: "channel_settings",
                column: "channel_type",
                unique: true);
        }
    }
}
