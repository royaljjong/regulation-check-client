// =============================================================================
// reportFormatter.js
// 건축 법규 사전 검토 결과 TXT 리포트 생성기
//
// 출력 순서 (HARNESS 워크플로우 고정):
//   [1] 조회 기준 정보
//   [2] 토지 법규 조회 결과
//   [3] 계획 용도
//   [4] 건축 기본 법규 (Core Layer)         ← selectedUse 있을 때만
//   [5] 건축 필수 연계 법규 (Extended Core) ← selectedUse 있을 때만
//   [6] 협력사 법규 (MEP Layer)             ← selectedUse 있을 때만
//   [7] 우선 검토 항목 (ReviewItems)        ← selectedUse 있을 때만
//   [8] 주의사항                            ← selectedUse 있을 때만
//   [9] 간이 계산기 결과 (BCR/FAR)          ← selectedUse 있을 때만
//   [부록] 판정 데이터 참고 정보 (note 있을 때만)
// =============================================================================

// ─────────────────────────────────────────────────────────────────────────────
// 상수
// ─────────────────────────────────────────────────────────────────────────────

const CATEGORY_ORDER = [
  '중첩규제', '지구단위계획', '허용용도', '밀도', '도로/건축선',
  '주차', '피난/계단', '승강기', '방화', '기타',
]

const LINE_THIN  = '─'.repeat(52)
const LINE_THICK = '═'.repeat(52)

// ─────────────────────────────────────────────────────────────────────────────
// 순수 헬퍼 함수
// ─────────────────────────────────────────────────────────────────────────────

function formatCoord(v) {
  return typeof v === 'number' ? v.toFixed(6) : '-'
}

function toYesNo(v) {
  return v === true ? '예' : v === false ? '아니오' : '미확인'
}

function getConfidence(res) {
  if (!res?.zoning) return { label: '신뢰도 낮음', explanation: '데이터 미매칭 (참고용)' }
  const dist = res.debug?.nearestDistanceMeters || 0
  if (dist > 0.1) return { label: '신뢰도 중간', explanation: '경계 인접 또는 일부 불확실 요소' }
  return { label: '신뢰도 높음', explanation: '데이터 직접 매칭 (법적 확정 아님)' }
}

function getDevRestrictionDisplay(res) {
  const dr = res?.extraLayers?.developmentRestriction
  if (!dr) {
    const v = res?.overlayFlags?.isDevelopmentRestricted
    return { text: toYesNo(v), badge: null, note: null, status: null }
  }
  if (dr.status === 'unavailable') return { text: '판정 불가', badge: null, note: dr.note, status: 'unavailable' }
  return {
    text:   dr.isInside ? '개발제한구역' : '해당 없음',
    badge:  dr.status === 'fallback' ? '(참고값)' : null,
    note:   dr.note,
    status: dr.status,
  }
}

function getDistrictUnitPlanDisplay(res) {
  const dup = res?.extraLayers?.districtUnitPlan
  if (!dup) {
    const v = res?.overlayFlags?.isInDistrictUnitPlan
    return { text: toYesNo(v), badge: null, note: null, status: null }
  }
  if (dup.status === 'unavailable') return { text: '판정 불가', badge: null, note: dup.note, status: 'unavailable' }
  return {
    text:   dup.isInside ? '지구단위계획구역' : '해당 없음',
    badge:  dup.status === 'fallback' ? '(참고값)' : null,
    note:   dup.note,
    status: dup.status,
  }
}

function sortRegulations(regs) {
  const ord = { '[제한]': 1, '[계획]': 2, '[용도]': 3, '[밀도]': 4, '[공통]': 5 }
  const get  = s => { for (const k in ord) if (s.startsWith(k)) return ord[k]; return 99 }
  return [...regs].sort((a, b) => get(a) - get(b))
}

function stepHeader(num, title) {
  return [``, LINE_THIN, `[${num}] ${title}`, LINE_THIN]
}

function confLabel(level) {
  return level === 'high' ? '높음' : level === 'medium' ? '중간' : '낮음'
}

function sourceLine(text) {
  return `  ※ 출처: ${text}`
}

function sourceDisplayText(source, confidence) {
  const srcLabel = {
    rule:       '내부 규칙 테이블',
    calculated: '입력값 산술 계산',
    api:        'API 직접 확인',
    shp:        'SHP 공간 데이터',
    none:       '데이터 없음',
  }[source] ?? source ?? '미상'
  const conf = confidence === 'high' ? '높음' : confidence === 'medium' ? '중간' : '낮음'
  return `${srcLabel}  신뢰도: ${conf}`
}

