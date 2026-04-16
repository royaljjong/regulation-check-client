using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Interfaces;
using CsvHelper;
using CsvHelper.Configuration;

namespace AutomationRawCheck.Application.Services;

public sealed class CsvInputAutomationService
{
    private static readonly string[] LabelColumns = ["label", "item", "name", "field", "항목", "항목명", "구분", "지표"];
    private static readonly string[] ValueColumns = ["value", "값", "내용", "수치", "data", "결과"];

    private readonly ICsvInputAutomationStore _store;

    public CsvInputAutomationService(ICsvInputAutomationStore store)
    {
        _store = store;
    }

    public async Task<CsvInputAutomationResultDto> ImportAsync(string fileName, Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken);
        var delimiter = DetectDelimiter(content);
        var rows = ReadRows(content, delimiter);
        var result = BuildResult(fileName, delimiter, rows);
        return _store.Save(result);
    }

    public CsvInputAutomationResultDto? Get(string token)
    {
        return _store.Get(token);
    }

    private static string DetectDelimiter(string content)
    {
        var firstLine = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var candidates = new Dictionary<string, int>
        {
            [","] = firstLine.Count(c => c == ','),
            [";"] = firstLine.Count(c => c == ';'),
            ["\t"] = firstLine.Count(c => c == '\t')
        };

        return candidates.OrderByDescending(kv => kv.Value).First().Key;
    }

    private static List<Dictionary<string, string?>> ReadRows(string content, string delimiter)
    {
        using var stringReader = new StringReader(content);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            BadDataFound = null,
            MissingFieldFound = null,
            HeaderValidated = null,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim
        };

        using var csv = new CsvReader(stringReader, config);
        if (!csv.Read())
        {
            return [];
        }

        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];
        var rows = new List<Dictionary<string, string?>>();

        while (csv.Read())
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers)
            {
                if (string.IsNullOrWhiteSpace(header))
                {
                    continue;
                }

                row[header] = csv.GetField(header);
            }

            if (row.Count > 0)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private static CsvInputAutomationResultDto BuildResult(string fileName, string delimiter, List<Dictionary<string, string?>> rows)
    {
        var observations = BuildObservations(rows);
        var mappings = new List<CsvFieldMappingDto>();
        var warnings = new List<string>();

        var siteArea = FindDouble(observations, mappings, "siteArea", "high", "대지면적", "부지면적", "sitearea");
        var buildingArea = FindDouble(observations, mappings, "buildingArea", "high", "건축면적", "buildingarea");
        var floorArea = FindDouble(observations, mappings, "floorArea", "high", "연면적", "총연면적", "floorarea", "grossfloorarea");
        var floorCount = FindInt(observations, mappings, "floorCount", "medium", "층수", "지상층수", "floorcount");
        var buildingHeight = FindDouble(observations, mappings, "buildingHeight", "medium", "높이", "건축높이", "buildingheight");
        var roadFrontageWidth = FindDouble(observations, mappings, "roadFrontageWidth", "medium", "도로폭", "전면도로폭", "roadwidth");
        var unitCount = FindInt(observations, mappings, "unitCount", "medium", "세대수", "unitcount");
        var roomCount = FindInt(observations, mappings, "roomCount", "medium", "실수", "roomcount");
        var guestRoomCount = FindInt(observations, mappings, "guestRoomCount", "high", "객실수", "guestroomcount");
        var bedCount = FindInt(observations, mappings, "bedCount", "high", "병상수", "bedcount");
        var studentCount = FindInt(observations, mappings, "studentCount", "high", "학생수", "원생수", "studentcount");
        var occupantCount = FindInt(observations, mappings, "occupantCount", "medium", "수용인원", "재실인원", "예상인원", "occupantcount");
        var vehicleIngressType = FindString(observations, mappings, "vehicleIngressType", "medium", "차량출입방식", "차량진입방식", "차량동선");
        var detailUseSubtype = FindString(observations, mappings, "detailUseSubtype", "medium", "업종", "세부용도", "detailusesubtype");
        var hazardousMaterialProfile = FindString(observations, mappings, "hazardousMaterialProfile", "medium", "위험물", "위험물프로필", "hazardousmaterialprofile");
        var logisticsOperationProfile = FindString(observations, mappings, "logisticsOperationProfile", "medium", "물류운영", "물류운영프로필", "logisticsoperationprofile");
        var medicalSpecialCriteria = FindString(observations, mappings, "medicalSpecialCriteria", "medium", "의료특수기준", "medicalspecialcriteria");
        var educationSpecialCriteria = FindString(observations, mappings, "educationSpecialCriteria", "medium", "교육특수기준", "educationspecialcriteria");
        var accommodationSpecialCriteria = FindString(observations, mappings, "accommodationSpecialCriteria", "medium", "숙박특수기준", "accommodationspecialcriteria");
        var selectedUse = FindString(observations, mappings, "selectedUse", "medium", "주용도", "계획용도", "selecteduse");

        if (siteArea is null && floorArea is null && buildingArea is null)
        {
            warnings.Add("면적 관련 핵심 필드를 찾지 못했습니다. CSV 헤더나 항목명을 확인하세요.");
        }

        var suggestedInputs = new BuildingInputsDto
        {
            SiteArea = siteArea,
            BuildingArea = buildingArea,
            FloorArea = floorArea,
            FloorCount = floorCount,
            BuildingHeight = buildingHeight,
            RoadFrontageWidth = roadFrontageWidth,
            UnitCount = unitCount,
            RoomCount = roomCount,
            GuestRoomCount = guestRoomCount,
            BedCount = bedCount,
            StudentCount = studentCount,
            OccupantCount = occupantCount,
            VehicleIngressType = vehicleIngressType,
            DetailUseSubtype = detailUseSubtype,
            HazardousMaterialProfile = hazardousMaterialProfile,
            LogisticsOperationProfile = logisticsOperationProfile,
            MedicalSpecialCriteria = medicalSpecialCriteria,
            EducationSpecialCriteria = educationSpecialCriteria,
            AccommodationSpecialCriteria = accommodationSpecialCriteria
        };

        var previewRows = rows
            .Take(5)
            .Select((row, index) => new CsvUploadRowPreviewDto
            {
                RowNumber = index + 1,
                Cells = row.Select(cell => new CsvUploadCellDto
                {
                    Column = cell.Key,
                    Value = cell.Value
                }).ToList()
            })
            .ToList();

        var summaryLines = new List<string>
        {
            $"{rows.Count}개 행을 읽었습니다.",
            $"{mappings.Count}개 입력 후보를 추론했습니다."
        };

        if (!string.IsNullOrWhiteSpace(selectedUse))
        {
            summaryLines.Add($"용도 후보: {selectedUse}");
        }

        if (siteArea is not null)
        {
            summaryLines.Add($"대지면적 추론값: {siteArea:0.##}㎡");
        }

        if (floorArea is not null)
        {
            summaryLines.Add($"연면적 추론값: {floorArea:0.##}㎡");
        }

        return new CsvInputAutomationResultDto
        {
            Token = $"csv_{Guid.NewGuid():N}",
            FileName = fileName,
            Delimiter = delimiter == "\t" ? "\\t" : delimiter,
            RowCount = rows.Count,
            SuggestedSelectedUse = selectedUse,
            SuggestedReviewLevel = InferReviewLevel(suggestedInputs),
            SuggestedBuildingInputs = suggestedInputs,
            Mappings = mappings,
            PreviewRows = previewRows,
            SummaryLines = summaryLines,
            Warnings = warnings
        };
    }

    private static List<(string Label, string NormalizedKey, string? Value)> BuildObservations(List<Dictionary<string, string?>> rows)
    {
        var observations = new List<(string Label, string NormalizedKey, string? Value)>();

        foreach (var row in rows)
        {
            var labelCell = row.FirstOrDefault(kv => LabelColumns.Contains(Normalize(kv.Key)));
            var valueCell = row.FirstOrDefault(kv => ValueColumns.Contains(Normalize(kv.Key)));

            if (!string.IsNullOrWhiteSpace(labelCell.Value) && !string.IsNullOrWhiteSpace(valueCell.Value))
            {
                observations.Add((labelCell.Value!, Normalize(labelCell.Value!), valueCell.Value));
            }

            foreach (var cell in row)
            {
                if (string.IsNullOrWhiteSpace(cell.Value))
                {
                    continue;
                }

                observations.Add((cell.Key, Normalize(cell.Key), cell.Value));
            }
        }

        return observations;
    }

    private static double? FindDouble(List<(string Label, string NormalizedKey, string? Value)> observations, List<CsvFieldMappingDto> mappings, string targetField, string confidence, params string[] keywords)
    {
        var candidate = FindCandidate(observations, keywords);
        if (!candidate.HasValue)
        {
            return null;
        }

        var parsed = ParseDouble(candidate.Value.Value.Value);
        if (parsed is null)
        {
            return null;
        }

        mappings.Add(ToMapping(candidate.Value.Value, targetField, confidence));
        return parsed;
    }

    private static int? FindInt(List<(string Label, string NormalizedKey, string? Value)> observations, List<CsvFieldMappingDto> mappings, string targetField, string confidence, params string[] keywords)
    {
        var candidate = FindCandidate(observations, keywords);
        if (!candidate.HasValue)
        {
            return null;
        }

        var parsed = ParseInt(candidate.Value.Value.Value);
        if (parsed is null)
        {
            return null;
        }

        mappings.Add(ToMapping(candidate.Value.Value, targetField, confidence));
        return parsed;
    }

    private static string? FindString(List<(string Label, string NormalizedKey, string? Value)> observations, List<CsvFieldMappingDto> mappings, string targetField, string confidence, params string[] keywords)
    {
        var candidate = FindCandidate(observations, keywords);
        if (!candidate.HasValue || string.IsNullOrWhiteSpace(candidate.Value.Value.Value))
        {
            return null;
        }

        mappings.Add(ToMapping(candidate.Value.Value, targetField, confidence));
        return candidate.Value.Value.Value?.Trim();
    }

    private static KeyValuePair<int, (string Label, string NormalizedKey, string? Value)>? FindCandidate(List<(string Label, string NormalizedKey, string? Value)> observations, params string[] keywords)
    {
        var normalizedKeywords = keywords.Select(Normalize).ToArray();
        var indexed = observations.Select((value, index) => new KeyValuePair<int, (string Label, string NormalizedKey, string? Value)>(index, value));
        var candidate = indexed.FirstOrDefault(entry => normalizedKeywords.Any(keyword => entry.Value.NormalizedKey.Contains(keyword, StringComparison.Ordinal)));
        return EqualityComparer<KeyValuePair<int, (string Label, string NormalizedKey, string? Value)>>.Default.Equals(candidate, default)
            ? null
            : candidate;
    }

    private static CsvFieldMappingDto ToMapping((string Label, string NormalizedKey, string? Value) candidate, string targetField, string confidence)
    {
        return new CsvFieldMappingDto
        {
            SourceLabel = candidate.Label,
            NormalizedKey = candidate.NormalizedKey,
            Value = candidate.Value,
            TargetField = targetField,
            Confidence = confidence
        };
    }

    private static string InferReviewLevel(BuildingInputsDto inputs)
    {
        if (inputs.MedicalSpecialCriteria is not null ||
            inputs.EducationSpecialCriteria is not null ||
            inputs.HazardousMaterialProfile is not null ||
            inputs.LogisticsOperationProfile is not null ||
            inputs.AccommodationSpecialCriteria is not null)
        {
            return "detailed";
        }

        if (inputs.UnitCount is not null ||
            inputs.GuestRoomCount is not null ||
            inputs.BedCount is not null ||
            inputs.StudentCount is not null ||
            inputs.OccupantCount is not null)
        {
            return "standard";
        }

        return "quick";
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch) || ch >= 0xAC00)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static double? ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = Regex.Match(value, @"-?\d[\d,]*\.?\d*");
        if (!match.Success)
        {
            return null;
        }

        var normalized = match.Value.Replace(",", string.Empty);
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static int? ParseInt(string? value)
    {
        var parsed = ParseDouble(value);
        return parsed is null ? null : Convert.ToInt32(Math.Round(parsed.Value, MidpointRounding.AwayFromZero));
    }
}
