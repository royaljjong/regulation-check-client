# 건축/토지 법규 검토 지원 백엔드 (MVP)

> 주소 또는 좌표 1개 입력 → 관련 규제·법령 근거를 통합 정리해 반환하는 백엔드 API

---

## 목표

- 토지이음과 유사하게 좌표/주소 기반으로 토지를 식별하고, 로컬 공간데이터와 규칙 테이블을 이용해 **1차 법규 검토 결과**를 반환합니다.
- 현재 MVP는 **좌표 입력 → 용도지역 판정** 엔진이 핵심입니다.
- 지구단위계획 / 개발제한구역 / 법제처 API는 확장 가능한 인터페이스 구조로 준비되어 있습니다.

---

## 주의사항

> **본 시스템의 결과는 참고용 1차 판정입니다.**
> 실제 건축 허가 판단의 근거로 사용할 수 없습니다.
> 건폐율/용적률 확정값은 제공하지 않습니다.
> 지구단위계획, 개발제한구역, 자치법규, 개별 법령 등 추가 검토가 반드시 필요합니다.

---

## 프로젝트 구조

```
automationrawcheck/
├── Program.cs                                        ← DI 등록, Swagger, Serilog
├── appsettings.json                                  ← 데이터 경로, 로깅, API 키 설정
│
├── Controllers/
│   └── RegulationCheckController.cs                 ← API 엔드포인트 3개
│
├── Dtos/
│   ├── CoordinateRequestDto.cs                      ← POST /coordinate 요청
│   ├── ParcelSearchRequestDto.cs                    ← POST /parcel 요청
│   └── RegulationCheckResponseDto.cs                ← 공통 응답
│
├── Domain/Models/                                    ← 순수 도메인 모델 (의존성 없음)
│   ├── CoordinateQuery.cs
│   ├── ParcelSearchRequest.cs
│   ├── ZoningFeature.cs
│   ├── RegulationCheckResult.cs
│   ├── RegulationSummary.cs
│   ├── ExtraLayerInfo.cs
│   └── LawReference.cs
│
├── Application/
│   ├── Interfaces/                                  ← 비즈니스 인터페이스 (확장 포인트)
│   │   ├── IZoningLayerProvider.cs                 ← 용도지역 조회
│   │   ├── ILawReferenceProvider.cs                ← 법령 참조 조회
│   │   ├── IDistrictUnitPlanProvider.cs            ← 지구단위계획
│   │   ├── IDevelopmentRestrictionProvider.cs      ← 개발제한구역
│   │   ├── IParcelSearchProvider.cs                ← 주소→좌표 변환
│   │   └── IRegulationCheckService.cs
│   └── Services/
│       └── RegulationCheckService.cs               ← 프로바이더 조합 서비스
│
├── Infrastructure/
│   ├── Configuration/
│   │   └── LawApiOptions.cs                        ← 법제처 API 설정
│   ├── Spatial/
│   │   ├── SpatialDataOptions.cs                   ← 공간 데이터 파일 경로 설정
│   │   ├── LoadedFeature.cs                        ← SHP/CSV 로드 결과 내부 모델
│   │   ├── ShapefileLoader.cs                      ← SHP 파일 읽기 전담
│   │   ├── CsvZoningLoader.cs                      ← CSV(WKT) 파일 읽기 전담
│   │   ├── ZoningFeatureCache.cs                   ← IMemoryCache 래핑/캐싱 전담
│   │   ├── CoordinateContainmentChecker.cs         ← Point-in-Polygon 판정 전담
│   │   └── ShapefileZoningLayerProvider.cs         ← IZoningLayerProvider 구현 (조합)
│   ├── Law/
│   │   └── StubLawReferenceProvider.cs             ← 법제처 API stub
│   ├── ExtraLayers/
│   │   ├── NullDistrictUnitPlanProvider.cs         ← 지구단위계획 null 구현
│   │   └── NullDevelopmentRestrictionProvider.cs  ← 개발제한구역 null 구현
│   └── Parcel/
│       └── StubParcelSearchProvider.cs             ← 주소→좌표 stub
│
├── Middleware/
│   └── GlobalExceptionMiddleware.cs                ← 전역 예외 처리
│
└── Data/
    └── 데이터_설치_안내.txt                          ← SHP/CSV 설치 방법
```

---

## NuGet 패키지 설치

```bash
cd automationrawcheck

# Swagger UI
dotnet add package Swashbuckle.AspNetCore --version 6.6.2

# Serilog
dotnet add package Serilog.AspNetCore --version 8.0.3

# NetTopologySuite - 공간 연산 (Point-in-Polygon 등)
dotnet add package NetTopologySuite --version 2.5.0

# NTS Shapefile Reader - SHP 파일 읽기
dotnet add package NetTopologySuite.IO.ShapeFile --version 2.1.0

# CsvHelper - CSV 파일 읽기
dotnet add package CsvHelper --version 33.0.1

# MemoryCache
dotnet add package Microsoft.Extensions.Caching.Memory --version 8.0.1

# (선택) 좌표계 변환 - SHP가 EPSG:5179(TM)인 경우 필요
# dotnet add package ProjNet4GeoAPI
```

---

## 로컬 실행 방법

### 1. 데이터 파일 준비

프로젝트 루트에 있는 ZIP 파일을 압축 해제합니다.

**SHP 방식 (권장)**
```
용도지역정보(도시계획)_20260223_전국 shp.zip 압축 해제
→ .shp, .dbf, .shx, .prj 파일이 같은 폴더에 있어야 합니다.
```

**CSV 방식 (fallback — SHP 없을 때 자동 사용)**
```
용도지역정보(도시계획)_20260223_전국csv.zip 압축 해제
→ WKT 컬럼이 포함된 CSV 파일
```

