using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCB.Migrations
{
    /// <inheritdoc />
    public partial class RedesignStepRecipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Force",
                table: "StepRecipe",
                newName: "LoadCell");

            migrationBuilder.RenameColumn(
                name: "DurationTime",
                table: "StepRecipe",
                newName: "Current2");

            migrationBuilder.AddColumn<int>(
                name: "AccTime",
                table: "StepRecipe",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AccTime2",
                table: "StepRecipe",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ContTime",
                table: "StepRecipe",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Current",
                table: "StepRecipe",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "DecTime",
                table: "StepRecipe",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "StepRecipe",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "VacOffTime",
                table: "StepRecipe",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccTime",
                table: "StepRecipe");

            migrationBuilder.DropColumn(
                name: "AccTime2",
                table: "StepRecipe");

            migrationBuilder.DropColumn(
                name: "ContTime",
                table: "StepRecipe");

            migrationBuilder.DropColumn(
                name: "Current",
                table: "StepRecipe");

            migrationBuilder.DropColumn(
                name: "DecTime",
                table: "StepRecipe");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "StepRecipe");

            migrationBuilder.DropColumn(
                name: "VacOffTime",
                table: "StepRecipe");

            migrationBuilder.RenameColumn(
                name: "LoadCell",
                table: "StepRecipe",
                newName: "Force");

            migrationBuilder.RenameColumn(
                name: "Current2",
                table: "StepRecipe",
                newName: "DurationTime");
        }
    }
}
