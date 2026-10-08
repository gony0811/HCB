# HCB Recipe / ECParameter 파라미터 정리

작성일: 2026-10-07

## 개요

Recipe는 제품(다이/웨이퍼)마다 바뀌는 값이고, ECParameter는 설비 고유의 캘리브레이션 값과 장비 상수입니다. 두 가지 모두 코드에 고정된 필드가 아닙니다. DB에 이름(Name)과 값(Value)이 쌍으로 저장된 행이고, 시퀀스가 **이름 문자열로 조회**합니다. 그래서 이름이 한 글자라도 다르면 런타임 예외가 나거나 기본값으로 동작합니다.

| 구분 | Recipe Param | Step Recipe | EC Param |
| --- | --- | --- | --- |
| 테이블 | `RecipeParam` | `StepRecipe` | `ECParam` |
| 엔티티 | `HCB/Data/Entity/RecipeParam.cs` | `HCB/Data/Entity/StepRecipe.cs` | `HCB/Data/Entity/ECParam.cs` |
| 소속 | Recipe 1개에 N개 (`RecipeId` FK, Cascade 삭제) | Recipe 1개에 N개 (가압 스텝) | 전역 (설비당 1세트) |
| 서비스 | `RecipeService` (SYSTEM/DB/Service) | `RecipeService.FindStepByName()` | `ECParamService` |
| 조회 대상 | 활성 레시피(`IsActive=1`, 1개만 허용)의 `ParamList` | 활성 레시피의 `StepList` | `ParamList` 전체 |
| 편집 화면 | USub02 (Parameter) | USub01 Step Seq 탭, USub02 | USub09 (EC Param), Calibration 탭에서 자동 저장 |

공통 컬럼(RecipeParam, ECParam):

| 컬럼 | 설명 |
| --- | --- |
| `Name` | 최대 100자, Unique |
| `Value` | 문자열 |
| `Minimum` / `Maximum` | 선택 |
| `ValueType` | Double, Integer 등 |
| `UnitType` | mm, None 등 |
| `Description` | 최대 500자 |

**로드 흐름:**

1. 앱이 시작되면 `RecipeService.Initialize()`가 Recipe와 ParamList, StepList를 메모리에 올립니다.
2. `IsActive`인 레시피가 `UseRecipe`로 지정됩니다.
3. `ECParamService.Initialize()`가 ECParam 전체를 메모리에 올립니다.
4. 이후 시퀀스는 DB가 아니라 메모리 DTO에서 값을 읽습니다.
5. 사용 레시피를 바꾸면(`SetUseRecipeAsync`) `VISION_RECIPE` 파라미터 값이 비전 PC로 전송됩니다.

## 명명 규칙

파라미터 이름은 **대문자 스네이크 케이스**(`TOP_DIE_THICKNESS`)로 통일합니다. 2026-10-07에 기존 이름을 아래와 같이 바꿨습니다. 기존 DB의 행은 앱 시작 시 마이그레이션 `RenameParamsToUpperSnake`가 자동으로 바꿉니다. 같은 레시피(또는 EC)에 새 이름 행이 이미 있으면 그 행은 바꾸지 않고 건너뜁니다.

