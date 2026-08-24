using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCB.Migrations
{
    /// <inheritdoc />
    public partial class AddCamDistHcroMeasurement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CamDistMeasurement",
                columns: table => new
                {
                    BondingRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    Hc1_StageX = table.Column<double>(type: "REAL", nullable: false),
                    Hc1_StageY = table.Column<double>(type: "REAL", nullable: false),
                    Hc1_DxCam = table.Column<double>(type: "REAL", nullable: false),
                    Hc1_DyCam = table.Column<double>(type: "REAL", nullable: false),
                    Hc2_StageX = table.Column<double>(type: "REAL", nullable: false),
                    Hc2_StageY = table.Column<double>(type: "REAL", nullable: false),
                    Hc2_DxCam = table.Column<double>(type: "REAL", nullable: false),
                    Hc2_DyCam = table.Column<double>(type: "REAL", nullable: false),
                    Hc2Offset_X = table.Column<double>(type: "REAL", nullable: false),
                    Hc2Offset_Y = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CamDistMeasurement", x => x.BondingRecordId);
                    table.ForeignKey(
                        name: "FK_CamDistMeasurement_BondingRecord_BondingRecordId",
                        column: x => x.BondingRecordId,
                        principalTable: "BondingRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HcroMeasurementPoint",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BondingRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    PointIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Angle = table.Column<double>(type: "REAL", nullable: false),
                    Hc1_X = table.Column<double>(type: "REAL", nullable: false),
                    Hc1_Y = table.Column<double>(type: "REAL", nullable: false),
                    Hc2_X = table.Column<double>(type: "REAL", nullable: false),
                    Hc2_Y = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HcroMeasurementPoint", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HcroMeasurementPoint_BondingRecord_BondingRecordId",
                        column: x => x.BondingRecordId,
                        principalTable: "BondingRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HcroMeasurementPoint_BondingRecordId",
                table: "HcroMeasurementPoint",
                column: "BondingRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CamDistMeasurement");

            migrationBuilder.DropTable(
                name: "HcroMeasurementPoint");
        }
    }
}
