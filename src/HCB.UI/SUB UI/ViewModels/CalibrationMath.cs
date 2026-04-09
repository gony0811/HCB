using System;

namespace HCB.UI
{
    public class CalibrationMath
    {
        // ═══════════════════════════════════════════════════════════════════
        //  1. 카메라 각도 산출
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 절대 좌표계 기준 카메라 비전 좌표계 각도 Θ 산출.
        /// HX(A) = WY(A) 조건에서 측정된 HcX(A), HcY(A)로부터 계산.
        /// </summary>
        /// <param name="hxA">Stage 이동값 HX(A) = WY(A)</param>
        /// <param name="hcX">카메라 비전 좌표계 X 측정값 HcX(A)</param>
        /// <param name="hcY">카메라 비전 좌표계 Y 측정값 HcY(A)</param>
        /// <returns>카메라 틀어짐 각도 Θ (단위: radian)</returns>
        public static double ComputeCameraTheta(double hxA, double hcX, double hcY)
        {
            double ratioX = hcX / hxA;
            double ratioY = hcY / hxA;
            double sin2Theta_fromX = (ratioX * ratioX) - 1.0;
            double sin2Theta_fromY = 1.0 - (ratioY * ratioY);
            double sin2Theta = (sin2Theta_fromX + sin2Theta_fromY) / 2.0;
            sin2Theta = Math.Clamp(sin2Theta, -1.0, 1.0);
            double theta = Math.Asin(sin2Theta) / 2.0;
            return theta;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  2. HcRO 회전 중심 산출
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 회전 좌표계 원점 HcRO = (HX(C), WY(C)) 계산.
        /// 회전 전/후 두 점 쌍으로부터 연립방정식을 풀어 회전 중심을 구한다.
        /// </summary>
        public static Point2D ComputeHcRO(
            Point2D hc1Before, Point2D hc1After,
            Point2D hc2Before, Point2D hc2After)
        {
            double u1x = hc1After.X - hc1Before.X;
            double u1y = hc1After.Y - hc1Before.Y;
            double u2x = hc2After.X - hc2Before.X;
            double u2y = hc2After.Y - hc2Before.Y;

            double d = u1x * u2y - u1y * u2x;
            if (Math.Abs(d) < 1e-10)
                throw new InvalidOperationException(
                    "D값이 0에 수렴합니다. 회전각이 너무 작거나 두 마크가 같은 방향으로 이동했습니다.");

            double k1 = (hc1After.X * hc1After.X - hc1Before.X * hc1Before.X
                       + hc1After.Y * hc1After.Y - hc1Before.Y * hc1Before.Y) / 2.0;
            double k2 = (hc2After.X * hc2After.X - hc2Before.X * hc2Before.X
                       + hc2After.Y * hc2After.Y - hc2Before.Y * hc2Before.Y) / 2.0;

            return Point2D.of(
                (k1 * u2y - k2 * u1y) / d,
                (k2 * u1x - k1 * u2x) / d);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  3. 회전 행렬 적용
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 회전 행렬 적용.
        /// [X']   [cosΘ  -sinΘ] [X]
        /// [Y'] = [sinΘ   cosΘ] [Y]
        /// </summary>
        public static Point2D ApplyRotation(Point2D point, double thetaRad)
        {
            double cos = Math.Cos(thetaRad);
            double sin = Math.Sin(thetaRad);
            return Point2D.of(
                cos * point.X - sin * point.Y,
                sin * point.X + cos * point.Y);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  4. Pc → HcRO 변환 유틸리티 (PDF p.11-13)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Pc 절대 좌표에 Δp 오프셋을 적용한다 (PDF p.11).
        /// 결과 = (absPos.X - dpx, absPos.Y - dpy)
        /// </summary>
        public static Point2D ApplyPcOffset(Point2D absPos, double dpx, double dpy)
        {
            return Point2D.of(absPos.X - dpx, absPos.Y - dpy);
        }

        /// <summary>
        /// 기준점을 중심으로 점을 특정 각도만큼 회전한다 (PDF p.13).
        /// result = R(Θ) · (point - pivot) + pivot
        /// </summary>
        public static Point2D RotateAroundPivot(Point2D point, Point2D pivot, double thetaRad)
        {
            double dx = point.X - pivot.X;
            double dy = point.Y - pivot.Y;
            var rotated = ApplyRotation(Point2D.of(dx, dy), thetaRad);
            return Point2D.of(rotated.X + pivot.X, rotated.Y + pivot.Y);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  5. 각도 계산 (atan2 기반)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 두 점이 이루는 벡터의 절대 각도.
        /// Θ = atan2(right.Y - left.Y, right.X - left.X)
        /// </summary>
        public static double ComputeLineAngle(Point2D left, Point2D right)
        {
            return Math.Atan2(right.Y - left.Y, right.X - left.X);
        }

        /// <summary>
        /// 두 벡터 AB → CD 사이 방향 포함 각도를 atan2(cross, dot)으로 계산한다 (PDF p.12, 15).
        /// </summary>
        public static double ComputeAlignAngle(Point2D a, Point2D b, Point2D c, Point2D d)
        {
            double ux = b.X - a.X;
            double uy = b.Y - a.Y;
            double vx = d.X - c.X;
            double vy = d.Y - c.Y;

            double cross = ux * vy - uy * vx;
            double dot = ux * vx + uy * vy;
            return Math.Atan2(cross, dot);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  6. 검증 유틸리티
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 두 점 사이 거리를 계산한다.
        /// </summary>
        public static double Distance(Point2D a, Point2D b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 두 좌표 사이 거리가 허용 오차 이내인지 검증한다.
        /// </summary>
        /// <param name="expected">기대 좌표</param>
        /// <param name="actual">실측 좌표</param>
        /// <param name="toleranceUm">허용 오차 (µm)</param>
        /// <returns>오차 이내이면 true</returns>
        public static bool VerifyPositionStability(Point2D expected, Point2D actual, double toleranceUm = 1.0)
        {
            return Distance(expected, actual) <= toleranceUm;
        }
    }
}
