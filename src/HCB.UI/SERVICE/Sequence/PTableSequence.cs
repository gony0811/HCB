using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static HCB.UI.SERVICE.CalibrationService;

namespace HCB.UI
{
    public partial class SequenceService : BackgroundService
    {

        public async Task<Dictionary<string, VisionMarkResult>> TopDieVision(CancellationToken ct)
        {

            try
            {
                _logger.Information("Top Die Vision Start");

                EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생
                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
                
                var result = false;

                string[] xy = { MotionExtensions.P_Y, MotionExtensions.H_X };
                string[] z = { MotionExtensions.H_Z };

                VisionMarkResult rightFid = new VisionMarkResult
                {
                    MarkType = MarkType.FIDUCIAL,
                    DirectType = DirectType.RIGHT,
                    StageX = await GetPosition(MotionExtensions.H_X, MotionExtensions.P_RIGHT_HIGH, ct),
                    StageY = await GetPosition(MotionExtensions.P_Y, MotionExtensions.P_RIGHT_HIGH, ct),
                };

                VisionMarkResult rightAlign = new VisionMarkResult
                {
                    MarkType = MarkType.ALIGN_MARK,
                    DirectType = DirectType.RIGHT,
                    StageX = rightFid.StageX,
                    StageY = rightFid.StageY,
                };

                VisionMarkResult leftFid = new VisionMarkResult
                {
                    MarkType = MarkType.FIDUCIAL,
                    DirectType = DirectType.LEFT,
                    StageX = await GetPosition(MotionExtensions.H_X, MotionExtensions.P_LEFT_HIGH, ct),
                    StageY = await GetPosition(MotionExtensions.P_Y, MotionExtensions.P_LEFT_HIGH, ct),
                };

                VisionMarkResult leftAlign = new VisionMarkResult
                {
                    MarkType = MarkType.ALIGN_MARK,
                    DirectType = DirectType.LEFT,
                    StageX = leftFid.StageX,
                    StageY = leftFid.StageY,
                };

                Dictionary<string, VisionMarkResult> visionResults = new Dictionary<string, VisionMarkResult>
                {
                    { "RIGHT_FID", rightFid },
                    { "RIGHT_ALIGN", rightAlign },
                    { "LEFT_FID", leftFid },
                    { "LEFT_ALIGN", leftAlign }
                };

                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동

                // 우측 피듀셜마크 이동                
                await MotionsMove(xy, MotionExtensions.P_RIGHT_HIGH, ct);
                await MotionsMove(z, MotionExtensions.P_RIGHT_FIDUCIAL_HIGH, ct);

                result =  await communicationService.RequestAFStart(CameraType.PC_HIGH, markType: MarkType.FIDUCIAL, ct);
                if (result == false) throw new Exception("AF 실패");

                var rFidXY = await communicationService.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.PC_HIGH, "RIGHT");
                VisionResult(rFidXY);
                rightFid.DxCamToMark = rFidXY.X;
                rightFid.DyCamToMark = rFidXY.Y;

                // 우측 얼라인마크 이동
                await MotionsMove(z, MotionExtensions.P_RIGHT_ALIGN_HIGH, ct);
                result = await communicationService.RequestAFStart(CameraType.PC_HIGH, markType: MarkType.ALIGN_MARK, ct);
                if (result == false) throw new Exception("AF 실패");
                var rAlignXY = await communicationService.RequestVisionMarkPosition(MarkType.ALIGN_MARK, CameraType.PC_HIGH, "RIGHT");
                VisionResult(rAlignXY);
                rightAlign.DxCamToMark = rAlignXY.X;
                rightAlign.DyCamToMark = rAlignXY.Y;

                // 좌측 피듀셜마크 이동 
                await MotionsMove(xy, MotionExtensions.P_LEFT_HIGH, ct);
                await MotionsMove(z, MotionExtensions.P_LEFT_FIDUCIAL_HIGH, ct);
                result = await communicationService.RequestAFStart(CameraType.PC_HIGH, markType: MarkType.FIDUCIAL, ct);
                if (result == false) throw new Exception("AF 실패");
                var lFidXY = await communicationService.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.PC_HIGH, "LEFT");
                VisionResult(lFidXY);
                leftFid.DxCamToMark = lFidXY.X;
                leftFid.DyCamToMark = lFidXY.Y;

                // 좌측 얼라인마크 이동
                await MotionsMove(z, MotionExtensions.P_LEFT_ALIGN_HIGH, ct);
                result = await communicationService.RequestAFStart(CameraType.PC_HIGH, markType: MarkType.ALIGN_MARK, ct);
                if (result == false) throw new Exception("AF 실패");
                var lAlignXY = await communicationService.RequestVisionMarkPosition(MarkType.ALIGN_MARK, CameraType.PC_HIGH, "LEFT");
                VisionResult(lAlignXY);
                leftAlign.DxCamToMark = lAlignXY.X;
                leftAlign.DyCamToMark = lAlignXY.Y;

