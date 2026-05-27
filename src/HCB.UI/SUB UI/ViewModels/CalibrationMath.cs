using System;
using System.Collections.Generic;
using static HCB.UI.SERVICE.CalibrationService;

namespace HCB.UI
{
    public static class CalibrationMath
    {
        public static double Dist(Point2D a, Point2D b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
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
        public static double ComputeCameraTheta2(double moveX, double deltaX, double deltaY)
        {
            // X축만 이동한 경우
            // 관측: visionDx = moveX * cosθ, visionDy = moveX * sinθ
            // deltaY = visionDy - 0 = moveX * sinθ
            // deltaX = visionDx - moveX = moveX * (cosθ - 1) ≈ 0 (작은 각도)
            return Math.Atan2(deltaY, moveX + deltaX);
        }
        // ═══════════════════════════════════════════════════════════════════
        //  2. HcRO 회전 중심 산출
        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// 최소자승법 원 피팅 (Kåsa method)
        /// (x - cx)² + (y - cy)² = r² 에 대한 선형화 풀이
        /// 
        /// 전개: x² + y² = 2·cx·x + 2·cy·y + (r² - cx² - cy²)
        /// A·x + B·y + C = x² + y²  형태의 연립방정식을 최소자승으로 풂
        /// </summary>
        public static Point2D FitCircleCenter(List<Point2D> points)
        {
            if (points.Count < 3)
                throw new ArgumentException("최소 3개 이상의 점이 필요합니다.");

            // ── 수치 안정성을 위한 중심 이동 ──
            double meanX = 0, meanY = 0;
            foreach (var p in points) { meanX += p.X; meanY += p.Y; }
            meanX /= points.Count;
            meanY /= points.Count;

            // ── 정규 방정식 구성 ──
            // A·[cx', cy'] = b  (2x2 시스템)
            // 여기서 cx' = cx - meanX, cy' = cy - meanY
            double Suu = 0, Suv = 0, Svv = 0;
            double Suuu = 0, Suvv = 0, Svvv = 0, Svuu = 0;

            foreach (var p in points)
            {
                double u = p.X - meanX;
                double v = p.Y - meanY;
                Suu += u * u;
                Suv += u * v;
                Svv += v * v;
                Suuu += u * u * u;
                Suvv += u * v * v;
                Svvv += v * v * v;
                Svuu += v * u * u;
            }

            // 연립방정식:
            // Suu·cx' + Suv·cy' = (Suuu + Suvv) / 2
            // Suv·cx' + Svv·cy' = (Svvv + Svuu) / 2
            double rhs1 = (Suuu + Suvv) / 2.0;
            double rhs2 = (Svvv + Svuu) / 2.0;

            double det = Suu * Svv - Suv * Suv;
            if (Math.Abs(det) < 1e-20)
                throw new InvalidOperationException(
                    "원 피팅 실패: 점들이 일직선 위에 있습니다.");

            double cx = (rhs1 * Svv - rhs2 * Suv) / det + meanX;
            double cy = (rhs2 * Suu - rhs1 * Suv) / det + meanY;

            return Point2D.of(cx, cy);
        }

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

        public static Point2D ComputeHcRO2(Point2D p1, Point2D p1_prime, Point2D p2, Point2D p2_prime)
        {
            // 각 선분의 중점 구하기
            double m1x = (p1.X + p1_prime.X) / 2.0;
            double m1y = (p1.Y + p1_prime.Y) / 2.0;
            double m2x = (p2.X + p2_prime.X) / 2.0;
            double m2y = (p2.Y + p2_prime.Y) / 2.0;

            // 각 선분의 기울기(벡터)의 수직 방향 벡터 구하기
            double v1x = -(p1_prime.Y - p1.Y);
            double v1y = p1_prime.X - p1.X;
            double v2x = -(p2_prime.Y - p2.Y);
            double v2y = p2_prime.X - p2.X;

            // 두 수직이등분선의 교점 계산 (연립 방정식)
            double det = v1x * v2y - v1y * v2x;
            if (Math.Abs(det) < 1e-6) throw new Exception("두 벡터가 평행합니다.");

            double t = ((m2x - m1x) * v2y - (m2y - m1y) * v2x) / det;

            return Point2D.of(m1x + t * v1x, m1y + t * v1y);
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
        public static Point2D ApplyRotation(VisionMarkResult point, double thetaRad)
        {
            double cos = Math.Cos(thetaRad);
            double sin = Math.Sin(thetaRad);
            return Point2D.of(
                cos * point.DxCamToMark - sin * point.DyCamToMark,
                sin * point.DxCamToMark + cos * point.DyCamToMark);
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
            // u = a→b (기준벡터), v = c→d (측정벡터)
            // Θ = atan2(u×v, u·v)  → u에서 v로의 회전각
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

        // Degree를 Radian으로
        public static double ToRadian(this double degree)
        {
            return degree * Math.PI / 180.0;
        }

        // Radian을 Degree로
        public static double ToDegree(this double radian)
        {
            return radian * 180.0 / Math.PI;
        }
    }
}
