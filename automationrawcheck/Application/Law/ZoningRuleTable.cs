// =============================================================================
// ZoningRuleTable.cs
// 용도지역별 건축 법규 참고 테이블 (국토계획법 기준)
//
// [근거 법령]
//   국토의 계획 및 이용에 관한 법률(국토계획법) 제76조 ~ 제84조
//   국토의 계획 및 이용에 관한 법률 시행령 제71조 ~ 제84조의2
//
// [중요 원칙]
//   - 여기 수치는 '법정 최대치' 참고값입니다. 확정 판정이 아닙니다.
//   - 건폐율/용적률 확정값은 지자체 조례로 결정됩니다 (법정값보다 낮게 설정 가능).
//   - 지구단위계획 지정 구역은 별도 규정이 적용됩니다.
//   - 이 테이블을 건폐율/용적률 자동 확정 판정에 사용하지 마십시오.
//
// [유지보수]
//   법령 개정 시 이 파일만 업데이트하면 됩니다.
//   ATRB_SE 코드 → ZoningRegulationInfo 딕셔너리를 관리합니다.
// =============================================================================

using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Application.Law;

#region ZoningRuleTable 정적 클래스

/// <summary>
/// 용도지역 코드(ATRB_SE)를 키로 하는 건축 법규 참고 정보 조회 테이블입니다.
/// <para>
/// <b>주의</b>: 모든 수치는 국토계획법 기준 참고값입니다. 확정 수치가 아닙니다.
/// </para>
/// </summary>
public static class ZoningRuleTable
{
    #region 테이블 정의

    private static readonly Dictionary<string, ZoningRegulationInfo> _table =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // ── 전용주거지역 ─────────────────────────────────────────────────────
        ["UQA111"] = new()
        {
            ZoneCode                = "UQA111",
            ZoneName                = "제1종전용주거지역",
            BuildingCoverageRatioRef = "50% 이하",
            FloorAreaRatioRef       = "50~100% (조례)",
            AllowedUseSummary       = "단독주택 중심 저밀 주거. 공동주택(아파트) 불허.",
            Restrictions            = new()
            {
                "공동주택(아파트, 연립, 다세대) 원칙적 불허",
                "건폐율·용적률은 지자체 조례로 결정 (법정 상한 이하)",
                "지구단위계획 지정 시 별도 기준 적용 가능"
            }
        },

        ["UQA112"] = new()
        {
            ZoneCode                = "UQA112",
            ZoneName                = "제2종전용주거지역",
            BuildingCoverageRatioRef = "50% 이하",
            FloorAreaRatioRef       = "100~150% (조례)",
            AllowedUseSummary       = "공동주택 중심 저밀 주거. 4층 이하 공동주택 허용.",
            Restrictions            = new()
            {
                "아파트 원칙적 불허 (4층 이하 공동주택 허용)",
                "건폐율·용적률은 지자체 조례로 결정",
                "지구단위계획 지정 시 별도 기준 적용 가능"
            }
        },

        // ── 일반주거지역 ─────────────────────────────────────────────────────
        ["UQA121"] = new()
        {
            ZoneCode                = "UQA121",
            ZoneName                = "제1종일반주거지역",
            BuildingCoverageRatioRef = "60% 이하",
            FloorAreaRatioRef       = "100~200% (조례)",
            AllowedUseSummary       = "단독주택·저밀 공동주택 중심. 4층 이하.",
            Restrictions            = new()
            {
                "4층 이하 건축물 허용 원칙",
                "건폐율·용적률은 지자체 조례로 결정",
                "제1·2종 근린생활시설 일부 허용",
                "지구단위계획 지정 시 별도 기준 적용 가능"
            }
        },

        ["UQA122"] = new()
        {
            ZoneCode                = "UQA122",
            ZoneName                = "제2종일반주거지역",
            BuildingCoverageRatioRef = "60% 이하",
            FloorAreaRatioRef       = "150~250% (조례)",
            AllowedUseSummary       = "공동주택 중심 중밀 주거. 층수 제한은 지자체 조례 확인.",
            Restrictions            = new()
            {
                "건폐율·용적률은 지자체 조례로 결정",
                "서울시는 7층/12층 이하 등 높이 세분 규정 존재",
                "지구단위계획 지정 시 별도 기준 적용 가능"
            }
        },

        ["UQA123"] = new()
        {
            ZoneCode                = "UQA123",
            ZoneName                = "제3종일반주거지역",
            BuildingCoverageRatioRef = "50% 이하",
            FloorAreaRatioRef       = "200~300% (조례)",
            AllowedUseSummary       = "고밀 공동주택 허용. 층수 무제한(단, 높이 기준 적용).",
            Restrictions            = new()
            {
                "건폐율·용적률은 지자체 조례로 결정",
                "일조권·채광·이격거리 규정 엄격 적용",
                "지구단위계획 지정 시 별도 기준 적용 가능"
            }
        },

