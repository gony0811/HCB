using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCB.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeComponent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Component",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Component",
                table: "Recipes");
        }
    }
}