                return visionResults;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task<VisionMarkResult> TopDieVisionRightFid(CancellationToken ct)
        {
            try
            {
                _logger.Information("Top Die Vision Start");
                EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

                var result = false;
                VisionMarkResult rightFid = new VisionMarkResult
                {
                    MarkType = MarkType.FIDUCIAL,
                    DirectType = DirectType.RIGHT,
                    StageX = await GetPosition(MotionExtensions.H_X, MotionExtensions.P_RIGHT_HIGH, ct),
                    StageY = await GetPosition(MotionExtensions.P_Y, MotionExtensions.P_RIGHT_HIGH, ct),
                };

                string[] xy = { MotionExtensions.P_Y, MotionExtensions.H_X };
                string[] z = { MotionExtensions.H_Z };


                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동

                // 우측 피듀셜마크 이동
                await MotionsMove(xy, MotionExtensions.P_RIGHT_HIGH, ct);
                await MotionsMove(z, MotionExtensions.P_RIGHT_FIDUCIAL_HIGH, ct);
                result = await communicationService.RequestAFStart(CameraType.PC_HIGH, markType: MarkType.FIDUCIAL, ct);
                if (result == false) throw new Exception("AF 실패");
                var rFidXY = await communicationService.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.PC_HIGH, "RIGHT");
                VisionResult(rFidXY);
                rightFid.DxCamToMark = rFidXY.X;
                rightFid.DyCamToMark = rFidXY.Y;

                return rightFid;
            }
            catch(Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task<VisionMarkResult> TopDieVisionRightAlign(CancellationToken ct)
        {
            try
            {
                _logger.Information("Top Die Vision Start");
                EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

                var result = false;
                VisionMarkResult rightAlign = new VisionMarkResult
                {
                    MarkType = MarkType.ALIGN_MARK,
                    DirectType = DirectType.RIGHT,
                    StageX = await GetPosition(MotionExtensions.H_X, MotionExtensions.P_RIGHT_HIGH, ct),
                    StageY = await GetPosition(MotionExtensions.P_Y, MotionExtensions.P_RIGHT_HIGH, ct),
                };

                string[] xy = { MotionExtensions.P_Y, MotionExtensions.H_X };
                string[] z = { MotionExtensions.H_Z };

                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동
                await MotionsMove(xy, MotionExtensions.P_RIGHT_HIGH, ct);
                await MotionsMove(z, MotionExtensions.P_RIGHT_ALIGN_HIGH, ct);
                result = await communicationService.RequestAFStart(CameraType.PC_HIGH, markType: MarkType.ALIGN_MARK, ct);
                if (result == false) throw new Exception("AF 실패");
                var rAlignXY = await communicationService.RequestVisionMarkPosition(MarkType.ALIGN_MARK, CameraType.PC_HIGH, "RIGHT");
                VisionResult(rAlignXY);
                rightAlign.DxCamToMark = rAlignXY.X;
                rightAlign.DyCamToMark = rAlignXY.Y;

                return rightAlign;

            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task<VisionMarkResult> TopDieVisionLeftFid(CancellationToken ct)
        {
            try
            {
                _logger.Information("Top Die Vision Start");
                EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

                var result = false;
                VisionMarkResult leftFid = new VisionMarkResult
                {
                    MarkType = MarkType.FIDUCIAL,
                    DirectType = DirectType.LEFT,
                    StageX = await GetPosition(MotionExtensions.H_X, MotionExtensions.P_LEFT_HIGH, ct),
                    StageY = await GetPosition(MotionExtensions.P_Y, MotionExtensions.P_LEFT_HIGH, ct),
                };

                string[] xy = { MotionExtensions.P_Y, MotionExtensions.H_X };
                string[] z = { MotionExtensions.H_Z };


                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동

                // 좌측 피듀셜마크 이동
                await MotionsMove(xy, MotionExtensions.P_LEFT_HIGH, ct);
                await MotionsMove(z, MotionExtensions.P_LEFT_FIDUCIAL_HIGH, ct);
                result = await communicationService.RequestAFStart(CameraType.PC_HIGH, markType: MarkType.FIDUCIAL, ct);
                if (result == false) throw new Exception("AF 실패");
                var lFidXY = await communicationService.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.PC_HIGH, "LEFT");
                VisionResult(lFidXY);
                leftFid.DxCamToMark = lFidXY.X;
                leftFid.DyCamToMark = lFidXY.Y;
                return leftFid;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task<VisionMarkResult> TopDieVisionLeftAlign(CancellationToken ct)
        {
            try
            {
                _logger.Information("Top Die Vision Start");
                EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

                var result = false;
                VisionMarkResult leftAlign = new VisionMarkResult
                {
                    MarkType = MarkType.ALIGN_MARK,
                    DirectType = DirectType.LEFT,
                    StageX = await GetPosition(MotionExtensions.H_X, MotionExtensions.P_LEFT_HIGH, ct),
                    StageY = await GetPosition(MotionExtensions.P_Y, MotionExtensions.P_LEFT_HIGH, ct),
                };

                string[] xy = { MotionExtensions.P_Y, MotionExtensions.H_X };
                string[] z = { MotionExtensions.H_Z };


                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동

                // 좌측 얼라인마크 이동
                await MotionsMove(xy, MotionExtensions.P_LEFT_HIGH, ct);
                await MotionsMove(z, MotionExtensions.P_LEFT_ALIGN_HIGH, ct);
                result = await communicationService.RequestAFStart(CameraType.PC_HIGH, markType: MarkType.ALIGN_MARK, ct);
                if (result == false) throw new Exception("AF 실패");
                var lAlignXY = await communicationService.RequestVisionMarkPosition(MarkType.ALIGN_MARK, CameraType.PC_HIGH, "LEFT");
                VisionResult(lAlignXY);
                leftAlign.DxCamToMark = lAlignXY.X;
                leftAlign.DyCamToMark = lAlignXY.Y;
                return leftAlign;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }


        public void VisionResult(VisionMarkPositionResponse response)
        {
            if (response.Result == Result.NG) throw new Exception("비전 통신 에러");
        }

    }
}

