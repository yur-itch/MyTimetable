using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyTimetable.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Lessons",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    LessonNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    LessonType = table.Column<string>(type: "text", nullable: false),
                    Professor = table.Column<string>(type: "text", nullable: false),
                    isCustom = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lessons", x => new { x.Date, x.LessonNumber });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Lessons");
        }
    }
}
