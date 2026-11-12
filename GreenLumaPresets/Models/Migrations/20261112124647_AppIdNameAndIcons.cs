using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GreenLumaPresets.Models.Migrations
{
    /// <inheritdoc />
    public partial class AppIdNameAndIcons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IconAppId",
                table: "Presets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IconUrl",
                table: "AppIds",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "AppIds",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IconAppId",
                table: "Presets");

            migrationBuilder.DropColumn(
                name: "IconUrl",
                table: "AppIds");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "AppIds");
        }
    }
}
