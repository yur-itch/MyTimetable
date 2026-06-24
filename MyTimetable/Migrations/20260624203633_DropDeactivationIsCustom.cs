using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyTimetable.Migrations
{
    /// <inheritdoc />
    public partial class DropDeactivationIsCustom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "isCustom",
                table: "Deactivations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "isCustom",
                table: "Deactivations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
