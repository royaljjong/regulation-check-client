// =============================================================================
// SpatialDataOptions.cs
// 공간 데이터 설정 옵션 - appsettings.json SpatialData 섹션 바인딩 클래스
// =============================================================================

namespace AutomationRawCheck.Infrastructure.Spatial;

#region SpatialDataOptions 클래스 정의

/// <summary>
/// appsettings.json의 "SpatialData" 섹션에 바인딩되는 설정 클래스입니다.
/// <para>
/// 실제 데이터 구조:
///   data/shp/
///     UPIS_004_20260201_11000/   ← 서울 (UPIS)
///       UPIS_C_UQ111.shp         ← UQ 코드별 SHP
///       UPIS_C_UQ122.shp
///       ...
///     KLIP_004_20260201_41000/   ← 경기 (KLIP)
///       KLIP_C_UQ111.shp
///       ...
/// ZoningShapefileDirectory를 최상위 shp 폴더로 지정하면
/// 모든 하위 디렉토리의 .shp 파일을 자동으로 탐색합니다.
/// </para>
/// </summary>
public sealed class SpatialDataOptions
{
    /// <summary>설정 섹션 키 이름</summary>
    public const string SectionName = "SpatialData";

    /// <summary>
    /// 용도지역 SHP 파일들이 있는 최상위 디렉토리 경로 (절대 경로 권장).
    /// 이 디렉토리 하위의 모든 .shp 파일을 재귀 탐색합니다.
    /// 예: "C:/GisData/shp"
    /// </summary>
    public string ZoningShapefileDirectory { get; set; } = "Data/shp";
}

#endregion
