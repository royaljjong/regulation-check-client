// =============================================================================
// ShapefileLoader.cs
// SHP 파일 로딩 전담 클래스
// - ShapefileZoningLayerProvider에서 호출합니다.
// - NTS ShapefileDataReader를 사용해 .shp 파일을 LoadedFeature 목록으로 변환합니다.
//
// [실제 데이터 구조 - 2026년 2월 기준]
//   파일명: UPIS_C_UQ111.shp, KLIP_C_UQ111.shp 등
//   속성 인코딩: EUC-KR
//   주요 컬럼:
//     DGM_NM   (UPIS) / dgm_nm (KLIP) : 용도지역명 (EUC-KR)
//     ATRB_SE  (UPIS) / atrb_se (KLIP): 속성구분코드 (예: UQA121)
//     LCLAS_CL                         : 대분류코드 (예: UQA100)
//     MLSFC_CL                         : 중분류코드
//     SCLAS_CL                         : 소분류코드
//     SIGNGU_SE (UPIS) / sgg_cd (KLIP) : 시군구 코드
//   UQ 코드(용도지역 레이어 종류)는 파일명에서 추출합니다.
//   예: UPIS_C_UQ111.shp → "UQ111"
// =============================================================================

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace AutomationRawCheck.Infrastructure.Spatial;

#region ShapefileLoader 클래스

/// <summary>
/// SHP(Shapefile) 파일에서 <see cref="LoadedFeature"/> 목록을 로드하는 전담 클래스입니다.
/// <para>
/// 속성 인코딩: EUC-KR (한국 국가 공간정보 표준).
/// UQ 레이어 코드는 파일명에서 추출하여 각 피처의 속성에 <c>_UQ_CODE</c> 키로 추가합니다.
/// </para>
/// </summary>
public sealed class ShapefileLoader
{
    #region 상수

    /// <summary>속성 딕셔너리에 삽입하는 UQ 코드 키 이름</summary>
    public const string UqCodeKey = "_UQ_CODE";

    /// <summary>파일명에서 UQ 코드를 추출하는 정규식 (예: UPIS_C_UQ111 → "UQ111")</summary>
    private static readonly Regex UqCodeRegex =
        new Regex(@"UQ\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>DBF 속성 인코딩 (EUC-KR / CP949)</summary>
    private static readonly Encoding EucKr = GetEucKr();

    private static Encoding GetEucKr()
    {
        // .NET Core/5+ 에서는 EUC-KR 등 레거시 인코딩을 기본 제공하지 않음
        // CodePagesEncodingProvider를 등록해야 사용 가능
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding("euc-kr");
    }

    #endregion

    #region 필드 및 생성자

    private readonly ILogger<ShapefileLoader> _logger;
    private readonly GeometryFactory _geometryFactory;

    /// <summary>ShapefileLoader를 초기화합니다.</summary>
    public ShapefileLoader(ILogger<ShapefileLoader> logger, GeometryFactory geometryFactory)
    {
        _logger          = logger          ?? throw new ArgumentNullException(nameof(logger));
        _geometryFactory = geometryFactory ?? throw new ArgumentNullException(nameof(geometryFactory));
    }

    #endregion

    #region SHP 로드 메서드

    /// <summary>
    /// 지정된 경로의 SHP 파일을 읽어 <see cref="LoadedFeature"/> 목록을 반환합니다.
    /// </summary>
    /// <param name="shpFilePath">SHP 파일의 전체 경로 (.shp 확장자)</param>
    /// <returns>로드된 피처 목록. 실패 시 빈 목록.</returns>
    public List<LoadedFeature> Load(string shpFilePath)
    {
        var result = new List<LoadedFeature>();

        // 파일명에서 UQ 코드 추출 (예: UPIS_C_UQ111.shp → "UQ111")
        var fileName = Path.GetFileNameWithoutExtension(shpFilePath);
        var uqMatch  = UqCodeRegex.Match(fileName);
        var uqCode   = uqMatch.Success
            ? uqMatch.Value.ToUpperInvariant()
            : string.Empty;

        try
        {
            _logger.LogInformation(
                "SHP 파일 로드 시작: {Path} (UQ={UqCode})", shpFilePath, uqCode);

            // EUC-KR 인코딩으로 DBF 속성 읽기
            using var reader = new ShapefileDataReader(shpFilePath, _geometryFactory, EucKr);

            #region 필드명 추출

            var header     = reader.DbaseHeader;
            var fieldCount = header.NumFields;
            var fieldNames = new string[fieldCount];
            for (int i = 0; i < fieldCount; i++)
                fieldNames[i] = header.Fields[i].Name;

            _logger.LogDebug(
                "SHP 필드 목록 ({UqCode}): [{Fields}]",
                uqCode, string.Join(", ", fieldNames));

            #endregion

            #region 피처 읽기 루프

            int loaded  = 0;
            int skipped = 0;

            while (reader.Read())
            {
                var geometry = reader.Geometry;

                if (geometry is null || geometry.IsEmpty)
                {
                    skipped++;
                    continue;
                }

                // 속성 딕셔너리 구성 (대소문자 무시)
                // ShapefileDataReader: index 0은 내부용, 실제 필드는 1부터 시작
                var attributes = new Dictionary<string, object?>(
                    fieldCount + 1,
                    StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < fieldCount; i++)
                {
                    var raw = reader.GetValue(i + 1);
                    if (raw is DBNull || raw is null)
                    {
                        attributes[fieldNames[i]] = null;
                    }
                    else if (raw is string s)
                    {
                        // DBF 문자열 필드의 null 바이트(\0) 및 공백 제거
                        var cleaned = s.Replace("\0", "").Trim();
                        attributes[fieldNames[i]] = cleaned.Length > 0 ? cleaned : null;
                    }
                    else
                    {
                        attributes[fieldNames[i]] = raw;
                    }
                }

                // UQ 코드를 파일명에서 추출해 속성에 주입
                attributes[UqCodeKey] = uqCode;

                result.Add(new LoadedFeature(geometry, attributes));
                loaded++;
            }

            #endregion

            _logger.LogInformation(
                "SHP 로드 완료 ({UqCode}): 성공={Loaded}, 건너뜀={Skipped}",
                uqCode, loaded, skipped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SHP 파일 로드 실패: {Path}", shpFilePath);
        }

        return result;
    }

    #endregion
}

#endregion