        // ── 준주거지역 ───────────────────────────────────────────────────────
        ["UQA130"] = new()
        {
            ZoneCode                = "UQA130",
            ZoneName                = "준주거지역",
            BuildingCoverageRatioRef = "70% 이하",
            FloorAreaRatioRef       = "200~500% (조례)",
            AllowedUseSummary       = "주거 기능 + 상업·업무 기능 혼재 가능.",
            Restrictions            = new()
            {
                "건폐율·용적률은 지자체 조례로 결정",
                "판매·업무시설 등 비주거 용도 상당 부분 허용",
                "지구단위계획 지정 시 별도 기준 적용 가능"
            }
        },

        // ── 상업지역 ─────────────────────────────────────────────────────────
        ["UQA210"] = new()
        {
            ZoneCode                = "UQA210",
            ZoneName                = "중심상업지역",
            BuildingCoverageRatioRef = "90% 이하",
            FloorAreaRatioRef       = "400~1,500% (조례)",
            AllowedUseSummary       = "도심 핵심 상업·업무. 고층 복합 개발 허용.",
            Restrictions            = new()
            {
                "건폐율·용적률은 지자체 조례로 결정 (법정 상한 이하)",
                "주거시설(아파트) 비율 제한 적용 가능",
                "지구단위계획 지정 시 별도 기준 적용 가능"
            }
        },

        ["UQA220"] = new()
        {
            ZoneCode                = "UQA220",
            ZoneName                = "일반상업지역",
            BuildingCoverageRatioRef = "80% 이하",
            FloorAreaRatioRef       = "300~1,300% (조례)",
            AllowedUseSummary       = "일반적인 상업·업무. 판매·숙박·업무시설 등 허용.",
            Restrictions            = new()
            {
                "건폐율·용적률은 지자체 조례로 결정",
                "지구단위계획 지정 시 별도 기준 적용 가능"
            }
        },

        ["UQA230"] = new()
        {
            ZoneCode                = "UQA230",
            ZoneName                = "근린상업지역",
            BuildingCoverageRatioRef = "70% 이하",
            FloorAreaRatioRef       = "200~900% (조례)",
            AllowedUseSummary       = "근린 생활권 중심 상업. 주거 혼용 가능.",
            Restrictions            = new()
            {
                "건폐율·용적률은 지자체 조례로 결정",
                "지구단위계획 지정 시 별도 기준 적용 가능"
            }
        },

        ["UQA240"] = new()
        {
            ZoneCode                = "UQA240",
            ZoneName                = "유통상업지역",
            BuildingCoverageRatioRef = "80% 이하",
            FloorAreaRatioRef       = "200~1,100% (조례)",
            AllowedUseSummary       = "도매·물류·유통 중심. 대형 판매·물류시설 허용.",
            Restrictions            = new()
            {
                "건폐율·용적률은 지자체 조례로 결정",
                "주거시설 불허 또는 엄격히 제한",
                "지구단위계획 지정 시 별도 기준 적용 가능"
            }
        },

        // ── 공업지역 ─────────────────────────────────────────────────────────
        ["UQA310"] = new()
        {
            ZoneCode                = "UQA310",
            ZoneName                = "전용공업지역",
            BuildingCoverageRatioRef = "70% 이하",
            FloorAreaRatioRef       = "150~300% (조례)",
            AllowedUseSummary       = "중화학공업·공해산업 등 전용 공업. 주거 불허.",
            Restrictions            = new()
            {
                "주거·상업시설 원칙적 불허",
                "환경·소음 규제 엄격 적용",
                "건폐율·용적률은 지자체 조례로 결정"
            }
        },

        ["UQA320"] = new()
        {
            ZoneCode                = "UQA320",
            ZoneName                = "일반공업지역",
            BuildingCoverageRatioRef = "70% 이하",
            FloorAreaRatioRef       = "200~350% (조례)",
            AllowedUseSummary       = "환경저해 우려가 크지 않은 일반공업. 일부 지원시설 허용.",
            Restrictions            = new()
            {
                "주거시설 원칙적 불허",
                "건폐율·용적률은 지자체 조례로 결정"
            }
        },

        ["UQA330"] = new()
        {
            ZoneCode                = "UQA330",
            ZoneName                = "준공업지역",
            BuildingCoverageRatioRef = "70% 이하",
            FloorAreaRatioRef       = "200~400% (조례)",
            AllowedUseSummary       = "경공업·첨단산업 + 주거·상업 혼재 가능. 복합개발 허용.",
            Restrictions            = new()
            {
                "주거·상업시설 일정 범위 허용 (지자체 조례 확인 필요)",
                "건폐율·용적률은 지자체 조례로 결정",
                "지구단위계획 지정 시 별도 기준 적용 가능"
            }
        },

