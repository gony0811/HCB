using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCB.Migrations
{
    /// <inheritdoc />
    public partial class AddECParam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ECParam",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    Maximum = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Minimum = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ValueType = table.Column<string>(type: "TEXT", nullable: false),
                    UnitType = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ECParam", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ECParam_Name",
                table: "ECParam",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ECParam");
        }
    }
}
