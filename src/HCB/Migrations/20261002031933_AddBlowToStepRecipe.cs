using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCB.Migrations
{
    /// <inheritdoc />
    public partial class AddBlowToStepRecipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BlowDuration",
                table: "StepRecipe",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "BlowEnable",
                table: "StepRecipe",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BlowOnTime",
                table: "StepRecipe",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlowDuration",
                table: "StepRecipe");

            migrationBuilder.DropColumn(
                name: "BlowEnable",
                table: "StepRecipe");

            migrationBuilder.DropColumn(
                name: "BlowOnTime",
                table: "StepRecipe");
        }
    }
}
