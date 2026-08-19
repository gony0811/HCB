using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCB.Migrations
{
    /// <inheritdoc />
    public partial class AddBondingModeFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TracingMode",
                table: "BondingRecord",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Use2DMapping",
                table: "BondingRecord",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UseBtmIndividualMeasure",
                table: "BondingRecord",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UseFiducialTracking",
                table: "BondingRecord",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UseRightFidSimilarity",
                table: "BondingRecord",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TracingMode",
                table: "BondingRecord");

            migrationBuilder.DropColumn(
                name: "Use2DMapping",
                table: "BondingRecord");

            migrationBuilder.DropColumn(
                name: "UseBtmIndividualMeasure",
                table: "BondingRecord");

            migrationBuilder.DropColumn(
                name: "UseFiducialTracking",
                table: "BondingRecord");

            migrationBuilder.DropColumn(
                name: "UseRightFidSimilarity",
                table: "BondingRecord");
        }
    }
}