### 2. `appsettings.json` 경로 설정

```json
"SpatialData": {
  "ZoningShapefilePath": "C:/GisData/zoning/용도지역정보.shp",
  "ZoningCsvPath":       "C:/GisData/zoning/용도지역정보.csv"
}
```

> 상대 경로도 가능합니다: `"Data/zoning.shp"` → 실행 파일 기준으로 해석됩니다.

### 3. 서버 실행

```bash
cd automationrawcheck
dotnet run
```

또는 Visual Studio / Rider에서 F5 (Debug 실행).

### 4. Swagger UI 접속

```
https://localhost:{포트}/swagger
```

`launchSettings.json`의 `applicationUrl`에서 포트 번호를 확인하세요.

---

## API 엔드포인트

### `POST /api/regulation-check/coordinate` — 좌표 기반 법규 검토 (MVP 핵심)

**Request**
```json
{
  "longitude": 127.1234,
  "latitude": 37.1234
}
```

**Response (용도지역 발견 시)**
```json
{
  "input": {
    "longitude": 127.1234,
    "latitude": 37.1234
  },
  "zoning": {
    "name": "제2종일반주거지역",
    "code": "UQ120",
    "sourceLayer": "용도지역정보.shp",
    "attributes": {
      "PRPOS_AREA_DSTRC_CD": "UQ120",
      "PRPOS_AREA_DSTRC_NM": "제2종일반주거지역",
      "SGG_OID": "12345",
      "COL_ADM_SE": "31230"
    }
  },
  "regulationSummary": {
    "status": "Preliminary",
    "message": "용도지역 기반 1차 판정 결과입니다. 지구단위계획, 개발제한구역, 자치법규, 개별 법령 검토가 추가로 필요할 수 있습니다. 본 결과는 참고용이며 확정 법규 판단의 근거로 사용할 수 없습니다."
  },
  "lawReferences": [],
  "extraLayers": {
    "districtUnitPlan": null,
    "developmentRestriction": null
  }
}
```

**Response (용도지역 미발견 시)**
```json
{
  "input": {
    "longitude": 0.0,
    "latitude": 0.0
  },
  "zoning": null,
  "regulationSummary": {
    "status": "NotFound",
    "message": "입력 좌표에 해당하는 용도지역 정보를 찾을 수 없습니다. 좌표(WGS84 경도/위도)가 올바른지 확인하거나 관할 기관에 문의하세요."
  },
  "lawReferences": [],
  "extraLayers": {
    "districtUnitPlan": null,
    "developmentRestriction": null
  }
}
```

---

### `POST /api/regulation-check/parcel` — 지번/주소 기반 검토

| searchType | 동작 |
|---|---|
| `"Coordinate"` | longitude/latitude로 좌표 판정 수행 (실제 동작) |
| `"JibunAddress"` | addressText 입력 → 현재 501 반환 (주소 API 미연동) |
| `"RoadAddress"` | addressText 입력 → 현재 501 반환 (주소 API 미연동) |

**Request (좌표 타입)**
```json
{
  "searchType": "Coordinate",
  "longitude": 127.1234,
  "latitude": 37.1234
}
```

**Request (지번 주소 타입 — 현재 placeholder)**
```json
{
  "searchType": "JibunAddress",
  "addressText": "경기도 성남시 분당구 정자동 1-1"
}
```

---

### `GET /api/regulation-check/health` — 헬스체크

```json
{
  "status": "ok",
  "timestamp": "2026-04-05T00:00:00Z",
  "note": "법규 검토 백엔드 API 정상 동작 중. 결과는 참고용 1차 판정입니다."
}
```

---

## 좌표계 주의사항

현재 구현은 입력 좌표와 SHP 파일의 좌표계가 모두 **WGS84(EPSG:4326)** 라고 가정합니다.

한국 공간 데이터(토지이음 계열)는 주로 **EPSG:5179(GRS80 TM)** 을 사용합니다.

SHP 파일의 좌표계가 EPSG:5179라면:
1. `ProjNet4GeoAPI` 패키지 설치: `dotnet add package ProjNet4GeoAPI`
2. [CoordinateContainmentChecker.cs](automationrawcheck/Infrastructure/Spatial/CoordinateContainmentChecker.cs)의 TODO 주석 참조
3. 입력 WGS84 좌표를 TM으로 변환 후 판정하거나, 로드 시 SHP 좌표를 WGS84로 변환

---

## 확장 계획 (향후)

| 기능 | 현재 상태 | 확장 방법 |
|---|---|---|
| 법제처 법령 API 연동 | `StubLawReferenceProvider` (빈 목록) | `LawInfoApiProvider` 구현 후 DI 교체 |
| 지구단위계획 | `NullDistrictUnitPlanProvider` (null) | SHP 또는 API 구현 후 DI 교체 |
| 개발제한구역 | `NullDevelopmentRestrictionProvider` (null) | SHP 또는 API 구현 후 DI 교체 |
| 지번/주소 검색 | `StubParcelSearchProvider` (null) | VWorld/카카오 API 구현 후 DI 교체 |
| 공간 인덱스 최적화 | 선형 탐색 | `CoordinateContainmentChecker`에 STRtree 적용 |
| 좌표계 변환 | WGS84 가정 | `ProjNet4GeoAPI`로 TM↔WGS84 변환 추가 |

---

## 기술 스택

- **.NET 8** / ASP.NET Core Web API
- **NetTopologySuite** — 공간 연산 (Point-in-Polygon)
- **NetTopologySuite.IO.ShapeFile** — SHP 파일 읽기
- **CsvHelper** — CSV 파일 읽기
- **Serilog** — 구조화 로깅
- **Swashbuckle.AspNetCore** — Swagger UI
- **IMemoryCache** — 공간 데이터 캐싱
