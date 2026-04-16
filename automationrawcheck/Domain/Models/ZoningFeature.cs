// =============================================================================
// ZoningFeature.cs
// 용도지역 피처 도메인 모델 - SHP/CSV에서 읽어온 용도지역 속성 정보
// =============================================================================

namespace AutomationRawCheck.Domain.Models;

#region ZoningFeature 레코드 정의

/// <summary>
/// 공간 레이어(SHP/CSV)에서 읽어온 용도지역 피처 정보를 나타냅니다.
/// </summary>
public record ZoningFeature
{
    /// <summary>용도지역명 (예: 제1종일반주거지역)</summary>
    public string Name { get; init; }

    /// <summary>용도지역 코드 (예: UQ110)</summary>
    public string Code { get; init; }

    /// <summary>원천 레이어 식별자 (파일명 등)</summary>
    public string SourceLayer { get; init; }

    /// <summary>원본 속성 딕셔너리 (필드명 → 값, null 허용)</summary>
    public IReadOnlyDictionary<string, object?> Attributes { get; init; }

    /// <summary>ZoningFeature를 초기화합니다.</summary>
    public ZoningFeature(
        string name,
        string code,
        string sourceLayer,
        IReadOnlyDictionary<string, object?> attributes)
    {
        Name = name;
        Code = code;
        SourceLayer = sourceLayer;
        Attributes = attributes;
    }
}

#endregion
