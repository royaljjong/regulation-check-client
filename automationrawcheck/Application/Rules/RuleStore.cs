// =============================================================================
// RuleStore.cs
// 규칙 데이터 사전 로더 + 무결성 검증
//
// [로딩 흐름]
//   1. 임베딩 리소스에서 JSON 역직렬화
//   2. isActive=false 항목 제거
//   3. 무결성 검증 (필수 필드, 중복 ID, sortOrder, layerType, legalBasis)
//   4. sortOrder 오름차순 정렬 후 정적 캐시
//
// [검증 항목]
//   - id 비어 있으면 예외
//   - 동일 파일 내 id 중복 → 예외
//   - LawLayerRule: layerType이 허용 값 이외이면 예외
//   - LawLayerRule: layerType="mep"인데 title 비어 있으면 예외
//   - LawLayerRule: layerType≠"mep"인데 law 비어 있으면 예외
//   - ReviewItemRule: category 또는 title 비어 있으면 예외
//   - sortOrder < 0 → 예외
//   - legalBasis 각 항목:
//       referenceType이 허용 값 이외이면 예외
//       lawName 비어 있으면 예외
//       article ≤ 0이면 예외 (null은 허용 — 별표/부칙 참조)
//       article도 null이고 appendixRef도 비어 있으면 예외
//       normalizedKey 비어 있으면 예외
//       동일 규칙 내 normalizedKey 중복 → 예외
// =============================================================================

using System.Reflection;
using System.Text.Json;

namespace AutomationRawCheck.Application.Rules;

/// <summary>
/// 임베딩 JSON에서 규칙 레코드를 로드·검증·정렬하여 정적 캐시로 제공합니다.
/// 앱 시작 시 정적 생성자가 한 번 실행되며, 검증 실패 시 즉시 예외를 던집니다.
/// </summary>
public static class RuleStore
{
    // ── 허용 값 상수 ─────────────────────────────────────────────────────────
    private static readonly HashSet<string> _validLayerTypes =
        new(StringComparer.Ordinal) { "core", "extendedCore", "mep" };