// ─────────────────────────────────────────────────────────────────────────────
// 주의사항 고정 문구
// ─────────────────────────────────────────────────────────────────────────────

function cautionLines() {
  return [
    '• 면적·층수·용도 혼합 여부에 따라 적용 법규가 달라집니다. 개별 조건을 반드시 확인하세요.',
    '• 지자체 조례 및 지구단위계획 기준이 일반 법규보다 우선 적용될 수 있습니다.',
    '• 소방·설비·전기 기준은 협력사와 별도 협의가 필요합니다.',
    '• 본 분석은 공공 공간데이터 기반 1차 참고용 결과입니다.',
    '  실제 인허가 가능 여부는 관할 관청에 확인하십시오.',
  ]
}

// ─────────────────────────────────────────────────────────────────────────────
// 메인 포매터
// ─────────────────────────────────────────────────────────────────────────────

/**
 * 건축 법규 사전 검토 결과를 TXT 문자열로 반환합니다.
 *
 * @param {object} params
 * @param {object}        params.lookupInfo          - { label, mode }
 * @param {object}        params.selectedCoordinate  - { lon, lat }
 * @param {object}        params.result              - API 응답 (RegulationCheckResponseDto)
 * @param {string|null}   params.selectedUse         - 계획 용도 (null이면 [4]~[9] 생략)
 * @param {object|null}   params.lawLayers           - { coreLaws[], extendedCoreLaws[], mepLaws[], dataSource?, confidence? }
 * @param {Array|null}    params.reviewItems         - ReviewItemDto[]
 * @param {object|null}   params.reviewMeta          - { dataSource, confidence } (선택)
 * @param {Array|null}    params.calcResult          - CalculatorResultItemDto[]
 * @param {object|null}   params.calcMeta            - { dataSource, confidence } (선택)
 * @returns {string}
 */
