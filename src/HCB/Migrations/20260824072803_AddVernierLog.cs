using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCB.Migrations
{
    /// <inheritdoc />
    public partial class AddVernierLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VernierLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    V1X = table.Column<double>(type: "REAL", nullable: true),
                    V1Y = table.Column<double>(type: "REAL", nullable: true),
                    V3X = table.Column<double>(type: "REAL", nullable: true),
                    V3Y = table.Column<double>(type: "REAL", nullable: true),
                    OffsetX = table.Column<double>(type: "REAL", nullable: true),
                    OffsetY = table.Column<double>(type: "REAL", nullable: true),
                    OffsetT = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VernierLog", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VernierLog_Time",
                table: "VernierLog",
                column: "Time");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VernierLog");
        }
    }
}
