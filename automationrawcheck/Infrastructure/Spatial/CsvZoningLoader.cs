// =============================================================================
// CsvZoningLoader.cs
// WKT 컬럼 포함 CSV 파일 로딩 전담 클래스
// - ShapefileZoningLayerProvider에서 SHP 파일 없을 때 fallback으로 호출합니다.
// =============================================================================

using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System.Globalization;
using System.Text;

namespace AutomationRawCheck.Infrastructure.Spatial;

#region CsvZoningLoader 클래스

/// <summary>
/// WKT(Well-Known Text) 컬럼을 포함한 CSV 파일에서
/// <see cref="LoadedFeature"/> 목록을 로드하는 전담 클래스입니다.
/// <para>
/// <see cref="ShapefileZoningLayerProvider"/>에서 SHP 파일이 없을 때 fallback으로 사용됩니다.
/// </para>
/// <para>
/// CSV 파일 요구사항:
/// <list type="bullet">
///   <item>헤더 행 포함</item>
///   <item>WKT 컬럼 포함 (컬럼명은 <see cref="WktColumnName"/> 상수로 지정)</item>
///   <item>인코딩: UTF-8 (EUC-KR 파일은 변환 필요)</item>
/// </list>
/// </para>
/// TODO (인코딩):
/// 한국 공간 데이터 CSV 중 EUC-KR 인코딩을 사용하는 경우
/// <see cref="DefaultEncoding"/>을 Encoding.GetEncoding("euc-kr")로 변경하세요.
/// </summary>
public sealed class CsvZoningLoader
{
    #region 상수

    /// <summary>
    /// CSV 파일의 WKT 컬럼명.
    /// 실제 CSV 파일의 WKT 컬럼명이 다르면 이 상수를 수정하세요.
    /// </summary>
    public const string WktColumnName = "WKT";

    /// <summary>
    /// CSV 파일 기본 인코딩.
    /// TODO: EUC-KR 파일이면 Encoding.GetEncoding("euc-kr")로 변경.
    /// </summary>
    private static readonly Encoding DefaultEncoding = Encoding.UTF8;

    #endregion

    #region 필드 및 생성자

    private readonly ILogger<CsvZoningLoader> _logger;
    private readonly GeometryFactory _geometryFactory;

    /// <summary>CsvZoningLoader를 초기화합니다.</summary>
    public CsvZoningLoader(ILogger<CsvZoningLoader> logger, GeometryFactory geometryFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _geometryFactory = geometryFactory ?? throw new ArgumentNullException(nameof(geometryFactory));
    }

    #endregion

    #region CSV 로드 메서드

    /// <summary>
    /// 지정된 경로의 CSV 파일을 읽어 <see cref="LoadedFeature"/> 목록을 반환합니다.
    /// </summary>
    /// <param name="csvFilePath">CSV 파일의 전체 경로</param>
    /// <returns>로드된 피처 목록. 실패 시 빈 목록.</returns>
    public List<LoadedFeature> Load(string csvFilePath)
    {
        var result = new List<LoadedFeature>();
        var wktReader = new WKTReader(_geometryFactory.GeometryServices);

        try
        {
            _logger.LogInformation("CSV 파일 로드 시작: {Path}", csvFilePath);

            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,   // 누락 필드 무시
                BadDataFound = null,        // 잘못된 데이터 무시
                Encoding = DefaultEncoding
            };

            using var streamReader = new StreamReader(csvFilePath, DefaultEncoding);
            using var csvReader = new CsvReader(streamReader, csvConfig);

            #region 헤더 읽기

            csvReader.Read();
            csvReader.ReadHeader();
            var headers = csvReader.HeaderRecord;

            if (headers is null || headers.Length == 0)
            {
                _logger.LogWarning("CSV 헤더 없음 또는 비어있음: {Path}", csvFilePath);
                return result;
            }

            _logger.LogDebug("CSV 필드 목록: [{Fields}]", string.Join(", ", headers));

            #endregion

            #region 피처 읽기 루프

            int loaded = 0;
            int skipped = 0;

            while (csvReader.Read())
            {
                // WKT 컬럼 읽기
                var wkt = csvReader.GetField<string?>(WktColumnName);
                if (string.IsNullOrWhiteSpace(wkt))
                {
                    skipped++;
                    continue;
                }

                // WKT 파싱
                Geometry? geometry;
                try
                {
                    geometry = wktReader.Read(wkt);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "WKT 파싱 실패 (건너뜀). WKT 앞부분: {Wkt}",
                        wkt.Length > 50 ? wkt[..50] + "..." : wkt);
                    skipped++;
                    continue;
                }

                if (geometry is null || geometry.IsEmpty)
                {
                    skipped++;
                    continue;
                }

                // 속성 딕셔너리 구성 (WKT 컬럼 제외)
                var attributes = new Dictionary<string, object?>(
                    headers.Length,
                    StringComparer.OrdinalIgnoreCase);

                foreach (var col in headers)
                {
                    if (col.Equals(WktColumnName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    attributes[col] = csvReader.GetField<string?>(col);
                }

                result.Add(new LoadedFeature(geometry, attributes));
                loaded++;
            }

            #endregion

            _logger.LogInformation(
                "CSV 로드 완료: 성공={Loaded}, 건너뜀={Skipped}",
                loaded, skipped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSV 파일 로드 실패: {Path}", csvFilePath);
        }

        return result;
    }

    #endregion
}

#endregion