export function buildReportText({
  lookupInfo,
  selectedCoordinate,
  result,
  selectedUse,
  lawLayers,
  reviewItems,
  reviewMeta,
  calcResult,
  calcMeta,
}) {
  const lines = []
  const conf   = getConfidence(result)
  const drDisp = getDevRestrictionDisplay(result)
  const dupDisp = getDistrictUnitPlanDisplay(result)

  // ── 문서 제목 ────────────────────────────────────────────────────────────
  lines.push(LINE_THICK)
  lines.push('  건축 법규 사전 검토 결과')
  lines.push(`  분석일시: ${new Date().toLocaleString('ko-KR')}`)
  lines.push(LINE_THICK)

  // ── [1] 조회 기준 정보 ───────────────────────────────────────────────────
  lines.push(...stepHeader(1, '조회 기준 정보'))
  lines.push(`조회 위치 : ${lookupInfo?.label || '선택된 좌표 지점'}`)
  lines.push(`조회 방식 : ${lookupInfo?.mode === 'address' ? '주소 기준 (V-World Geocoding)' : '좌표(WGS84) 직접 지정'}`)
  lines.push(`좌표      : Lon ${formatCoord(selectedCoordinate.lon)}  /  Lat ${formatCoord(selectedCoordinate.lat)}`)
  lines.push(`신뢰도    : ${conf.label}  (${conf.explanation})`)
  if ((result.debug?.nearestDistanceMeters ?? 0) > 0) {
    lines.push(`경계 거리 : ${result.debug.nearestDistanceMeters.toFixed(2)} m`)
  }
  lines.push(`데이터 출처: ${result.meta?.layerName?.split('(')[0]?.trim() || '공공 공간 데이터'}`)
  if (result.dataTrust) {
    const dt = result.dataTrust
    lines.push(`용도지역 데이터: ${dt.zoningSource === 'api' ? 'API 확인' : 'SHP 로컬 데이터'}  (신뢰도 ${confLabel(dt.zoningConfidence)})`)
    lines.push(`개발제한구역  : 신뢰도 ${confLabel(dt.devRestrictionConfidence)}`)
    lines.push(`지구단위계획  : 신뢰도 ${confLabel(dt.districtUnitPlanConfidence)}`)
    lines.push(`종합 신뢰도   : ${confLabel(dt.overallConfidence)}`)
    if (dt.overallNote) lines.push(`ℹ ${dt.overallNote}`)
  }
  if (result.debug?.responseTimeMs != null) {
    lines.push(`서버 응답시간 : ${result.debug.responseTimeMs}ms`)
  }
  lines.push(`* 신뢰도는 공간 데이터 매칭 정확도이며 인허가 확정 판단이 아닙니다.`)

  // ── [2] 토지 법규 조회 결과 ──────────────────────────────────────────────
  lines.push(...stepHeader(2, '토지 법규 조회 결과'))

  if (result.summaryText) {
    lines.push('[1차 판단 요약]')
    result.summaryText.split('\n').forEach(l => lines.push(`  ${l}`))
    lines.push('')
  }

  lines.push('[공간 규제 레이어 상태]')
  lines.push(`  개발제한구역         : ${drDisp.text}${drDisp.badge ? '  ' + drDisp.badge : ''}`)
  lines.push(`  지구단위계획         : ${dupDisp.text}${dupDisp.badge ? '  ' + dupDisp.badge : ''}`)
  lines.push(`  개발행위허가제한지역 : ${toYesNo(result.overlayFlags?.isDevelopmentActionRestricted)}`)
  lines.push('')

  const sortedReg = sortRegulations(result?.relatedRegulations || [])
  lines.push('[1차 판정 법규 목록]')
  if (sortedReg.length > 0) {
    sortedReg.forEach(r => lines.push(`  ${r}`))
  } else {
    lines.push('  없음')
  }

  // ── [3] 계획 용도 ────────────────────────────────────────────────────────
  lines.push(...stepHeader(3, '계획 용도'))
  if (selectedUse) {
    lines.push(`선택 용도   : ${selectedUse}`)
    if (result.zoning?.zoneName) {
      lines.push(`판정 용도지역: ${result.zoning.zoneName}`)
    }
  } else {
    lines.push('(계획 용도가 선택되지 않았습니다.)')
    lines.push('용도를 선택하면 [4]~[9] 항목이 포함된 전체 리포트를 저장할 수 있습니다.')
    lines.push('')
    lines.push(LINE_THICK)
    lines.push('※ 본 분석은 공공 공간데이터를 기반으로 추출된 1차 참고용 결과입니다.')
    lines.push('※ 실제 인허가 가능 여부는 조례 및 지침에 따르므로 반드시 관할 관청에 확인하십시오.')
    lines.push(LINE_THICK)
    lines.push(..._appendix(drDisp, dupDisp))
    return lines.join('\n')
  }

  // ── [4] 건축 기본 법규 (Core Layer) ─────────────────────────────────────
  lines.push(...stepHeader(4, '건축 기본 법규  (Core Layer)'))
  const coreLaws = lawLayers?.coreLaws ?? []
  if (coreLaws.length > 0) {
    coreLaws.forEach((item, i) => {
      lines.push(`${String(i + 1).padStart(2)}. ${item.law}`)
      lines.push(`    → ${item.scope}`)
    })
    lines.push(sourceLine(sourceDisplayText(lawLayers?.source ?? lawLayers?.dataSource ?? 'rule', lawLayers?.confidence ?? 'low')))
  } else {
    lines.push('  (데이터 없음 — 용도 선택 후 재조회하세요.)')
  }

  // ── [5] 건축 필수 연계 법규 (Extended Core) ──────────────────────────────
  lines.push(...stepHeader(5, '건축 필수 연계 법규  (Extended Core)'))
  const extLaws = lawLayers?.extendedCoreLaws ?? []
  if (extLaws.length > 0) {
    extLaws.forEach((item, i) => {
      lines.push(`${String(i + 1).padStart(2)}. ${item.law}`)
      lines.push(`    → ${item.scope}`)
    })
    lines.push(sourceLine(sourceDisplayText(lawLayers?.source ?? lawLayers?.dataSource ?? 'rule', lawLayers?.confidence ?? 'low')))
  } else {
    lines.push('  (데이터 없음 — 용도 선택 후 재조회하세요.)')
  }

  // ── [6] 협력사 법규 (MEP Layer) ──────────────────────────────────────────
  lines.push(...stepHeader(6, '협력사 법규  (MEP Layer)'))
  lines.push('※ 연계 검토 필요 — 자동 판정 불가. 각 담당팀과 별도 협의하세요.')
  const mepLaws = lawLayers?.mepLaws ?? []
  if (mepLaws.length > 0) {
    lines.push('')
    mepLaws.forEach((item, i) => {
      lines.push(`${String(i + 1).padStart(2)}. ${item.title}  [${item.teamTag}]`)
    })
    lines.push(sourceLine(sourceDisplayText(lawLayers?.source ?? lawLayers?.dataSource ?? 'rule', lawLayers?.confidence ?? 'low')))
  } else {
    lines.push('  (데이터 없음 — 용도 선택 후 재조회하세요.)')
  }

  // ── [7] 우선 검토 항목 (ReviewItems) ────────────────────────────────────
  lines.push(...stepHeader(7, '우선 검토 항목  (ReviewItems)'))
  if (!reviewItems || reviewItems.length === 0) {
    lines.push('  (검토 항목 없음)')
  } else {
    CATEGORY_ORDER
      .filter(cat => reviewItems.some(it => it.category === cat))
      .forEach(cat => {
        const catItems = reviewItems.filter(it => it.category === cat)
        lines.push(``)
        lines.push(`▶ ${cat}`)
        catItems.forEach(item => {
          const tags = []
          if (item.priority === 'high')   tags.push('[우선]')
          if (!item.isAutoCheckable)      tags.push('[수동 검토]')
          const tagStr = tags.length > 0 ? tags.join(' ') + '  ' : ''
          lines.push(`  ${tagStr}${item.title}`)
          lines.push(`    → ${item.description}`)
          if (item.requiredInputs?.length > 0) {
            lines.push(`    필요 입력 : ${item.requiredInputs.join(' / ')}`)
          }
          if (item.relatedLaws?.length > 0) {
            lines.push(`    관련 법령 : ${item.relatedLaws.join(' · ')}`)
          }
        })
      })
    lines.push(sourceLine(sourceDisplayText(reviewMeta?.source ?? reviewMeta?.dataSource ?? 'rule', reviewMeta?.confidence ?? 'low')))
  }

  // ── [8] 주의사항 ─────────────────────────────────────────────────────────
  lines.push(...stepHeader(8, '주의사항'))
  lines.push(...cautionLines())

  // ── [9] 간이 계산기 결과 (BCR / FAR) ────────────────────────────────────
  lines.push(...stepHeader(9, '간이 계산기 결과  (BCR / FAR)'))
  if (!calcResult || calcResult.length === 0) {
    lines.push('  (계산기 미사용 — 면적 입력 후 계산하면 결과가 포함됩니다.)')
  } else {
    calcResult.forEach(item => {
      const statusStr = item.isExceeded
        ? '⚠ 법정 상한 초과'
        : item.limit != null ? '적정' : '한도 미확인'
      const limitStr  = item.limit != null ? `  한도: ${item.limit}%` : ''
      lines.push(`  ${item.label} (${item.type}): ${item.value}%${limitStr}  → [${statusStr}]`)
      if (item.note) {
        lines.push(`    참고: ${item.note}`)
      }
    })
    lines.push('')
    lines.push('  ※ 계산 결과는 국토계획법 법정 상한 참고값입니다. 조례 기준과 다를 수 있습니다.')
    lines.push(sourceLine(sourceDisplayText(calcMeta?.source ?? calcMeta?.dataSource ?? 'calculated', calcMeta?.confidence ?? 'low')))
  }

  // ── 문서 끝 ──────────────────────────────────────────────────────────────
  lines.push('')
  lines.push(LINE_THICK)
  lines.push('※ 본 분석은 공공 공간데이터를 기반으로 추출된 1차 참고용 결과입니다.')
  lines.push('※ 실제 인허가 가능 여부는 조례 및 지침에 따르므로 반드시 관할 관청에 확인하십시오.')
  lines.push(LINE_THICK)

  // ── 부록: 판정 데이터 참고 정보 ─────────────────────────────────────────
  lines.push(..._appendix(drDisp, dupDisp))

  return lines.join('\n')
}

// ─────────────────────────────────────────────────────────────────────────────
// 내부: 부록 (note 있을 때만 출력)
// ─────────────────────────────────────────────────────────────────────────────

function _appendix(drDisp, dupDisp) {
  const noteLines = []
  if (drDisp.note)  noteLines.push(`  개발제한구역 판정 참고  : ${drDisp.note}`)
  if (dupDisp.note) noteLines.push(`  지구단위계획 판정 참고  : ${dupDisp.note}`)
  if (noteLines.length === 0) return []
  return [
    '',
    LINE_THIN,
    '[부록] 판정 데이터 참고 정보',
    LINE_THIN,
    ...noteLines,
  ]
}