| 이전 이름 | 새 이름 | 구분 |
| --- | --- | --- |
| `TopDieThickness` | `TOP_DIE_THICKNESS` | Recipe |
| `BtmDieThickness` | `BTM_DIE_THICKNESS` | Recipe |
| `WaferSize` | `WAFER_SIZE` | Recipe |
| `DieSizeX` | `DIE_SIZE_X` | Recipe |
| `DieSizeY` | `DIE_SIZE_Y` | Recipe |
| `GapX` | `GAP_X` | Recipe |
| `GapY` | `GAP_Y` | Recipe |
| `ScribeShiftX` | `SCRIBE_SHIFT_X` | Recipe |
| `ScribeShiftY` | `SCRIBE_SHIFT_Y` | Recipe |
| `AlignTopSpacingX` | `ALIGN_TOP_SPACING_X` | Recipe |
| `AlignTopSpacingY` | `ALIGN_TOP_SPACING_Y` | Recipe |
| `HC1 피듀셜 위치 보정 X` | `HC1_FID_OFFSET_X` | Recipe |
| `HC1 피듀셜 위치 보정 Y` | `HC1_FID_OFFSET_Y` | Recipe |
| `HC2 피듀셜 위치 보정 X` | `HC2_FID_OFFSET_X` | Recipe |
| `HC2 피듀셜 위치 보정 Y` | `HC2_FID_OFFSET_Y` | Recipe |
| `HcCenterErrorX` | `HC_CENTER_ERROR_X` | Recipe |
| `HcCenterErrorY` | `HC_CENTER_ERROR_Y` | Recipe |
| `xLowErrorOffset` | `LOW_ERROR_OFFSET_X` | Recipe |
| `yLowErrorOffset` | `LOW_ERROR_OFFSET_Y` | Recipe |
| `TopBtmGap` | `TOP_BTM_GAP` | Recipe |
| `BondingForce` | `BONDING_FORCE` | Recipe |
| `BondingTime` | `BONDING_TIME` | Recipe |
| `버니어_OFFSET_X` | `VERNIER_OFFSET_X` | Recipe |
| `버니어_OFFSET_Y` | `VERNIER_OFFSET_Y` | Recipe |
| `버니어_거리_X` | `VERNIER_DIST_X` | Recipe |
| `버니어_거리_Y` | `VERNIER_DIST_Y` | Recipe |
| `Vision Recipe` | `VISION_RECIPE` | Recipe |
| `ShankToWaferOffset` | `SHANK_TO_WAFER_OFFSET` | EC |
| `ShankToDieOffset` | `SHANK_TO_DIE_OFFSET` | EC |
| `ShankLowOffsetX` | `SHANK_LOW_OFFSET_X` | EC |
| `ShankLowOffsetY` | `SHANK_LOW_OFFSET_Y` | EC |
| `AlignDistTolerance` | `ALIGN_DIST_TOLERANCE` | EC |
| `Hc1FidRefDx` | `HC1_FID_REF_DX` | EC |
| `Hc1FidRefDy` | `HC1_FID_REF_DY` | EC |
| `Hc2FidRefDx` | `HC2_FID_REF_DX` | EC |
| `Hc2FidRefDy` | `HC2_FID_REF_DY` | EC |
| `LowVisionRetryMax` | `LOW_VISION_RETRY_MAX` | EC |
| `VisionRetryMax` | `VISION_RETRY_MAX` | EC |
| `VisionRetryStepMm` | `VISION_RETRY_STEP_MM` | EC |
| `BtmThetaSign` | `BTM_THETA_SIGN` | EC |
| `BtmThetaMinDeg` | `BTM_THETA_MIN_DEG` | EC |

## 레시피 종류별 필수 항목

레시피는 종류(DIE / WAFER)마다 필수 항목이 다릅니다. 기준은 [ParamCatalog.cs](../src/HCB.UI/SYSTEM/DB/Param/ParamCatalog.cs)의 `Req` 값(`Req.Die`, `Req.Wafer`, `Req.Both`)입니다.

- **DIE:** StepSeq 흐름(TopHighAlign → BtmHighAlign → 좌표 통합 → BondingCorr)에서 읽는 값
- **WAFER:** WaferSeq 흐름(웨이퍼 맵, ResultMeasurement/SimpleMeasurement)에서 읽는 값

레시피를 생성하면 그 종류의 필수 항목이 자동으로 추가됩니다. 보정 오프셋은 `0`으로, 두께와 위치 같은 물리값은 빈 값으로 들어가며 빈 값은 직접 입력해야 합니다. 기존 레시피에 빠진 필수 항목은 누락 경고의 [필수 항목 추가] 버튼으로 채울 수 있습니다.

