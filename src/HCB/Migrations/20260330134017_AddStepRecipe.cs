using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCB.Migrations
{
    /// <inheritdoc />
    public partial class AddStepRecipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StepRecipe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecipeId = table.Column<int>(type: "INTEGER", nullable: false),
                    StepNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Force = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    DurationTime = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StepRecipe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StepRecipe_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StepRecipe_RecipeId",
                table: "StepRecipe",
                column: "RecipeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StepRecipe");
        }
    }
}
