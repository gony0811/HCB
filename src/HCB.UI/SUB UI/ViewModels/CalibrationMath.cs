using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public class CalibrationMath
    {
        // ══════════════════════════════════════════════════════════════════
        // 기본 유틸리티
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 스테이지 절대 좌표와 오프셋으로 원점을 계산합니다.
        /// </summary>
        public static Point2D Origin(Point2D stage, Point2D offset)
        {
            return Point2D.of(stage.X + offset.X, stage.Y + offset.Y);
        }

        /// <summary>
        /// 두 점의 상대 좌표를 계산합니다. (p1 - p2)
        /// </summary>
        public static Point2D CenterXY(Point2D p1, Point2D p2)
        {
            return Point2D.of(p1.X - p2.X, p1.Y - p2.Y);
        }

        /// <summary>
        /// 두 점 사이의 각도를 계산합니다. (atan2)
        /// </summary>
        public static double GetTheta(Point2D p1, Point2D p2)
        {
            Point2D relativePos = CenterXY(p2, p1);
            return Math.Atan2(relativePos.Y, relativePos.X);
        }

        /// <summary>
        /// 회전 변환 행렬을 적용합니다.
        /// [cosΘ -sinΘ] [x]   [originX]
        /// [sinΘ  cosΘ] [y] + [originY]
        /// </summary>
        private static Point2D ApplyRotation(double theta, Point2D point, Point2D origin)
        {
            double cosT = Math.Cos(theta);
            double sinT = Math.Sin(theta);

            return Point2D.of(
                cosT * point.X - sinT * point.Y + origin.X,
                sinT * point.X + cosT * point.Y + origin.Y
            );
        }

        // ══════════════════════════════════════════════════════════════════
        // [Page 3] Hc Vision 각도 계산 (Θ1, Θ2)
        //   HX(A) = WY(A) 조건으로 이동 후 비전 측정값으로 Θ 산출
        //   (HcX/HX)² = 1 + sin2Θ
        //   (HcY/HX)² = 1 - sin2Θ
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Hc 카메라의 절대 좌표계 기준 각도 Θ를 계산합니다.
        /// HX(A) == WY(A) 조건(임의 이동값이 동일)으로 측정한 값을 입력합니다.
        /// </summary>
        /// <param name="hxA">절대 좌표계 이동값 (HX(A) == WY(A) 조건)</param>
        /// <param name="hcVision">해당 이동 후 Hc 비전 좌표계 측정값 (X, Y)</param>
        /// <returns>각도 Θ (라디안)</returns>
        public static double CalcCameraAngle(double hxA, Point2D hcVision)
        {
            if (Math.Abs(hxA) < double.Epsilon)
                throw new ArgumentException("HX(A)는 0이 될 수 없습니다 (나눗셈 오류).", nameof(hxA));

            double ratioX = hcVision.X / hxA;
            double ratioY = hcVision.Y / hxA;

            // (HcX/HX)² = 1 + sin2Θ  =>  sin2Θ = (HcX/HX)² - 1
            // (HcY/HX)² = 1 - sin2Θ  =>  sin2Θ = 1 - (HcY/HX)²
            // 두 식의 평균으로 측정 노이즈 완화
            double sin2Theta = ((ratioX * ratioX - 1.0) + (1.0 - ratioY * ratioY)) / 2.0;

            // 수치 오차 클램핑 [-1, 1]
            sin2Theta = Math.Max(-1.0, Math.Min(1.0, sin2Theta));

            // 2Θ = asin(sin2Θ)  =>  Θ = asin(sin2Θ) / 2
            return Math.Asin(sin2Theta) / 2.0;
        }

        /// <summary>
        /// Hc1, Hc2 두 카메라의 절대 좌표계 기준 각도를 각각 계산합니다.
        /// </summary>
        /// <param name="hxA">임의 이동값 (HX(A) == WY(A) 조건)</param>
        /// <param name="hc1Vision">Hc1 비전 좌표계 측정값</param>
        /// <param name="hc2Vision">Hc2 비전 좌표계 측정값</param>
        /// <param name="theta1">Hc1 각도 Θ1 (라디안, out)</param>
        /// <param name="theta2">Hc2 각도 Θ2 (라디안, out)</param>
        public static void CalcBothCameraAngles(
            double hxA,
            Point2D hc1Vision, Point2D hc2Vision,
            out double theta1, out double theta2)
        {
            theta1 = CalcCameraAngle(hxA, hc1Vision);
            theta2 = CalcCameraAngle(hxA, hc2Vision);
        }

        // ══════════════════════════════════════════════════════════════════
        // [Page 4] BH Fiducial Mark 절대 좌표계값 생성
        //   [HX(Hc+)]   [cosΘ  -sinΘ] [HcX(+)]   [HX(Hc)]
        //   [WY(Hc+)] = [sinΘ   cosΘ] [HcY(+)] + [WY(Hc)]
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// BH Fiducial Mark의 절대 좌표계(HX, WY) 측정값을 생성합니다.
        /// </summary>
        /// <param name="theta">카메라 각도 Θ (라디안)</param>
        /// <param name="hcMark">Fiducial Mark의 Hc 비전 좌표계 측정값</param>
        /// <param name="hcOrigin">Hc 카메라 원점의 절대 좌표계값 (HX, WY)</param>
        /// <returns>Fiducial Mark의 절대 좌표계 Point2D (HX, WY)</returns>
        public static Point2D CalcFiducialAbsolutePosition(
            double theta, Point2D hcMark, Point2D hcOrigin)
        {
            return ApplyRotation(theta, hcMark, hcOrigin);
        }

        /// <summary>
        /// Hc1, Hc2 각각의 BH Fiducial Mark 절대 좌표계값을 생성합니다.
        /// </summary>
        /// <param name="theta1">Hc1 카메라 각도 Θ1 (라디안)</param>
        /// <param name="theta2">Hc2 카메라 각도 Θ2 (라디안)</param>
        /// <param name="hc1Mark">Hc1 비전 좌표계의 Fiducial Mark 측정값</param>
        /// <param name="hc2Mark">Hc2 비전 좌표계의 Fiducial Mark 측정값</param>
        /// <param name="hc1Origin">Hc1 카메라 원점의 절대 좌표계값</param>
        /// <param name="hc2Origin">Hc2 카메라 원점의 절대 좌표계값</param>
        /// <param name="hc1Result">Hc1 Fiducial Mark 절대 좌표계 결과 (out)</param>
        /// <param name="hc2Result">Hc2 Fiducial Mark 절대 좌표계 결과 (out)</param>
        public static void CalcBothFiducialPositions(
            double theta1, double theta2,
            Point2D hc1Mark, Point2D hc2Mark,
            Point2D hc1Origin, Point2D hc2Origin,
            out Point2D hc1Result, out Point2D hc2Result)
        {
            hc1Result = CalcFiducialAbsolutePosition(theta1, hc1Mark, hc1Origin);
            hc2Result = CalcFiducialAbsolutePosition(theta2, hc2Mark, hc2Origin);
        }

        // ══════════════════════════════════════════════════════════════════
        // [Page 5~6] HcRO 회전 중심 계산 (연립방정식 해)
        //   BH를 Θ만큼 회전 전후의 Hc1, Hc2 Fiducial Mark 절대 좌표로
        //   회전 원점 HcRO = (HX(C), WY(C)) 산출
        //
        //   k1 = { (HX'(Hc1+))²-(HX(Hc1+))² + (WY'(Hc1+))²-(WY(Hc1+))² } / 2
        //   k2 = { (HX'(Hc2+))²-(HX(Hc2+))² + (WY'(Hc2+))²-(WY(Hc2+))² } / 2
        //   D  = (HX'(Hc1+)-HX(Hc1+))(WY'(Hc2+)-WY(Hc2+))
        //       -(WY'(Hc1+)-WY(Hc1+))(HX'(Hc2+)-HX(Hc2+))
        //   HX(C) = { k1(WY'(Hc2+)-WY(Hc2+)) - k2(WY'(Hc1+)-WY(Hc1+)) } / D
        //   WY(C) = { k2(HX'(Hc1+)-HX(Hc1+)) - k1(HX'(Hc2+)-HX(Hc2+)) } / D
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// BH 회전 전후 Fiducial Mark 절대 좌표로 HcRO 회전 중심을 계산합니다.
        /// </summary>
        /// <param name="hc1Before">회전 전 Hc1 Fiducial Mark 절대 좌표 (HX(Hc1+), WY(Hc1+))</param>
        /// <param name="hc1After">회전 후 Hc1 Fiducial Mark 절대 좌표 (HX'(Hc1+), WY'(Hc1+))</param>
        /// <param name="hc2Before">회전 전 Hc2 Fiducial Mark 절대 좌표 (HX(Hc2+), WY(Hc2+))</param>
        /// <param name="hc2After">회전 후 Hc2 Fiducial Mark 절대 좌표 (HX'(Hc2+), WY'(Hc2+))</param>
        /// <returns>HcRO 회전 중심 (HX(C), WY(C))</returns>
        public static Point2D CalcHcRotationCenter(
            Point2D hc1Before, Point2D hc1After,
            Point2D hc2Before, Point2D hc2After)
        {
            // 변위 벡터
            double u1x = hc1After.X - hc1Before.X;
            double u1y = hc1After.Y - hc1Before.Y;
            double u2x = hc2After.X - hc2Before.X;
            double u2y = hc2After.Y - hc2Before.Y;

            // k1, k2
            double k1 = (hc1After.X * hc1After.X - hc1Before.X * hc1Before.X
                       + hc1After.Y * hc1After.Y - hc1Before.Y * hc1Before.Y) / 2.0;
            double k2 = (hc2After.X * hc2After.X - hc2Before.X * hc2Before.X
                       + hc2After.Y * hc2After.Y - hc2Before.Y * hc2Before.Y) / 2.0;

            // 결정식 D
            double D = u1x * u2y - u1y * u2x;
            if (Math.Abs(D) < double.Epsilon)
                throw new InvalidOperationException(
                    "D=0: 두 Fiducial Mark가 같은 방향으로 이동했습니다. 회전 중심을 계산할 수 없습니다.");

            return Point2D.of(
                (k1 * u2y - k2 * u1y) / D,  // HX(C)
                (k2 * u1x - k1 * u2x) / D   // WY(C)
            );
        }

        // ══════════════════════════════════════════════════════════════════
        // [Page 7] HcRO 좌표계 기준 측정값 환산
        //   Hc1/Hc2 FoV 내 P, Q를 HcRO 좌표계로 환산
        //   [HcROHX(P)]   [cosΘ1 -sinΘ1] [Hc1X(P)]   [HX(Hc1)]   [HX(C)]
        //   [HcROWY(P)] = [sinΘ1  cosΘ1] [Hc1Y(P)] + [WY(Hc1)] - [WY(C)]
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Hc 비전 좌표계 측정값을 HcRO 좌표계로 환산합니다.
        /// </summary>
        /// <param name="theta">해당 Hc 카메라 각도 (라디안)</param>
        /// <param name="hcVisionMark">Hc 비전 좌표계 Mark 측정값</param>
        /// <param name="hcOrigin">해당 Hc 카메라 원점 절대 좌표</param>
        /// <param name="hcRoCenter">HcRO 회전 중심 (HX(C), WY(C))</param>
        /// <returns>HcRO 좌표계 측정값</returns>
        public static Point2D ConvertToHcROCoord(
            double theta,
            Point2D hcVisionMark,
            Point2D hcOrigin,
            Point2D hcRoCenter)
        {
            // R(Θ)·HcMark + HcOrigin - HcROCenter
            Point2D abs = ApplyRotation(theta, hcVisionMark, hcOrigin);
            return Point2D.of(abs.X - hcRoCenter.X, abs.Y - hcRoCenter.Y);
        }

        // ══════════════════════════════════════════════════════════════════
        // [Page 8~9] Pc 카메라 Vision 각도 계산 (Θp) 및 절대 좌표 생성
        //   HX(+) = PY(+) 조건 사용, Hc와 동일한 수식 구조
        //   [HX(+)]   [cosΘp -sinΘp] [PcX(+)]   [HX(Pc)]
        //   [PY(+)] = [sinΘp  cosΘp] [PcY(+)] + [PY(Pc)]
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Pc 카메라의 절대 좌표계 기준 각도 Θp를 계산합니다.
        /// HX(+) == PY(+) 조건으로 측정한 값을 입력합니다.
        /// </summary>
        /// <param name="hxPlus">임의 이동값 (HX(+) == PY(+) 조건)</param>
        /// <param name="pcVision">Pc 비전 좌표계 측정값 (PcX, PcY)</param>
        /// <returns>각도 Θp (라디안)</returns>
        public static double CalcPcCameraAngle(double hxPlus, Point2D pcVision)
        {
            // Hc와 동일한 수식 구조 (축 이름만 다름)
            return CalcCameraAngle(hxPlus, pcVision);
        }

        /// <summary>
        /// Pc 카메라로 측정한 Mark의 절대 좌표계값을 생성합니다.
        /// BH Fiducial Mark 및 T-Die Align Mark 모두 동일하게 적용됩니다.
        /// </summary>
        /// <param name="thetaP">Pc 카메라 각도 Θp (라디안)</param>
        /// <param name="pcMark">Pc 비전 좌표계 Mark 측정값 (PcX, PcY)</param>
        /// <param name="pcOrigin">Pc 카메라 원점의 절대 좌표계값 (HX(Pc), PY(Pc))</param>
        /// <returns>Mark의 절대 좌표계값 (HX, PY)</returns>
        public static Point2D CalcPcMarkAbsolutePosition(
            double thetaP, Point2D pcMark, Point2D pcOrigin)
        {
            return ApplyRotation(thetaP, pcMark, pcOrigin);
        }

        // ══════════════════════════════════════════════════════════════════
        // [Page 11] Pc 절대 좌표 → HcRO 좌표계 오프셋 계산 및 적용
        //   Δpx = HX(L+) - HcROHX(L+)
        //   Δpy = PY(L+) - HcROWY(L+)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Pc 절대 좌표계와 HcRO 좌표계 사이의 오프셋을 계산합니다.
        /// L+ (Left BH Fiducial Mark)의 두 좌표계값 차이를 기준으로 산출합니다.
        /// </summary>
        /// <param name="leftMarkAbsolute">L+ Mark의 Pc 절대 좌표계값 (HX(L+), PY(L+))</param>
        /// <param name="leftMarkHcRO">L+ Mark의 HcRO 좌표계값 (HcROHX(L+), HcROWY(L+))</param>
        /// <returns>오프셋 (Δpx, Δpy)</returns>
        public static Point2D CalcPcToHcROOffset(Point2D leftMarkAbsolute, Point2D leftMarkHcRO)
        {
            return Point2D.of(
                leftMarkAbsolute.X - leftMarkHcRO.X,  // Δpx
                leftMarkAbsolute.Y - leftMarkHcRO.Y   // Δpy
            );
        }

        /// <summary>
        /// Pc 절대 좌표계 Mark를 HcRO 좌표계로 이동합니다.
        /// </summary>
        /// <param name="markAbsolute">Pc 절대 좌표계 Mark</param>
        /// <param name="offset">CalcPcToHcROOffset으로 산출한 오프셋 (Δpx, Δpy)</param>
        /// <returns>HcRO 좌표계로 이동된 Mark 좌표</returns>
        public static Point2D ApplyPcToHcROOffset(Point2D markAbsolute, Point2D offset)
        {
            return Point2D.of(markAbsolute.X - offset.X, markAbsolute.Y - offset.Y);
        }

        // ══════════════════════════════════════════════════════════════════
        // [Page 12] Pc 절대 좌표계와 HcRO 좌표계 사이 각도 Θ+ 계산
        //   L+, R+ 두 점의 Pc 절대좌표와 HcRO 좌표를 이용해 atan2로 각도 산출
        //   u = HcRO의 L+→R+ 벡터
        //   v = Pc 절대좌표(오프셋 적용) L+ 기준 R+ 벡터
        //   cross = ux*vy - uy*vx  (2D 외적)
        //   dot   = ux*vx + uy*vy  (내적)
        //   Θ+ = atan2(cross, dot)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Pc 절대 좌표계와 HcRO 좌표계 사이 각도 Θ+를 계산합니다.
        /// </summary>
        /// <param name="rightMarkAbsolute">R+ Mark의 Pc 절대 좌표 (HX(R+), PY(R+))</param>
        /// <param name="leftMarkHcRO">L+ Mark의 HcRO 좌표 (HcROHX(L+), HcROWY(L+))</param>
        /// <param name="rightMarkHcRO">R+ Mark의 HcRO 좌표 (HcROHX(R+), HcROWY(R+))</param>
        /// <param name="offset">Pc→HcRO 오프셋 (Δpx, Δpy)</param>
        /// <returns>각도 Θ+ (라디안)</returns>
        public static double CalcPcHcROAngle(
            Point2D rightMarkAbsolute,
            Point2D leftMarkHcRO, Point2D rightMarkHcRO,
            Point2D offset)
        {
            // u = HcRO 좌표계의 L+→R+ 벡터
            double ux = rightMarkHcRO.X - leftMarkHcRO.X;
            double uy = rightMarkHcRO.Y - leftMarkHcRO.Y;

            // v = Pc 절대 좌표(오프셋 적용)에서 L+ 기준 R+ 방향 벡터
            double vx = rightMarkAbsolute.X - offset.X - leftMarkHcRO.X;
            double vy = rightMarkAbsolute.Y - offset.Y - leftMarkHcRO.Y;

            return Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
        }

        // ══════════════════════════════════════════════════════════════════
        // [Page 13] T-Die Mark를 Θ+ 각도 회전하여 HcRO 좌표계로 환산
        //   L+ 기준 상대좌표로 Θ+ 회전 후 L+ 기준점 복원
        //   [HcROHX(LT)]   [cosΘ+ -sinΘ+] [HX(LT)-Δpx-HcROHX(L+)]   [HcROHX(L+)]
        //   [HcROWY(LT)] = [sinΘ+  cosΘ+] [PY(LT)-Δpy-HcROWY(L+)] + [HcROWY(L+)]
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// T-Die Mark를 Θ+ 회전하여 HcRO 좌표계로 환산합니다.
        /// </summary>
        /// <param name="thetaPlus">Pc-HcRO 사이 각도 Θ+</param>
        /// <param name="tDieMark">T-Die Mark Pc 절대 좌표 (HX(LT/RT), PY(LT/RT))</param>
        /// <param name="offset">Pc→HcRO 오프셋 (Δpx, Δpy)</param>
        /// <param name="leftMarkHcRO">L+ Mark의 HcRO 좌표 (기준점)</param>
        /// <returns>HcRO 좌표계의 T-Die Mark 좌표</returns>
        public static Point2D ConvertTDieMarkToHcRO(
            double thetaPlus,
            Point2D tDieMark,
            Point2D offset,
            Point2D leftMarkHcRO)
        {
            // L+ 기준 상대 좌표 (오프셋 적용)
            Point2D relative = Point2D.of(
                tDieMark.X - offset.X - leftMarkHcRO.X,
                tDieMark.Y - offset.Y - leftMarkHcRO.Y
            );

            // Θ+ 회전 후 L+ 기준점 복원
            return ApplyRotation(thetaPlus, relative, leftMarkHcRO);
        }

        // ══════════════════════════════════════════════════════════════════
        // [Page 15] 얼라인 Angle Θo 계산 (T-Die ↔ B-Die 각도)
        //   u = AB = LB→RB (B-Die 방향 벡터)
        //   v = CD = LT→RT (T-Die 방향 벡터)
        //   Θo = atan2(cross, dot)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// T-Die와 B-Die 사이 얼라인 각도 Θo를 계산합니다.
        /// </summary>
        /// <param name="lb">B-Die Left Mark HcRO 좌표 (HcROHX(LB), HcROWY(LB))</param>
        /// <param name="rb">B-Die Right Mark HcRO 좌표 (HcROHX(RB), HcROWY(RB))</param>
        /// <param name="lt">T-Die Left Mark HcRO 좌표 (HcROHX(LT), HcROWY(LT))</param>
        /// <param name="rt">T-Die Right Mark HcRO 좌표 (HcROHX(RT), HcROWY(RT))</param>
        /// <returns>얼라인 각도 Θo (라디안, AB→CD 방향 포함)</returns>
        public static double CalcAlignAngle(Point2D lb, Point2D rb, Point2D lt, Point2D rt)
        {
            // u = AB = LB→RB (B-Die 방향 벡터)
            double ux = rb.X - lb.X;
            double uy = rb.Y - lb.Y;

            // v = CD = LT→RT (T-Die 방향 벡터)
            double vx = rt.X - lt.X;
            double vy = rt.Y - lt.Y;

            return Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
        }

        // ══════════════════════════════════════════════════════════════════
        // [Page 16] 각도 보정 후 T-Die 좌표값 추출
        //   Θf = Θo - Θs  (얼라인 Angle)
        //   [HcROHX'(LT)]   [cosΘf -sinΘf] [HcROHX(LT)]
        //   [HcROWY'(LT)] = [sinΘf  cosΘf] [HcROWY(LT)]
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 얼라인 각도 Θf를 계산합니다. (Θf = Θo - Θs)
        /// </summary>
        /// <param name="thetaO">T-Die/B-Die 각도 연산값 Θo</param>
        /// <param name="thetaS">도면 기준 B-Die Mark → T-Die 각도 Θs (방향 포함)</param>
        /// <returns>얼라인 보정 각도 Θf</returns>
        public static double CalcAlignCorrectionAngle(double thetaO, double thetaS)
        {
            return thetaO - thetaS;
        }

        /// <summary>
        /// Θf 회전 후 T-Die Mark의 HcRO 좌표값을 추출합니다.
        /// </summary>
        /// <param name="thetaF">얼라인 보정 각도 Θf</param>
        /// <param name="tDieMarkHcRO">회전 전 T-Die Mark HcRO 좌표</param>
        /// <returns>회전 후 T-Die Mark HcRO 좌표 (HcROHX', HcROWY')</returns>
        public static Point2D RotateTDieMarkByAlignAngle(double thetaF, Point2D tDieMarkHcRO)
        {
            // 원점 (0, 0) 기준 회전 (오프셋 없음)
            return ApplyRotation(thetaF, tDieMarkHcRO, Point2D.of(0, 0));
        }

        // ══════════════════════════════════════════════════════════════════
        // [Page 17] 얼라인 Shift 연산
        //   HcROHX(Xf) = HcROHX'(LT) - HcROHX(LB) - Xs
        //   HcROWY(Yf) = HcROWY'(LT) - HcROWY(LB) - Ys
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 얼라인 Shift량을 계산합니다.
        /// 현 HX, WY 위치에서 이 값만큼 조정 후 T-Die와 B-Die 최종 본딩 진행합니다.
        /// </summary>
        /// <param name="ltAfterRotation">Θf 회전 후 T-Die LT Mark HcRO 좌표 (HcROHX'(LT), HcROWY'(LT))</param>
        /// <param name="lb">B-Die LB Mark HcRO 좌표 (HcROHX(LB), HcROWY(LB))</param>
        /// <param name="schematicOffset">도면 기준 상대 위치값 (Xs, Ys) = T-Die - B-Die</param>
        /// <returns>얼라인 Shift량 (HcROHX(Xf), HcROWY(Yf))</returns>
        public static Point2D CalcAlignShift(
            Point2D ltAfterRotation,
            Point2D lb,
            Point2D schematicOffset)
        {
            return Point2D.of(
                ltAfterRotation.X - lb.X - schematicOffset.X,  // Xf
                ltAfterRotation.Y - lb.Y - schematicOffset.Y   // Yf
            );
        }
    }
}