| Name | 구역 | DIE | WAFER | 자동 추가 값 |
| --- | --- | --- | --- | --- |
| `TOP_DIE_THICKNESS` | 다이 / 웨이퍼 | 필수 | 필수 | 빈 값(직접 입력) |
| `BTM_DIE_THICKNESS` | 다이 / 웨이퍼 | 필수 | 필수 | 빈 값(직접 입력) |
| `TOP_DIE_SIZE` | 다이 / 웨이퍼 | 필수 |  | 빈 값(직접 입력) |
| `WAFER_SIZE` | 다이 / 웨이퍼 |  | 필수 | 빈 값(직접 입력) |
| `DIE_SIZE_X` | 다이 / 웨이퍼 |  | 필수 | 빈 값(직접 입력) |
| `DIE_SIZE_Y` | 다이 / 웨이퍼 |  | 필수 | 빈 값(직접 입력) |
| `GAP_X` | 다이 / 웨이퍼 |  | 필수 | 빈 값(직접 입력) |
| `GAP_Y` | 다이 / 웨이퍼 |  | 필수 | 빈 값(직접 입력) |
| `SCRIBE_SHIFT_X` | 다이 / 웨이퍼 |  | 필수 | 0 |
| `SCRIBE_SHIFT_Y` | 다이 / 웨이퍼 |  | 필수 | 0 |
| `ALIGN_TOP_SPACING_X` | 다이 / 웨이퍼 |  | 필수 | 빈 값(직접 입력) |
| `ALIGN_TOP_SPACING_Y` | 다이 / 웨이퍼 |  | 필수 | 빈 값(직접 입력) |
| `SPEC_X` | 판정 스펙 |  | 필수 | 빈 값(직접 입력) |
| `SPEC_Y` | 판정 스펙 |  | 필수 | 빈 값(직접 입력) |
| `SPEC_THETA` | 판정 스펙 | 필수 | 필수 | 0 |
| `FID_ALIGN_GAP` | 정렬 보정 | 필수 | 필수 | 빈 값(직접 입력) |
| `X_ALIGN_OFFSET` | 정렬 보정 | 필수 |  | 0 |
| `Y_ALIGN_OFFSET` | 정렬 보정 | 필수 |  | 0 |
| `T_ALIGN_OFFSET` | 정렬 보정 | 필수 |  | 0 |
| `HC1_FID_OFFSET_X` | 정렬 보정 | 필수 |  | 0 |
| `HC1_FID_OFFSET_Y` | 정렬 보정 | 필수 |  | 0 |
| `HC2_FID_OFFSET_X` | 정렬 보정 | 필수 |  | 0 |
| `HC2_FID_OFFSET_Y` | 정렬 보정 | 필수 |  | 0 |
| `HC_CENTER_ERROR_X` | 정렬 보정 | 필수 | 필수 | 0 |
| `HC_CENTER_ERROR_Y` | 정렬 보정 | 필수 | 필수 | 0 |
| `LOW_ERROR_OFFSET_X` | 정렬 보정 | 필수 |  | 0 |
| `LOW_ERROR_OFFSET_Y` | 정렬 보정 | 필수 |  | 0 |
| `READY_POSITION` | 모션 / 본딩 | 필수 |  | 빈 값(직접 입력) |
| `TOP_BTM_GAP` | 모션 / 본딩 | 필수 | 필수 | 빈 값(직접 입력) |
| `VERNIER_OFFSET_X` | 버니어 | 필수 |  | 0 |
| `VERNIER_OFFSET_Y` | 버니어 | 필수 |  | 0 |
| `VERNIER_DIST_X` | 버니어 | 필수 |  | 빈 값(직접 입력) |
| `VERNIER_DIST_Y` | 버니어 | 필수 |  | 빈 값(직접 입력) |

## Recipe 파라미터

활성 레시피에 아래 이름의 행이 없으면 `FindByParam()`이 "`<이름>` 파라미터가 없습니다" 예외를 던지고 시퀀스가 멈춥니다(‘필수’). ‘예시값’은 로컬 DB(`bin/Debug/.../hcb.db`, 레시피 `1`)에 있는 값입니다. 빈 칸은 로컬 DB에 행이 없는 항목입니다.

### 다이 / 웨이퍼 치수

