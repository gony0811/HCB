# Wafer 중심 · Theta 찾기 시퀀스 (저배/고배)

> 구현: [`WaferSeqTabViewModel.cs`](../src/HCB.UI/SUB%20UI/ViewModels/WaferSeqTabViewModel.cs)
> Wafer 탭에서 **Scribeline(저배) → AlignMark(고배)** 를 이용해 웨이퍼의 중심과 회전(θ)을 찾아
> Die 격자를 기계 축에 정렬하고, 모든 Die의 절대 위치(저배/고배)를 산출하는 시퀀스다.

---

## 1. 개요

웨이퍼는 실장 시점마다 Table 위 위치·회전이 조금씩 다르므로, 본딩 전에 **웨이퍼 중심**과
**회전각(θ)** 을 실측해 Die 격자 원점을 맞춰야 한다. 이 시퀀스는 4단계로 구성된다.

| 단계 | 카메라 | 목적 | 산출물 |
|------|--------|------|--------|
| **1차** | 저배(HC_LOW) | 엣지 3점 원 피팅 → **대략 중심** | `CoarseCenterX/Y`, `HasCoarseCenter` |
| **2차** | 저배(HC_LOW) | Scribeline 정렬 → **정밀 중심 + θ 보정** | `ScribeAbs*`, `HasScribeMeasure` |
| **3차** | 저배 → 고배 | 저배 Center → **고배 Center 전환** | 고배 위치로 XY 이동 |
| **4차** | 고배(HC1) | AlignMark로 **정밀 θ 재보정** | `ThetaAngleDeg`, Die 위치 재계산 |

전체를 한 번에 실행하는 커맨드는 `RunFullSequence` (1→2→3→4 순차, 단계 실패/취소 시 중단).

### 저배 vs 고배 두 개의 센터
찾은 중심은 두 좌표계로 유지된다.
- **저배 센터** `CenterX/Y` — 저배(HC_LOW) 카메라 기준 (측정으로 직접 산출)
- **고배 센터** `HighCenterX/Y` = 저배 센터 + `ShankLowOffset` + `HcCenterError`
  - 변환 오프셋 `_highOffsetX/Y = ShankLowOffset + HcCenterError`
  - `GenerateWaferMap()`가 Die마다 저배 위치(`PositionX/Y`)와 고배 위치(`HighPositionX/Y`)를 함께 생성

### 모션 축
| 상수 | 실제 축 | 용도 |
|------|---------|------|
| `XAxis` | `H_X` (Head X) | 스테이지 X |
| `YAxis` | `W_Y` (Wafer Table Y) | 웨이퍼 테이블 Y |
| `TAxis`/`ThetaAxis` | `W_T` (Wafer Table θ) | 웨이퍼 회전 |

### Z축 이동 규칙 (규칙1)
측정 위치로 갈 때 항상 Z를 먼저 이동한 뒤 XY를 이동한다.
- 기본(저배/신규): `h_z`(HEAD_SAFETY) → `H_Z`(측정 높이)
- 고배 → 저배 전환: `H_Z` 먼저 내리고 → `h_z`
- 고배 Z: `H_Z = ShankToWaferOffset − TopDieThickness − BtmDieThickness + FID_ALIGN_GAP − 0.1`
- 현재 Z 위치는 `_zAtHighMag` 플래그로 추적 (`MoveZForLowMagAsync` / `MoveZForHighMagAsync`)

---

## 2. 1차 — 저배 엣지 3점 대략 중심 (`FindCenterStep1`)

저배율은 화소당 ~45µm라 스크라이브 신뢰도가 낮아, **웨이퍼 엣지(원호)** 를 3점 잡아
최소자승 원 피팅으로 대략 중심을 구한다.

1. 규칙1대로 저배 Z 이동 (`MoveZForLowMagAsync`)
2. 지정 Position `WAFER_ALIGN_1/2/3`(약 120° 간격, 11/4/7시)으로 자동 이동하며 각 위치에서 엣지 1점 측정
   - `RequestWaferEdge(clock)` → 엣지 절대좌표 = **현재 스테이지 − 카메라→엣지 오프셋**
   - `EdgeStations` 매핑: `WAFER_ALIGN_1`=11시(`H11`, 통신코드 12), `_2`=4시(`H04`), `_3`=7시(`H07`)
3. 3점으로 `CalibrationMath.FitCircleCenter` → 대략 중심 (`CoarseCenterX/Y`)
4. 저배 카메라를 대략 중심으로 이동
5. `ApplyWaferCenter(center)` → 저배/고배 센터 확정 + Die 위치 전체 재계산

