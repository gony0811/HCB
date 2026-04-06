using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public class CalibrationMath
    {
        /// <param name="hxA">Stage 이동값 HX(A) = WY(A)</param>
        /// <param name="hcX">카메라 비전 좌표계 X 측정값 HcX(A)</param>
        /// <param name="hcY">카메라 비전 좌표계 Y 측정값 HcY(A)</param>
        /// <returns>카메라 틀어짐 각도 Θ (단위: radian)</returns>
        public static double ComputeCameraTheta(double hxA, double hcX, double hcY)
        {
            // (HcX / HX)² = 1 + sin2Θ  →  sin2Θ = (HcX/HX)² - 1
            // (HcY / HX)² = 1 - sin2Θ  →  sin2Θ = 1 - (HcY/HX)²
            // 두 식의 평균으로 안정적인 sin2Θ 추출

            double ratioX = hcX / hxA;
            double ratioY = hcY / hxA;

            double sin2Theta_fromX = (ratioX * ratioX) - 1.0;
            double sin2Theta_fromY = 1.0 - (ratioY * ratioY);

            double sin2Theta = (sin2Theta_fromX + sin2Theta_fromY) / 2.0;

            // sin2Θ 범위 클램프 [-1, 1]
            sin2Theta = Math.Clamp(sin2Theta, -1.0, 1.0);

            // 2Θ = asin(sin2Θ)  →  Θ = asin(sin2Θ) / 2
            double theta = Math.Asin(sin2Theta) / 2.0;

            return theta;
        }

        /// <summary>
        /// 회전 좌표계 원점 HcRO = (HX(C), WY(C)) 계산.
        /// </summary>
        public static Point2D ComputeHcRO(
            Point2D hc1Before,
            Point2D hc1After,
            Point2D hc2Before,
            Point2D hc2After)
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

            return Point2D.of((k1 * u2y - k2 * u1y) / d, (k2 * u1x - k1 * u2x) / d);
        }

        /// <summary>
        /// 비전 좌표 → 회전 행렬 적용 (Stage 위치 덧셈 제외)
        /// [HX]   [cosΘ  -sinΘ] [VisionX]
        /// [WY] = [sinΘ   cosΘ] [VisionY]
        /// </summary>
        /// <param name="vision">카메라 비전 좌표계 측정값</param>
        /// <param name="thetaRad">카메라 틀어짐 각도 (radian)</param>
        /// <returns>회전 행렬 적용된 좌표 (Stage 위치 미포함)</returns>
        public static Point2D ApplyRotation(Point2D vision, double thetaRad)
        {
            double cos = Math.Cos(thetaRad);
            double sin = Math.Sin(thetaRad);

            return Point2D.of(cos * vision.X - sin * vision.Y, sin * vision.X + cos * vision.Y);
        }
    }
}