        // ── 녹지지역 ─────────────────────────────────────────────────────────
        ["UQA410"] = new()
        {
            ZoneCode                = "UQA410",
            ZoneName                = "보전녹지지역",
            BuildingCoverageRatioRef = "20% 이하",
            FloorAreaRatioRef       = "50~80% (조례)",
            AllowedUseSummary       = "자연환경·경관·산림 보전. 건축 행위 극히 제한.",
            Restrictions            = new()
            {
                "건축 허용 용도 극히 제한 (농림·어업시설, 마을공동시설 등 일부만 허용)",
                "형질변경 원칙적 불허",
                "건폐율·용적률은 지자체 조례로 결정"
            }
        },

        ["UQA420"] = new()
        {
            ZoneCode                = "UQA420",
            ZoneName                = "생산녹지지역",
            BuildingCoverageRatioRef = "20% 이하",
            FloorAreaRatioRef       = "50~100% (조례)",
            AllowedUseSummary       = "농업·임업·어업 생산 위주. 관련 시설 허용.",
            Restrictions            = new()
            {
                "농업·임업·어업 관련 시설 중심으로 허용 용도 제한",
                "건폐율·용적률은 지자체 조례로 결정"
            }
        },

        ["UQA430"] = new()
        {
            ZoneCode                = "UQA430",
            ZoneName                = "자연녹지지역",
            BuildingCoverageRatioRef = "20% 이하",
            FloorAreaRatioRef       = "50~100% (조례)",
            AllowedUseSummary       = "불가피한 개발 허용 녹지. 제한적 건축 가능.",
            Restrictions            = new()
            {
                "개발 행위 엄격히 제한되나 특정 용도는 조건부 허용",
                "건폐율·용적률은 지자체 조례로 결정",
                "개발제한구역 중첩 여부 반드시 추가 확인 필요"
            }
        },

        // ── 기타 특수 ─────────────────────────────────────────────────────────
        ["UDV100"] = new()
        {
            ZoneCode                = "UDV100",
            ZoneName                = "개발제한구역(그린벨트)",
            BuildingCoverageRatioRef = "규정 없음 (개별 허가)",
            FloorAreaRatioRef       = "규정 없음 (개별 허가)",
            AllowedUseSummary       = "건축 행위 원칙적 금지. 예외적 허가제.",
            Restrictions            = new()
            {
                "건축 행위 원칙적 전면 금지",
                "국토교통부 장관 또는 지자체 허가 시에만 예외적 허용",
                "허용 용도: 농림어업·주민복지 관련 시설 등 극히 제한적",
                "개발제한구역의 지정 및 관리에 관한 특별조치법 적용"
            }
        },

        ["UQT600"] = new()
        {
            ZoneCode                = "UQT600",
            ZoneName                = "도시자연공원구역",
            BuildingCoverageRatioRef = "규정 없음 (개별 허가)",
            FloorAreaRatioRef       = "규정 없음 (개별 허가)",
            AllowedUseSummary       = "자연경관·생태계 보전. 공원·산림 관련 시설 외 불허.",
            Restrictions            = new()
            {
                "개발 행위 원칙적 금지",
                "도시공원법 적용 — 공원 시설 외 건축 불허",
                "관할 지자체 공원녹지과 확인 필수"
            }
        },
    };

    #endregion

    #region 조회 메서드

    /// <summary>
    /// 용도지역 코드(ATRB_SE)로 건축 법규 참고 정보를 조회합니다.
    /// </summary>
    /// <param name="zoneCode">ATRB_SE 코드 (예: "UQA122")</param>
    /// <returns>
    /// 해당 코드의 참고 정보.
    /// 매핑 없으면 기본 안내 정보 반환 (null 반환하지 않음).
    /// </returns>
    public static ZoningRegulationInfo GetInfo(string zoneCode)
    {
        if (!string.IsNullOrWhiteSpace(zoneCode) &&
            _table.TryGetValue(zoneCode, out var info))
        {
            return info;
        }

        // 매핑 없는 코드 → 일반 안내
        return new ZoningRegulationInfo
        {
            ZoneCode                = zoneCode ?? string.Empty,
            ZoneName                = "알 수 없음",
            BuildingCoverageRatioRef = "지자체 조례 확인 필요",
            FloorAreaRatioRef       = "지자체 조례 확인 필요",
            AllowedUseSummary       = "허용 용도는 관할 지자체 도시계획부서 확인이 필요합니다.",
            Restrictions            = new()
            {
                "코드가 규칙 테이블에 등록되어 있지 않습니다.",
                "관할 지자체 또는 토지이음(eum.go.kr) 확인을 권장합니다."
            }
        };
    }

    /// <summary>
    /// 테이블에 해당 코드가 있는지 확인합니다.
    /// </summary>
    public static bool Contains(string zoneCode) =>
        !string.IsNullOrWhiteSpace(zoneCode) && _table.ContainsKey(zoneCode);

    #endregion
}

#endregion
