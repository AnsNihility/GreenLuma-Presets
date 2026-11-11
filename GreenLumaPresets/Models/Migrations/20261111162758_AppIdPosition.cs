using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GreenLumaPresets.Models.Migrations
{
    /// <inheritdoc />
    public partial class AppIdPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "AppIds",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Existing rows: rowid is the closest thing to the original insertion order.
            migrationBuilder.Sql("UPDATE AppIds SET Position = rowid;");

            migrationBuilder.CreateIndex(
                name: "IX_AppIds_PresetId_Position",
                table: "AppIds",
                columns: new[] { "PresetId", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppIds_PresetId_Position",
                table: "AppIds");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "AppIds");
        }
    }
}