    private static readonly HashSet<string> _validReferenceTypes =
        new(StringComparer.Ordinal)
        {
            "statute",          // 법률 본문
            "enforcementDecree", // 시행령
            "enforcementRule",   // 시행규칙
            "notice",            // 고시
            "ordinance",         // 조례
            "guideline",         // 지침·훈령·예규
        };

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        AllowTrailingCommas         = true,
    };

    // ── 공개 캐시 ─────────────────────────────────────────────────────────────

    /// <summary>법규 레이어 규칙 전체 목록 (isActive=true만, sortOrder 오름차순)</summary>
    public static IReadOnlyList<LawLayerRuleRecord> LawLayerRules { get; }

    /// <summary>건축 검토 항목 규칙 전체 목록 (isActive=true만, sortOrder 오름차순)</summary>
    public static IReadOnlyList<ReviewItemRuleRecord> ReviewItemRules { get; }

    /// <summary>신규 용도용 review item seed 규칙 목록</summary>
    public static IReadOnlyList<ReviewItemRuleRecord> ReviewItemSeedRules { get; }

    /// <summary>신규 용도용 law layer seed 규칙 목록</summary>
    public static IReadOnlyList<LawLayerRuleRecord> LawLayerSeedRules { get; }

    // ── 통계 (진단용) ─────────────────────────────────────────────────────────

    /// <summary>로드된 법규 레이어 규칙 수 (비활성 제외)</summary>
    public static int LawLayerRuleCount => LawLayerRules.Count;

    /// <summary>로드된 검토 항목 규칙 수 (비활성 제외)</summary>
    public static int ReviewItemRuleCount => ReviewItemRules.Count;

    // ─────────────────────────────────────────────────────────────────────────

    static RuleStore()
    {
        var llRaw = Load<LawLayerRuleRecord>("law_layer_rules.json");
        var riRaw = Load<ReviewItemRuleRecord>("review_item_rules.json");
        var llExtensionRaw = LoadOptional<LawLayerRuleRecord>("law_layer_rules_expansion.json");
        var riExtensionRaw = LoadOptional<ReviewItemRuleRecord>("review_item_rules_expansion.json");
        var llSeedRaw = LoadOptional<LawLayerRuleRecord>("law_layer_seed_rules.json");
        var riSeedRaw = LoadOptional<ReviewItemRuleRecord>("review_item_seed_rules.json");

        // isActive=false 항목 제거
        var llActive = llRaw.Concat(llExtensionRaw).Where(r => r.IsActive).ToList();
        var riActive = riRaw.Concat(riExtensionRaw).Where(r => r.IsActive).ToList();
        var llSeedActive = llSeedRaw.Where(r => r.IsActive).ToList();
        var riSeedActive = riSeedRaw.Where(r => r.IsActive).ToList();

        // 무결성 검증
        ValidateLawLayerRules(llActive, "law_layer_rules.json");
        ValidateReviewItemRules(riActive, "review_item_rules.json");
        ValidateLawLayerRules(llSeedActive, "law_layer_seed_rules.json");
        ValidateReviewItemRules(riSeedActive, "review_item_seed_rules.json");

        // sortOrder 오름차순 정렬 (stable: 같은 값이면 원본 순서 유지)
        LawLayerRules  = llActive.OrderBy(r => r.SortOrder).ToList().AsReadOnly();
        ReviewItemRules = riActive.OrderBy(r => r.SortOrder).ToList().AsReadOnly();
        LawLayerSeedRules = llSeedActive.OrderBy(r => r.SortOrder).ToList().AsReadOnly();
        ReviewItemSeedRules = riSeedActive.OrderBy(r => r.SortOrder).ToList().AsReadOnly();
    }

    // ── 로딩 ─────────────────────────────────────────────────────────────────

    private static List<T> Load<T>(string fileName)
    {
        var asm = Assembly.GetExecutingAssembly();

        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            throw new InvalidOperationException(
                $"임베딩 리소스를 찾을 수 없습니다: {fileName}. " +
                $"csproj에 <EmbeddedResource> 항목이 있는지 확인하세요.");

        using var stream = asm.GetManifestResourceStream(resourceName)!;
        return JsonSerializer.Deserialize<List<T>>(stream, _jsonOpts)
               ?? throw new InvalidOperationException($"규칙 JSON 역직렬화 실패: {fileName}");
    }

    private static List<T> LoadOptional<T>(string fileName)
    {
        var asm = Assembly.GetExecutingAssembly();

        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return [];

        using var stream = asm.GetManifestResourceStream(resourceName)!;
        return JsonSerializer.Deserialize<List<T>>(stream, _jsonOpts) ?? [];
    }

    // ── 검증 — LawLayerRule ───────────────────────────────────────────────────

    private static void ValidateLawLayerRules(List<LawLayerRuleRecord> rules, string file)
    {
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var r in rules)
        {
            // id 필수
            if (string.IsNullOrWhiteSpace(r.Id))
                throw new InvalidOperationException($"[{file}] id가 비어 있는 규칙이 있습니다.");

            // id 중복
            if (!seenIds.Add(r.Id))
                throw new InvalidOperationException($"[{file}] 중복 id: '{r.Id}'");

            // sortOrder 음수 금지
            if (r.SortOrder < 0)
                throw new InvalidOperationException($"[{file}] id='{r.Id}' sortOrder가 음수입니다: {r.SortOrder}");

            // layerType 허용 값
            if (!_validLayerTypes.Contains(r.LayerType))
                throw new InvalidOperationException(
                    $"[{file}] id='{r.Id}' 알 수 없는 layerType: '{r.LayerType}'. " +
                    $"허용 값: {string.Join(", ", _validLayerTypes)}");

            // MEP: title 필수
            if (r.LayerType == "mep" && string.IsNullOrWhiteSpace(r.Title))
                throw new InvalidOperationException(
                    $"[{file}] id='{r.Id}' layerType='mep'인 항목에 title이 비어 있습니다.");

            // Core/ExtendedCore: law 필수
            if (r.LayerType != "mep" && string.IsNullOrWhiteSpace(r.Law))
                throw new InvalidOperationException(
                    $"[{file}] id='{r.Id}' layerType='{r.LayerType}'인 항목에 law가 비어 있습니다.");

            // legalBasis 항목 검증
            ValidateLegalBasis(r.LegalBasis, r.Id, file);
        }
    }

    // ── 검증 — ReviewItemRule ─────────────────────────────────────────────────

    private static void ValidateReviewItemRules(List<ReviewItemRuleRecord> rules, string file)
    {
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var r in rules)
        {
            // id 필수
            if (string.IsNullOrWhiteSpace(r.Id))
                throw new InvalidOperationException($"[{file}] id가 비어 있는 규칙이 있습니다.");

            // id 중복
            if (!seenIds.Add(r.Id))
                throw new InvalidOperationException($"[{file}] 중복 id: '{r.Id}'");

            // sortOrder 음수 금지
            if (r.SortOrder < 0)
                throw new InvalidOperationException($"[{file}] id='{r.Id}' sortOrder가 음수입니다: {r.SortOrder}");

            // category 필수
            if (string.IsNullOrWhiteSpace(r.Category))
                throw new InvalidOperationException($"[{file}] id='{r.Id}' category가 비어 있습니다.");

            // title 필수
            if (string.IsNullOrWhiteSpace(r.Title))
                throw new InvalidOperationException($"[{file}] id='{r.Id}' title이 비어 있습니다.");

            // legalBasis 항목 검증
            ValidateLegalBasis(r.LegalBasis, r.Id, file);
        }
    }

    // ── 검증 — LegalBasis ────────────────────────────────────────────────────

    private static void ValidateLegalBasis(
        List<LegalReferenceRecord> refs, string ruleId, string file)
    {
        if (refs.Count == 0) return;   // legalBasis 미입력은 허용

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var lb in refs)
        {
            // referenceType 허용 값
            if (!_validReferenceTypes.Contains(lb.ReferenceType))
                throw new InvalidOperationException(
                    $"[{file}] id='{ruleId}' legalBasis referenceType 허용 값 외: '{lb.ReferenceType}'. " +
                    $"허용: {string.Join(", ", _validReferenceTypes)}");

            // lawName 필수
            if (string.IsNullOrWhiteSpace(lb.LawName))
                throw new InvalidOperationException(
                    $"[{file}] id='{ruleId}' legalBasis에 lawName이 비어 있는 항목이 있습니다.");

            // article > 0 이거나 appendixRef가 있어야 함 (최소 하나의 조문 위치 필요)
            if (lb.Article.HasValue && lb.Article.Value <= 0)
                throw new InvalidOperationException(
                    $"[{file}] id='{ruleId}' legalBasis article은 양수여야 합니다: {lb.Article}");

            if (!lb.Article.HasValue && string.IsNullOrWhiteSpace(lb.AppendixRef))
                throw new InvalidOperationException(
                    $"[{file}] id='{ruleId}' legalBasis 항목에 article 또는 appendixRef 중 하나가 필요합니다. " +
                    $"(lawName='{lb.LawName}')");

            // normalizedKey 필수
            if (string.IsNullOrWhiteSpace(lb.NormalizedKey))
                throw new InvalidOperationException(
                    $"[{file}] id='{ruleId}' legalBasis normalizedKey가 비어 있습니다. " +
                    $"(lawName='{lb.LawName}')");

            // 동일 규칙 내 normalizedKey 중복 금지
            if (!seenKeys.Add(lb.NormalizedKey))
                throw new InvalidOperationException(
                    $"[{file}] id='{ruleId}' legalBasis 내 중복 normalizedKey: '{lb.NormalizedKey}'");
        }
    }
}
