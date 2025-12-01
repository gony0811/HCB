using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCB.Migrations
{
    /// <inheritdoc />
    public partial class _20251201_AddIoDeviceDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IoDeviceDetail",
                columns: table => new
                {
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Ip = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    IoDeviceType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IoDeviceDetail", x => x.DeviceId);
                    table.ForeignKey(
                        name: "FK_IoDeviceDetail_Device_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Device",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IoData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    IoDataType = table.Column<string>(type: "TEXT", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ParentDeviceId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IoData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IoData_IoDeviceDetail_ParentDeviceId",
                        column: x => x.ParentDeviceId,
                        principalTable: "IoDeviceDetail",
                        principalColumn: "DeviceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IoData_ParentDeviceId",
                table: "IoData",
                column: "ParentDeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IoData");

            migrationBuilder.DropTable(
                name: "IoDeviceDetail");
        }
    }
}