> 3점이 일직선이면 원 피팅 불가(`InvalidOperationException`) → WAFER_ALIGN 위치 확인 안내.

---

## 3. 2차 — 저배 Scribeline 정밀 중심 + θ (`FindCenterStep2`)

저배(HC_LOW)로 Scribeline(십자 교차점)을 비전 중심에 정렬하며 정밀 중심과 θ를 잡는다.
HC_LOW는 피사계 심도가 커서 AF 불필요.

### Scribeline 격자 규칙
십자(+)마크는 네 Die `(r,c)·(r,c+1)·(r+1,c)·(r+1,c+1)`이 모두 있는 **내부 교차점**에만 존재.
최외곽 경계선은 한쪽에 Die가 없어(ㅜ/ㅏ 모양) 측정 불가.
- 세로선 c 오프셋: `(c − half + 0.5) × pitchX`
- 가로선 r 오프셋: `−(r − half + 0.5) × pitchY` (half = (WaferSize−1)/2)
- **WaferSize 짝수** → 중심에 Scribeline 존재 / **홀수** → 중심에 Die, 선은 ±pitch/2
- 원형 웨이퍼라 Row마다 Die 수가 달라 기준 선 좌/우 측정 가능 선 수가 비대칭
- 유효 범위 계산: `ScribeLineRange(r)`, `FindScribeCrossRow(preferredRow)`

### 절차
1. **기준 교차점 결정** — Recipe `ScribeShiftX/Y`에서 가장 가까운 *십자마크가 있는* 교차점으로 스냅
   (최외곽 선 제외). 실제 적용된 오프셋을 `_refScribeOffsetX/Y`에 저장 (Recipe 값과 다를 수 있음)
2. 규칙1대로 저배 Z 이동 → 기준 교차점으로 절대 이동
3. **중심 정렬** `CenterScribeAsync` — 측정 offset(r.X,r.Y)만큼 XY를 역이동(−offset)해 교차점을
   카메라 중심(offset≈0, tol 3µm)으로 끌어옴, 최대 4회 반복 → 중심 절대좌표 확정
4. 정렬 후 실제로 잡힌 교차점을 절대좌표에서 역산 (`startC`/`startR`, `_refScribeOffset` 갱신)
5. **θ 보정** `CorrectThetaBySymmetricPairAsync` — 중심을 피벗으로 좌우 대칭 쌍을 안쪽→바깥으로 확장,
   검출되는 **가장 바깥(최장 baseline)** 쌍에서 1회 θ 산출 → `W_T` 회전
6. 회전 후 중심 재정렬(`CenterScribeAsync`) → 최종 중심 절대좌표 `ScribeAbsX/Y/T` 기록, `HasScribeMeasure=true`
7. 정밀 중심 = `ScribeAbs − _refScribeOffset` → `ApplyWaferCenter` → Die 위치 전체 재계산

### 대칭 쌍 θ 보정 코어 (`CorrectThetaBySymmetricSweepAsync`)
저배 Scribe · 고배 AlignMark 공용.
- 현재 위치를 피벗(offset 0)으로 좌 −m·우 +m 대칭점을 m=1→mMax로 확장하며 두 점 측정
- 한쪽이라도 미검출이면 **웨이퍼 끝**으로 보고 확장 중단, 그때까지 성공한 가장 바깥 쌍 사용
- 최장 baseline 기울기 `atan2(ΔY, ΔX)` (±90° 정규화), `|기울기| ≥ minDeg`면 `W_T`를 −기울기만큼 1회 회전
  - 회전 부호 `ThetaSign = −1.0` (하드웨어 방향 반대면 +1)
- 측정·보정 후 X는 피벗으로 복귀한 상태로 종료 → 회전 피벗이 중심과 달라도 각도는 0이 되고 중심은 재정렬로 복원

---

## 4. 3차 — 저배 Center → 고배 Center 전환 (`FindCenterStep3`)

Scribe 측정 위치가 아니라 웨이퍼 **저배 Center**(`CenterX/Y`)를 기준으로 고배 Center로 XY 전환.
- 목표 = 저배 Center + `ShankLowOffset` + `HcCenterError`
  - `targetHX = CenterX + ShankLowOffsetX + HcCenterErrorX`
  - `targetWY = CenterY + ShankLowOffsetY + HcCenterErrorY`
- 규칙1대로 고배 Z 먼저 이동(`MoveZForHighMagAsync`) 후 XY 이동
- ShankLowOffset이 저배카메라↔Shank 상대거리이므로, 저배 측정 → 고배 실작업으로 좌표계 전환

