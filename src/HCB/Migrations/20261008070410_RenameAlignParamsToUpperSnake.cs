using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCB.Migrations
{
    /// <summary>
    /// 1차 이름 변경(RenameParamsToUpperSnake)에서 빠졌던 레시피 파라미터를 대문자 스네이크 케이스로 변경.
    /// 같은 레시피에 새 이름이 이미 있으면 해당 행은 건너뛴다(중복 생성 방지).
    /// </summary>
    public partial class RenameAlignParamsToUpperSnake : Migration
    {
        private static readonly (string Old, string New)[] RecipeNames =
        {
            (@"RightAlignHeight", @"RIGHT_ALIGN_HEIGHT"),
            (@"LeftAlignHeight", @"LEFT_ALIGN_HEIGHT"),
            (@"RefTopAlignDist", @"REF_TOP_ALIGN_DIST"),
            (@"RefBtmAlignDist", @"REF_BTM_ALIGN_DIST"),
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
                mb.Sql($@"UPDATE ""RecipeParam"" SET ""Name"" = '{to}'
                          WHERE ""Name"" = '{from}'
                            AND NOT EXISTS (SELECT 1 FROM ""RecipeParam"" r2
                                            WHERE r2.""RecipeId"" = ""RecipeParam"".""RecipeId"" AND r2.""Name"" = '{to}');");
            }
        }
    }
}
