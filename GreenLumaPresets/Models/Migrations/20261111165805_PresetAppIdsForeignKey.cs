using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GreenLumaPresets.Models.Migrations
{
    /// <inheritdoc />
    public partial class PresetAppIdsForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Orphans can't exist through the app, but would violate the new constraint: drop them defensively.
            migrationBuilder.Sql("DELETE FROM AppIds WHERE PresetId NOT IN (SELECT Id FROM Presets);");

            migrationBuilder.AddForeignKey(
                name: "FK_AppIds_Presets_PresetId",
                table: "AppIds",
                column: "PresetId",
                principalTable: "Presets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppIds_Presets_PresetId",
                table: "AppIds");
        }
    }
}