| Name | 타입 · 단위 | 예시값 | 용도 | 사용처 | 필수 |
| --- | --- | --- | --- | --- | --- |
| `TOP_DIE_THICKNESS` | Double · mm | | Top Die 두께. H_Z 본딩/픽업 높이 계산 | MainSequence, StepSequence, Calibration·Vision·WaferSeq 탭 | 필수 |
| `BTM_DIE_THICKNESS` | Double · mm | | Btm Die 두께. H_Z = ShankToWaferOffset − Top − Btm − READY | 동일 | 필수 |
| `TOP_DIE_SIZE` | 문자열 | | Top Die 크기(비전 촬상 위치 예측) | DieSequence(TopHighAlign), Calibration 탭 | 필수 |
| `WAFER_SIZE` | Integer | 13 | 웨이퍼 크기 | WaferSeq 탭 (웨이퍼 맵) | 선택 |
| `DIE_SIZE_X` / `DIE_SIZE_Y` | Double · mm | 13 / 13 | 다이 크기 | WaferSeq 탭 | 선택 |
| `GAP_X` / `GAP_Y` | Double · mm | 0.1 / 0.1 | 다이 간격 | WaferSeq 탭 | 선택 |
| `SCRIBE_SHIFT_X` / `SCRIBE_SHIFT_Y` | Double · mm | 0 / 0 | 스크라이브 라인 시프트 | WaferSeq 탭 | 선택 |
| `ALIGN_TOP_SPACING_X` / `ALIGN_TOP_SPACING_Y` | Double · mm | 11.6 / 6.9 | Top 얼라인 마크 간격 | WaferSeq 탭 | 선택 |

### 비전 · 정렬 · 보정

| Name | 타입 · 단위 | 예시값 | 용도 | 사용처 | 필수 |
| --- | --- | --- | --- | --- | --- |
| `FID_ALIGN_GAP` | Double · mm | | Fid↔Align 마크 촬상 높이 차(h_z/H_Z 상대 이동량) | DieSequence, MainSequence, Calibration·Vision·WaferSeq 탭 | 필수 |
| `X_ALIGN_OFFSET` | Double · mm | | 최종 보정 X 오프셋(`OffsetXY.X`, ResultX에 더함) | MainSequence `LoadCalibrationInto` | 필수 |
| `Y_ALIGN_OFFSET` | Double · mm | | 최종 보정 Y 오프셋(`OffsetXY.Y`) | 동일 | 필수 |
| `T_ALIGN_OFFSET` | Double · deg | | 최종 보정 θ 오프셋(`OffsetT`, ResultT에 더함) | 동일 | 필수 |
| `SPEC_X` / `SPEC_Y` | Double · mm | 0.5 / 0 | 본딩 결과 판정 스펙 | WaferSeq 탭 | 선택 |
| `SPEC_THETA` | Double · deg | 0 | θ 목표값. thetaF = SPEC_THETA − (측정 각도차) | MainSequence(CoordinateSystemIntegration), WaferSeq 탭 | 필수 |
| `HC1_FID_OFFSET_X` / `Y` | Double · mm | | HC1 피듀셜 위치 보정(`hc1FidOffset`) | MainSequence(BtmHighAlign) | 필수 |
| `HC2_FID_OFFSET_X` / `Y` | Double · mm | | HC2 피듀셜 위치 보정(`hc2FidOffset`) | 동일 | 필수 |
| `HC_CENTER_ERROR_X` / `HC_CENTER_ERROR_Y` | Double · mm | | HC 카메라 센터링 오차 보정 | MainSequence, StepSequence, Calibration 탭 | 필수 |
| `LOW_ERROR_OFFSET_X` / `LOW_ERROR_OFFSET_Y` | Double · mm | | 저배율 비전 픽업 위치 오차 보정 | MainSequence(Pick Up) | 필수 |

### 모션 · 본딩 · 기타

| Name | 타입 · 단위 | 예시값 | 용도 | 사용처 | 필수 |
| --- | --- | --- | --- | --- | --- |
| `READY_POSITION` | Double · mm | | H_Z 본딩 대기 간격(Z 하강 식에서 빼는 값) | MainSequence, StepSequence(BondingCorr), Vision 탭 | 필수 |
| `TOP_BTM_GAP` | Double · mm | | Top/Btm Die 사이 간격(Btm 측정 시 H_Z) | MainSequence | 필수 |
| `VERNIER_OFFSET_X` / `_Y` | Double · mm | | 버니어 측정 위치 오프셋 | InitSequence | 필수 |
| `VERNIER_DIST_X` / `_Y` | Double · mm | | 버니어 패턴 간 거리(`vernier.Preprocess`) | StepSeq 탭 | 필수 |
| `BONDING_FORCE` | Double | | 본딩 하중(표시·저장) | BondingInfoWindow | 선택 |
| `BONDING_TIME` | Double | | 본딩 시간(표시·저장) | BondingInfoWindow | 선택 |
| `VISION_RECIPE` | 문자열 | | 비전 PC 레시피 이름. 사용 레시피 변경 시 비전에 전송(대소문자 무시) | RecipeService.SetUseRecipeAsync | 선택 |