---

## 5. 4차 — 고배 AlignMark 정밀 θ (`FindCenterStep4`)

Scribe가 아닌 **AlignMark**를 고배(HC1)로 측정해 θ를 정밀 재보정한다. 별도 Shift 없음.

1. 규칙1대로 고배 Z 이동
2. 시작 AlignMark 측정 (`MeasureHc1AlignAbsAsync`, 실패 시 `SearchAlignByYAsync`로 Y 탐색)
3. 좌우 대칭 확장 최대 배수 `mMax` 산출 (`MaxStepsWithinWafer` 양쪽 중 작은 값)
4. `CorrectThetaBySymmetricSweepAsync`로 2차와 동일한 대칭 쌍 θ 보정 (가장 바깥에서 1회)
5. 완료 후 `ComputeDiePositions`로 Die 위치 전체 자동 재계산

### AlignMark 측정 보조
- `MeasureHc1AlignAbsAsync` — HC1 AF 후 측정, 절대좌표 = 현재 스테이지 − 카메라→마크 오프셋
- `SearchAlignByYAsync` — 마크가 FOV를 벗어나면 `W_Y`를 0 기준 좌우 확장 스캔(+step,−step,+2step…),
  찾으면 절대좌표 반환. 성공/실패 무관하게 `W_Y`는 원위치 복귀(절대좌표는 W_Y 이동에 불변)
- `MaxStepsWithinWafer` — 시작 위치에서 sweep 방향으로 웨이퍼 밖으로 안 나가는 최대 스텝.
  DieList가 있으면 해당 Row의 Col 범위로 정확히, 없으면 반경으로 근사

---

## 6. Die 위치 계산 (`ComputeDiePositions` / `ApplyWaferCenter` / `GenerateWaferMap`)

찾은 중심을 격자 원점으로 삼아 모든 Die의 절대 위치를 재계산한다.
- 중심 소스 우선순위: **2차 정밀 중심**(`ScribeAbs − _refScribeOffset`) > **1차 대략 중심**(`CoarseCenter`)
- `ApplyWaferCenter`:
  1. 저배 센터 `CenterX/Y` = 측정 중심
  2. 고배 센터 `HighCenterX/Y` = 저배 + `ShankLowOffset` + `HcCenterError`
  3. `GenerateWaferMap()` 호출
- `GenerateWaferMap`: WaferSize·DieSize·Gap 격자에서 반경 밖 Die 제외, 각 Die에
  - `PositionX = CenterX + (col − halfCol)·(DieSizeX + GapX)`
  - `PositionY = CenterY − (row − halfRow)·(DieSizeY + GapY)`
  - `HighPositionX/Y = Position + _highOffset`
  - θ는 이미 보정되어 격자가 축과 정렬됐다고 보고 축 정렬 격자로 계산

---

## 7. 관련 동작 (참고)

- **선택 Die 보기** `ViewDieLowMag` / `ViewDieHighMag` — 규칙1대로 Z 이동 후 해당 Die로 XY 이동,
  저배(HC_LOW) 또는 고배(HC1/HC2) 카메라로 AlignMark 측정 (`Low/Hc1/Hc2MeasureText` 표시)
- **본딩** `Bonding` — 중심 미계산 시 차단. 클릭 Die의 고배 절대좌표로 `StepSeqTab.WaferBonding` 실행
- **배치 검증** `VerifyPlacement` — 본딩 후 Top/Btm Align 4점 측정(`ResultMeasurement`) →
  강체 3자유도(`SimpleMeasurement`) 오차 `ErrorX/ErrorY`(µm)·`ErrorTheta`(°) 산출 → `PlacementResult` DB 저장
- **취소/Interlock** — `_alignCts` 토큰으로 진행 중 측정/시프트 즉시 취소(`CancelOperation`),
  Interlock 발생 시 `OnInterlockActivated`가 자동 취소

---

## 8. 전체 흐름 요약

```
RunFullSequence
  ├─ 1차 FindCenterStep1 : 저배 엣지 3점 → 원 피팅 대략 중심 → Die 위치 계산
  ├─ 2차 FindCenterStep2 : 저배 Scribe 정렬 → 정밀 중심 + θ(대칭 쌍) → Die 위치 계산
  ├─ 3차 FindCenterStep3 : 저배 Center → 고배 Center(ShankLowOffset+HcCenterError) XY 전환
  └─ 4차 FindCenterStep4 : 고배 AlignMark → θ 정밀 재보정 → Die 위치 재계산
```
