using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCB.Migrations
{
    /// <inheritdoc />
    public partial class AddBondingData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BondingRecord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AvgMode = table.Column<bool>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    ParentRecordId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BondingRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BondingRecord_BondingRecord_ParentRecordId",
                        column: x => x.ParentRecordId,
                        principalTable: "BondingRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BondingAnalysis",
                columns: table => new
                {
                    BondingRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    P_PC_Fid_DX = table.Column<double>(type: "REAL", nullable: true),
                    P_PC_Fid_DY = table.Column<double>(type: "REAL", nullable: true),
                    P_PC_Fid_Dist = table.Column<double>(type: "REAL", nullable: true),
                    P_PC_Fid_Theta = table.Column<double>(type: "REAL", nullable: true),
                    P_PC_Align_DX = table.Column<double>(type: "REAL", nullable: true),
                    P_PC_Align_DY = table.Column<double>(type: "REAL", nullable: true),
                    P_PC_Align_Dist = table.Column<double>(type: "REAL", nullable: true),
                    P_PC_Align_Theta = table.Column<double>(type: "REAL", nullable: true),
                    P_HC_Fid_L_X = table.Column<double>(type: "REAL", nullable: true),
                    P_HC_Fid_L_Y = table.Column<double>(type: "REAL", nullable: true),
                    P_HC_Fid_R_X = table.Column<double>(type: "REAL", nullable: true),
                    P_HC_Fid_R_Y = table.Column<double>(type: "REAL", nullable: true),
                    P_HC_Fid_DX = table.Column<double>(type: "REAL", nullable: true),
                    P_HC_Fid_DY = table.Column<double>(type: "REAL", nullable: true),
                    P_HC_Fid_Dist = table.Column<double>(type: "REAL", nullable: true),
                    P_HC_Fid_Theta = table.Column<double>(type: "REAL", nullable: true),
                    W_HC_Fid_L_X = table.Column<double>(type: "REAL", nullable: true),
                    W_HC_Fid_L_Y = table.Column<double>(type: "REAL", nullable: true),
                    W_HC_Fid_R_X = table.Column<double>(type: "REAL", nullable: true),
                    W_HC_Fid_R_Y = table.Column<double>(type: "REAL", nullable: true),
                    W_HC_Fid_DX = table.Column<double>(type: "REAL", nullable: true),
                    W_HC_Fid_DY = table.Column<double>(type: "REAL", nullable: true),
                    W_HC_Fid_Dist = table.Column<double>(type: "REAL", nullable: true),
                    W_HC_Fid_Theta = table.Column<double>(type: "REAL", nullable: true),
                    W_HC_Align_L_X = table.Column<double>(type: "REAL", nullable: true),
                    W_HC_Align_L_Y = table.Column<double>(type: "REAL", nullable: true),
                    W_HC_Align_R_X = table.Column<double>(type: "REAL", nullable: true),
                    W_HC_Align_R_Y = table.Column<double>(type: "REAL", nullable: true),
                    W_HC_Align_DX = table.Column<double>(type: "REAL", nullable: true),
                    W_HC_Align_DY = table.Column<double>(type: "REAL", nullable: true),
                    W_HC_Align_Dist = table.Column<double>(type: "REAL", nullable: true),
                    W_HC_Align_Theta = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BondingAnalysis", x => x.BondingRecordId);
                    table.ForeignKey(
                        name: "FK_BondingAnalysis_BondingRecord_BondingRecordId",
                        column: x => x.BondingRecordId,
                        principalTable: "BondingRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BondingCoordinate",
                columns: table => new
                {
                    BondingRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    BFL_X = table.Column<double>(type: "REAL", nullable: false),
                    BFL_Y = table.Column<double>(type: "REAL", nullable: false),
                    BFR_X = table.Column<double>(type: "REAL", nullable: false),
                    BFR_Y = table.Column<double>(type: "REAL", nullable: false),
                    BL_X = table.Column<double>(type: "REAL", nullable: false),
                    BL_Y = table.Column<double>(type: "REAL", nullable: false),
                    BR_X = table.Column<double>(type: "REAL", nullable: false),
                    BR_Y = table.Column<double>(type: "REAL", nullable: false),
                    TL_X = table.Column<double>(type: "REAL", nullable: false),
                    TL_Y = table.Column<double>(type: "REAL", nullable: false),
                    TR_X = table.Column<double>(type: "REAL", nullable: false),
                    TR_Y = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BondingCoordinate", x => x.BondingRecordId);
                    table.ForeignKey(
                        name: "FK_BondingCoordinate_BondingRecord_BondingRecordId",
                        column: x => x.BondingRecordId,
                        principalTable: "BondingRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BondingEquipment",
                columns: table => new
                {
                    BondingRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    PcTRad = table.Column<double>(type: "REAL", nullable: false),
                    Hc1Rad = table.Column<double>(type: "REAL", nullable: false),
                    Hc2Rad = table.Column<double>(type: "REAL", nullable: false),
                    Hcro_X = table.Column<double>(type: "REAL", nullable: false),
                    Hcro_Y = table.Column<double>(type: "REAL", nullable: false),
                    Hc2Offset_X = table.Column<double>(type: "REAL", nullable: false),
                    Hc2Offset_Y = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BondingEquipment", x => x.BondingRecordId);
                    table.ForeignKey(
                        name: "FK_BondingEquipment_BondingRecord_BondingRecordId",
                        column: x => x.BondingRecordId,
                        principalTable: "BondingRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BondingMeasurement",
                columns: table => new
                {
                    BondingRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    TopRF_StageX = table.Column<double>(type: "REAL", nullable: true),
                    TopRF_StageY = table.Column<double>(type: "REAL", nullable: true),
                    TopRF_DxCam = table.Column<double>(type: "REAL", nullable: true),
                    TopRF_DyCam = table.Column<double>(type: "REAL", nullable: true),
                    TopRF_CenterX = table.Column<double>(type: "REAL", nullable: true),
                    TopRF_CenterY = table.Column<double>(type: "REAL", nullable: true),
                    TopRA_StageX = table.Column<double>(type: "REAL", nullable: true),
                    TopRA_StageY = table.Column<double>(type: "REAL", nullable: true),
                    TopRA_DxCam = table.Column<double>(type: "REAL", nullable: true),
                    TopRA_DyCam = table.Column<double>(type: "REAL", nullable: true),
                    TopRA_CenterX = table.Column<double>(type: "REAL", nullable: true),
                    TopRA_CenterY = table.Column<double>(type: "REAL", nullable: true),
                    TopLF_StageX = table.Column<double>(type: "REAL", nullable: true),
                    TopLF_StageY = table.Column<double>(type: "REAL", nullable: true),
                    TopLF_DxCam = table.Column<double>(type: "REAL", nullable: true),
                    TopLF_DyCam = table.Column<double>(type: "REAL", nullable: true),
                    TopLF_CenterX = table.Column<double>(type: "REAL", nullable: true),
                    TopLF_CenterY = table.Column<double>(type: "REAL", nullable: true),
                    TopLA_StageX = table.Column<double>(type: "REAL", nullable: true),
                    TopLA_StageY = table.Column<double>(type: "REAL", nullable: true),
                    TopLA_DxCam = table.Column<double>(type: "REAL", nullable: true),
                    TopLA_DyCam = table.Column<double>(type: "REAL", nullable: true),
                    TopLA_CenterX = table.Column<double>(type: "REAL", nullable: true),
                    TopLA_CenterY = table.Column<double>(type: "REAL", nullable: true),
                    BtmRF_DxCam = table.Column<double>(type: "REAL", nullable: true),
                    BtmRF_DyCam = table.Column<double>(type: "REAL", nullable: true),
                    BtmRA_DxCam = table.Column<double>(type: "REAL", nullable: true),
                    BtmRA_DyCam = table.Column<double>(type: "REAL", nullable: true),
                    BtmLF_DxCam = table.Column<double>(type: "REAL", nullable: true),
                    BtmLF_DyCam = table.Column<double>(type: "REAL", nullable: true),
                    BtmLA_DxCam = table.Column<double>(type: "REAL", nullable: true),
                    BtmLA_DyCam = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BondingMeasurement", x => x.BondingRecordId);
                    table.ForeignKey(
                        name: "FK_BondingMeasurement_BondingRecord_BondingRecordId",
                        column: x => x.BondingRecordId,
                        principalTable: "BondingRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BondingResult",
                columns: table => new
                {
                    BondingRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    ResultX = table.Column<double>(type: "REAL", nullable: false),
                    ResultY = table.Column<double>(type: "REAL", nullable: false),
                    ResultT = table.Column<double>(type: "REAL", nullable: false),
                    Vernier_OffsetX = table.Column<double>(type: "REAL", nullable: true),
                    Vernier_OffsetY = table.Column<double>(type: "REAL", nullable: true),
                    Vernier_OffsetT = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BondingResult", x => x.BondingRecordId);
                    table.ForeignKey(
                        name: "FK_BondingResult_BondingRecord_BondingRecordId",
                        column: x => x.BondingRecordId,
                        principalTable: "BondingRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BondingSetting",
                columns: table => new
                {
                    BondingRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    OffsetX = table.Column<double>(type: "REAL", nullable: false),
                    OffsetY = table.Column<double>(type: "REAL", nullable: false),
                    OffsetT = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BondingSetting", x => x.BondingRecordId);
                    table.ForeignKey(
                        name: "FK_BondingSetting_BondingRecord_BondingRecordId",
                        column: x => x.BondingRecordId,
                        principalTable: "BondingRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BondingRecord_Kind",
                table: "BondingRecord",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_BondingRecord_ParentRecordId",
                table: "BondingRecord",
                column: "ParentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_BondingRecord_Time",
                table: "BondingRecord",
                column: "Time");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BondingAnalysis");

            migrationBuilder.DropTable(
                name: "BondingCoordinate");

            migrationBuilder.DropTable(
                name: "BondingEquipment");

            migrationBuilder.DropTable(
                name: "BondingMeasurement");

            migrationBuilder.DropTable(
                name: "BondingResult");

            migrationBuilder.DropTable(
                name: "BondingSetting");

            migrationBuilder.DropTable(
                name: "BondingRecord");
        }
    }
}