### Step Recipe (가압 스텝)

Step 행마다 아래 컬럼이 있습니다. 시퀀스는 `FindStepByName()`으로 Step 이름을 찾습니다. 코드가 찾는 Step 이름은 `PICK UP`, `BTM PRESS`(MainSequence), `TOP PRESS`(StepSequence)입니다. 코드에 시간 단위가 적혀 있지 않아 ms로 가정했습니다.

| 컬럼 | 타입 | 설명 (PMAC 대응 변수) |
| --- | --- | --- |
| `Name` | string(100) | Step 이름 |
| `StepNumber` | int | 스텝 순번 |
| `AccTime` | int | 1단계 가압시간 (`BONDING_ACC_TIME`) |
| `AccTime2` | int | 2단계 가압시간 (`BONDING_ACC_TIME2`) |
| `ContTime` | int | 유지시간 (`BONDING_CONT_TIME`) |
| `DecTime` | int | 감압시간 (`BONDING_DEC_TIME`) |
| `LoadCell` | double | 목표 로드셀 값 (`BONDING_LOADCELL`) |
| `Current` | double | 1단 기울기 전류 (`BONDING_CURRENT`) |
| `Current2` | double | 최종 전류 (`BONDING_CURRENT2`, ×1000) |
| `VacOffTime` | int | Vacuum OFF 시점 |
| `BlowEnable` | bool | Blow 사용 여부 |
| `BlowOnTime` | int | Blow ON 시점 |
| `BlowDuration` | int | Blow 유지시간 |
| `Description` | string(200) | 설명 |

## ECParameter

ECParam은 조회하는 메서드에 따라 값이 없을 때의 동작이 다릅니다.

| 조회 메서드 | 값이 없을 때 |
| --- | --- |
| `GetDouble()` | 예외 발생 (필수) |
| `FindByName()` | 빈 DTO 반환 |
| `GetEcParamInt()` / `GetEcParamDouble()` | 코드 기본값 사용 |

현재 로컬 DB의 ECParam 테이블은 비어 있습니다. 실제 값은 장비 DB에서 채워야 합니다.

### 장비 오프셋 (수동 입력)

| Name | 타입 · 단위 | 용도 | 사용처 | 누락 시 |
| --- | --- | --- | --- | --- |
| `SHANK_TO_WAFER_OFFSET` | Double · mm | Shank ↔ Wafer 표면 거리. 본딩 H_Z 계산의 기준 | MainSequence, StepSequence, Calibration·Vision·WaferSeq 탭 | 예외 |
| `SHANK_TO_DIE_OFFSET` | Double · mm | Shank ↔ Die Carrier 거리. 픽업 H_Z 계산 | MainSequence(Pick Up) | 예외 |
| `SHANK_LOW_OFFSET_X` / `SHANK_LOW_OFFSET_Y` | Double · mm | 저배율 카메라 ↔ Shank 중심 오프셋 | MainSequence, Calibration·WaferSeq 탭 | 예외 |
| `ALIGN_DIST_TOLERANCE` | Double · mm | Top/Btm Align 거리 차 허용치. 초과 시 경고 | StepSeq 탭 | 검사 생략 (0 이하도 생략) |

### 캘리브레이션 결과 (Calibration 탭에서 자동 저장)

