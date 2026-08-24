using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCB.Migrations
{
    /// <inheritdoc />
    public partial class AddCamDistCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Hc1_CenterX",
                table: "CamDistMeasurement",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Hc1_CenterY",
                table: "CamDistMeasurement",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Hc2_CenterX",
                table: "CamDistMeasurement",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Hc2_CenterY",
                table: "CamDistMeasurement",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Hc1_CenterX",
                table: "CamDistMeasurement");

            migrationBuilder.DropColumn(
                name: "Hc1_CenterY",
                table: "CamDistMeasurement");

            migrationBuilder.DropColumn(
                name: "Hc2_CenterX",
                table: "CamDistMeasurement");

            migrationBuilder.DropColumn(
                name: "Hc2_CenterY",
                table: "CamDistMeasurement");
        }
    }
}
