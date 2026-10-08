using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCB.Migrations
{
    /// <summary>
    /// RecipeParam / ECParam 파라미터 이름을 대문자 스네이크 케이스로 변경.
    /// 같은 레시피(또는 EC)에 새 이름이 이미 있으면 해당 행은 건너뛴다(중복 생성 방지).
    /// </summary>
    public partial class RenameParamsToUpperSnake : Migration
    {
        private static readonly (string Old, string New)[] RecipeNames =
        {
            (@"TopDieThickness", @"TOP_DIE_THICKNESS"),
            (@"BtmDieThickness", @"BTM_DIE_THICKNESS"),
            (@"WaferSize", @"WAFER_SIZE"),
            (@"DieSizeX", @"DIE_SIZE_X"),
            (@"DieSizeY", @"DIE_SIZE_Y"),
            (@"GapX", @"GAP_X"),
            (@"GapY", @"GAP_Y"),
            (@"ScribeShiftX", @"SCRIBE_SHIFT_X"),
            (@"ScribeShiftY", @"SCRIBE_SHIFT_Y"),
            (@"AlignTopSpacingX", @"ALIGN_TOP_SPACING_X"),
            (@"AlignTopSpacingY", @"ALIGN_TOP_SPACING_Y"),
            (@"HC1 피듀셜 위치 보정 X", @"HC1_FID_OFFSET_X"),
            (@"HC1 피듀셜 위치 보정 Y", @"HC1_FID_OFFSET_Y"),
            (@"HC2 피듀셜 위치 보정 X", @"HC2_FID_OFFSET_X"),
            (@"HC2 피듀셜 위치 보정 Y", @"HC2_FID_OFFSET_Y"),
            (@"HcCenterErrorX", @"HC_CENTER_ERROR_X"),
            (@"HcCenterErrorY", @"HC_CENTER_ERROR_Y"),
            (@"xLowErrorOffset", @"LOW_ERROR_OFFSET_X"),
            (@"yLowErrorOffset", @"LOW_ERROR_OFFSET_Y"),
            (@"TopBtmGap", @"TOP_BTM_GAP"),
            (@"BondingForce", @"BONDING_FORCE"),
            (@"BondingTime", @"BONDING_TIME"),
            (@"버니어_OFFSET_X", @"VERNIER_OFFSET_X"),
            (@"버니어_OFFSET_Y", @"VERNIER_OFFSET_Y"),
            (@"버니어_거리_X", @"VERNIER_DIST_X"),
            (@"버니어_거리_Y", @"VERNIER_DIST_Y"),
            (@"Vision Recipe", @"VISION_RECIPE"),
        };

        private static readonly (string Old, string New)[] EcNames =
        {
            (@"ShankToWaferOffset", @"SHANK_TO_WAFER_OFFSET"),
            (@"ShankToDieOffset", @"SHANK_TO_DIE_OFFSET"),
            (@"ShankLowOffsetX", @"SHANK_LOW_OFFSET_X"),
            (@"ShankLowOffsetY", @"SHANK_LOW_OFFSET_Y"),
            (@"AlignDistTolerance", @"ALIGN_DIST_TOLERANCE"),
            (@"Hc1FidRefDx", @"HC1_FID_REF_DX"),
            (@"Hc1FidRefDy", @"HC1_FID_REF_DY"),
            (@"Hc2FidRefDx", @"HC2_FID_REF_DX"),
            (@"Hc2FidRefDy", @"HC2_FID_REF_DY"),
            (@"LowVisionRetryMax", @"LOW_VISION_RETRY_MAX"),
            (@"VisionRetryMax", @"VISION_RETRY_MAX"),
            (@"VisionRetryStepMm", @"VISION_RETRY_STEP_MM"),
            (@"BtmThetaSign", @"BTM_THETA_SIGN"),
            (@"BtmThetaMinDeg", @"BTM_THETA_MIN_DEG"),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => Rename(migrationBuilder, toNew: true);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => Rename(migrationBuilder, toNew: false);

        private static void Rename(MigrationBuilder mb, bool toNew)
        {
            foreach (var (o, n) in RecipeNames)
            {
                var (from, to) = toNew ? (o, n) : (n, o);
                mb.Sql($@"UPDATE ""RecipeParam"" SET ""Name"" = '{Q(to)}'
                          WHERE ""Name"" = '{Q(from)}'
                            AND NOT EXISTS (SELECT 1 FROM ""RecipeParam"" r2
                                            WHERE r2.""RecipeId"" = ""RecipeParam"".""RecipeId"" AND r2.""Name"" = '{Q(to)}');");
            }

            foreach (var (o, n) in EcNames)
            {
                var (from, to) = toNew ? (o, n) : (n, o);
                mb.Sql($@"UPDATE ""ECParam"" SET ""Name"" = '{Q(to)}'
                          WHERE ""Name"" = '{Q(from)}'
                            AND NOT EXISTS (SELECT 1 FROM ""ECParam"" e2 WHERE e2.""Name"" = '{Q(to)}');");
            }
        }

        private static string Q(string s) => s.Replace("'", "''");
    }
}