| Name | 타입 · 단위 | 용도 | 저장 위치 → 사용처 | 누락 시 |
| --- | --- | --- | --- | --- |
| `HC1_X` / `HC1_Y` | Double · mm | HC1 카메라 위치(카메라 거리) | Calibration → MainSequence(CamDistAndHcro) | 빈 값 → Parse 예외 |
| `HC2_X` / `HC2_Y` | Double · mm | HC2 카메라 위치(Hc2Offset) | Calibration → MainSequence | 동일 |
| `HC1_T` / `HC2_T` | Double · deg | HC1/HC2 카메라 회전 각도 | Calibration → MainSequence, EqpCommunicationService(비전 각도 전송) | 동일 |
| `PC_T` | Double · deg | PC 카메라 회전 각도 | Calibration → MainSequence, EqpCommunicationService | 동일 |
| `PC_W_T` | Double · deg | PC 카메라 ↔ Wafer 각도. HC1/HC2_T에 더해서 사용 | EqpCommunicationService | 동일 |
| `HCRO_X` / `HCRO_Y` | Double · mm | Head 회전중심(HCRO). 좌표 통합 시 마크에서 빼는 값 | Calibration·MainSequence(Manual Tracing) → MainSequence | 동일 |
| `HCRO_PC_X` / `HCRO_PC_Y` | Double · mm | PC 카메라 기준 회전중심 | MainSequence(`LoadCalibrationInto`) | 동일 |
| `HC1_FID_REF_DX` / `HC1_FID_REF_DY` | Double · mm | HC1 피듀셜 기준 DxCam/DyCam. 드리프트 측정 기준 | Calibration(`SetOrUpdate`) → DieSequence | 예외 |
| `HC2_FID_REF_DX` / `HC2_FID_REF_DY` | Double · mm | HC2 피듀셜 기준 DxCam/DyCam | 동일 | 예외 |

### 알고리즘 옵션 (기본값 있음)

| Name | 타입 | 기본값 | 용도 | 사용처 |
| --- | --- | --- | --- | --- |
| `HCRO_FIT_MODE` | 문자열 | `rigid` | 회전중심 피팅 방식: `rigid`(강체) 또는 `circle`(FitCircle) | MainSequence |
| `HCRO_REPEAT_N` | Integer | 1 | HCRO 측정 반복 횟수. [0°, −0.75°, +0.75°] × N회 촬상 | MainSequence |
| `LOW_VISION_RETRY_MAX` | Integer | 3 | 저배율 비전 재시도 횟수 | DieSequence |
| `VISION_RETRY_MAX` | Integer | 3 | 고배율 비전 재시도 횟수 | StepSequence |
| `VISION_RETRY_STEP_MM` | Double · mm | 0.005 | 재시도 시 이동 간격 | StepSequence |
| `BTM_THETA_SIGN` | Double | −1.0 | Btm θ 보정 부호(하드웨어 방향이 반대면 +1) | DieSequence |
| `BTM_THETA_MIN_DEG` | Double · deg | 0.0 | Btm θ 보정 데드밴드(미만이면 생략) | DieSequence |

## 참고 및 주의사항

- **시드 없음:** 두 테이블 모두 `DbSeeder`가 기본 행을 넣지 않습니다. 새 장비나 새 레시피에서는 위 표의 ‘필수’ 항목을 직접 등록해야 합니다. 레시피를 복사(`CopyRecipe`)하면 ParamList도 함께 복사됩니다.
- **이름 일치:** 조회는 `Equals`로 하므로 대소문자와 공백을 구분합니다. 예외는 `VISION_RECIPE` 하나뿐입니다(Trim 후 대소문자 무시).
- **값 형식:** Value는 문자열로 저장되고 `double.Parse`로 읽습니다. `SetOrUpdate`는 소수점 8자리(`F8`)로 저장합니다. 시퀀스는 `Minimum`/`Maximum`으로 범위를 검사하지 않습니다.
- **미사용 상수:** `BOND_DELAY`는 `MotionConstants.cs`에 선언만 되어 있고, 이 값을 읽는 코드가 없습니다. `SAFTY`는 주석 처리되어 있습니다.
- **중복 클래스:** `RecipeService`가 `SERVICE/RecipeService.cs`와 `SYSTEM/DB/Service/RecipeService.cs` 두 곳에 같은 이름으로 있습니다. `FindByParam`이 있는 쪽은 후자입니다.
- **단위 표기:** 이 문서의 단위는 코드에서 값을 쓰는 방식(모션 축은 mm, θ는 deg)을 보고 추정했습니다. 장비 DB의 `UnitType`과 다르면 DB 값을 따릅니다.

**관련 소스:** `src/HCB/Data/Entity/{RecipeParam,StepRecipe,ECParam}.cs`, `src/HCB/Data/DataConfig.cs`, `src/HCB.UI/SYSTEM/DB/Service/{RecipeService,ECParamService}.cs`, `src/HCB.UI/SERVICE/Extenstions/MotionConstants.cs`, `src/HCB.UI/SERVICE/SequenceService.cs`(`GetEcParamInt/Double`).
