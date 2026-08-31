using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCB.Migrations
{
    /// <inheritdoc />
    public partial class AddPlacementResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlacementResult",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Row = table.Column<int>(type: "INTEGER", nullable: false),
                    Col = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorX = table.Column<double>(type: "REAL", nullable: false),
                    ErrorY = table.Column<double>(type: "REAL", nullable: false),
                    ErrorTheta = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlacementResult", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlacementResult_Row_Col",
                table: "PlacementResult",
                columns: new[] { "Row", "Col" });

            migrationBuilder.CreateIndex(
                name: "IX_PlacementResult_Time",
                table: "PlacementResult",
                column: "Time");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlacementResult");
        }
    }
}
