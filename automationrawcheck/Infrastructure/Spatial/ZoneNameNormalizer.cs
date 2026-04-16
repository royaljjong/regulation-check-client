// =============================================================================
// ZoneNameNormalizer.cs
// SHP DGM_NM 필드 → 사람이 읽는 용도지역명 정규화 유틸리티
//
// [역할]
//   CoordinateContainmentChecker가 ZoningFeature.Name을 결정할 때 사용합니다.
//   Infrastructure 레이어 내부 전용입니다.
//
// [해결 문제]
//   - DGM_NM 값이 비어있을 때 → ATRB_SE 코드 기반 이름으로 fallback
//   - DGM_NM 값이 코드 형식(예: UQA220)일 때 → 이름 테이블 조회
//   - 내부 공백 불일치("제 2종 일반주거지역") → 공백 제거 정규화
//
// [fallback 우선순위]
//   1순위: DGM_NM 필드 (실제 용도지역 명칭)
//   2순위: ALIAS  필드 (KLIP 별칭)
//   3순위: ATRB_SE 코드 → CodeToKoreanName 테이블 조회
//   최후 : "알 수 없음"
//
// [주의]
//   CodeToKoreanName은 ZoningRuleTable과 독립적으로 관리합니다.
//   ZoningRuleTable: Application 레이어, 법규 규칙 엔진 (확장·수정 주기)
//   CodeToKoreanName: Infrastructure 레이어, 데이터 품질 보정 (안정적 코드셋)
// =============================================================================

using System.Text.RegularExpressions;

namespace AutomationRawCheck.Infrastructure.Spatial;

/// <summary>
/// SHP 속성에서 사람이 읽는 용도지역 표시명을 결정하는 정규화 유틸리티입니다.
/// </summary>
internal static class ZoneNameNormalizer
{
    #region 코드 패턴 정규식

    /// <summary>
    /// ATRB_SE 코드 형식 패턴 (예: UQA220, UDV100).
    /// DGM_NM에 코드값이 잘못 들어온 경우를 방어적으로 감지합니다.
    /// </summary>
    private static readonly Regex LooksLikeCode =
        new Regex(@"^[A-Z]{2,4}\d{3}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    #endregion

    #region ATRB_SE 코드 → 한국어 이름 조회 테이블

    /// <summary>
    /// ATRB_SE 코드 → 용도지역 표시명.
    /// DGM_NM 필드가 비어있거나 코드값일 때 사용하는 Infrastructure 레이어 fallback입니다.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> CodeToKoreanName =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // ── 전용주거지역 ────────────────────────────────────────────────────
        ["UQA111"] = "제1종전용주거지역",
        ["UQA112"] = "제2종전용주거지역",
        // ── 일반주거지역 ────────────────────────────────────────────────────
        ["UQA121"] = "제1종일반주거지역",
        ["UQA122"] = "제2종일반주거지역",
        ["UQA123"] = "제3종일반주거지역",
        // ── 준주거지역 ──────────────────────────────────────────────────────
        ["UQA130"] = "준주거지역",
        // ── 상업지역 ────────────────────────────────────────────────────────
        ["UQA210"] = "중심상업지역",
        ["UQA220"] = "일반상업지역",
        ["UQA230"] = "근린상업지역",
        ["UQA240"] = "유통상업지역",
        // ── 공업지역 ────────────────────────────────────────────────────────
        ["UQA310"] = "전용공업지역",
        ["UQA320"] = "일반공업지역",
        ["UQA330"] = "준공업지역",
        // ── 녹지지역 ────────────────────────────────────────────────────────
        ["UQA410"] = "보전녹지지역",
        ["UQA420"] = "생산녹지지역",
        ["UQA430"] = "자연녹지지역",
        // ── 특수 구역 ───────────────────────────────────────────────────────
        ["UDV100"] = "개발제한구역",
        ["UQT600"] = "도시자연공원구역",
    };

    #endregion

    #region 공개 API

    /// <summary>
    /// SHP 속성 딕셔너리에서 사람이 읽는 용도지역 표시명을 결정합니다.
    /// </summary>
    /// <param name="attributes">LoadedFeature의 원본 DBF 속성 딕셔너리</param>
    /// <returns>
    /// 정규화된 용도지역 표시명.
    /// fallback 소진 시 "알 수 없음".
    /// </returns>
    public static string ResolveDisplayName(Dictionary<string, object?> attributes)
    {
        // ── 1순위: DGM_NM ─────────────────────────────────────────────────────
        var fromDgmNm = TryResolveNameField(attributes, "DGM_NM");
        if (fromDgmNm is not null) return fromDgmNm;

        // ── 2순위: ALIAS ──────────────────────────────────────────────────────
        var fromAlias = TryResolveNameField(attributes, "ALIAS");
        if (fromAlias is not null) return fromAlias;

        // ── 3순위: ATRB_SE 코드 → 이름 테이블 조회 ───────────────────────────
        var code = PickString(attributes, "ATRB_SE", "atrb_se");
        if (!string.IsNullOrWhiteSpace(code) &&
            CodeToKoreanName.TryGetValue(code, out var nameFromCode))
        {
            return nameFromCode;
        }

        return "알 수 없음";
    }

    /// <summary>
    /// 단일 문자열 값을 정규화합니다.
    /// <list type="bullet">
    ///   <item>null/empty → null 반환</item>
    ///   <item>코드 형식(UQA220 등) → null 반환 (호출자가 code-lookup으로 처리)</item>
    ///   <item>내부 공백 제거 (예: "제 2종 일반주거지역" → "제2종일반주거지역")</item>
    /// </list>
    /// </summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var trimmed = raw.Trim();

        // 코드 형식이면 이름으로 사용하지 않음
        if (LooksLikeCode.IsMatch(trimmed)) return null;

        // 내부 공백 제거 (한국 용도지역 명칭은 공백 없음이 표준)
        var normalized = Regex.Replace(trimmed, @"\s+", string.Empty);
        return normalized.Length > 0 ? normalized : null;
    }

    #endregion

    #region 내부 유틸리티

    /// <summary>
    /// 지정 필드에서 값을 읽어 정규화합니다. 유효하지 않으면 null을 반환합니다.
    /// </summary>
    private static string? TryResolveNameField(
        Dictionary<string, object?> attributes,
        string fieldName)
    {
        var raw = PickString(attributes, fieldName);
        return Normalize(raw);
    }

    /// <summary>
    /// 속성 딕셔너리에서 첫 번째로 유효한 문자열 값을 반환합니다.
    /// </summary>
    private static string? PickString(
        Dictionary<string, object?> attributes,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var val))
            {
                var str = val?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(str)) return str;
            }
        }
        return null;
    }

    #endregion
}
