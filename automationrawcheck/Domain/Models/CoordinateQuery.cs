// =============================================================================
// CoordinateQuery.cs
// 좌표 쿼리 도메인 모델 - 경도/위도 입력값을 나타내는 불변 레코드
// =============================================================================

namespace AutomationRawCheck.Domain.Models;

#region CoordinateQuery 레코드 정의

/// <summary>
/// 공간 검색에 사용되는 WGS84 좌표 쿼리 모델입니다.
/// 경도(Longitude)와 위도(Latitude)를 보관합니다.
/// </summary>
public record CoordinateQuery
{
    /// <summary>경도 (WGS84, -180 ~ 180)</summary>
    public double Longitude { get; init; }

    /// <summary>위도 (WGS84, -90 ~ 90)</summary>
    public double Latitude { get; init; }

    /// <summary>CoordinateQuery를 초기화합니다.</summary>
    /// <param name="longitude">경도 (-180 ~ 180)</param>
    /// <param name="latitude">위도 (-90 ~ 90)</param>
    public CoordinateQuery(double longitude, double latitude)
    {
        Longitude = longitude;
        Latitude = latitude;
    }
}

#endregion
