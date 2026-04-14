// =============================================================================
// BuildingReviewPanel.jsx
// POST /api/regulation-check/review 전용 UI 컴포넌트
//
// [렌더 구조]
//   상단 요약 바 (reviewLevel + zoning + inputSummary)
//   검토 항목 목록 (priority 정렬 → category 그룹)
//     각 항목: judgeStatus 배지 + judgeNote 하이라이트 + priority 테두리
//   nextLevelHint 패널 (입력 폼 + 다음 단계 활성화 항목 미리보기)
// =============================================================================

import { useState, useEffect, useCallback } from 'react'

// ─────────────────────────────────────────────────────────────────────────────
// 상수
// ─────────────────────────────────────────────────────────────────────────────

const JUDGE_META = {
  active:    { label: '✅ 판정 완료', color: '#0369a1', bg: '#e0f2fe', border: '#bae6fd', dot: '#0ea5e9' },
  reference: { label: '📋 기준값',   color: '#92400e', bg: '#fef3c7', border: '#fde68a', dot: '#f59e0b' },
  pending:   { label: '⚠️ 입력 필요', color: '#64748b', bg: '#f1f5f9', border: '#e2e8f0', dot: '#94a3b8' },
}

const PRIORITY_STYLE = {
  high:   { borderLeft: '3px solid #fca5a5' },
  medium: { borderLeft: '3px solid #fde68a' },
  low:    { borderLeft: '3px solid #e2e8f0' },
}

const CATEGORY_ORDER = [
  '중첩규제', '지구단위계획', '허용용도', '밀도', '도로/건축선',
  '주차', '피난/계단', '승강기', '방화', '기타',
]

const CATEGORY_COLOR = {
  '중첩규제':    '#dc2626',
  '지구단위계획':'#1d4ed8',
  '허용용도':    '#d97706',
  '밀도':        '#d97706',
  '도로/건축선': '#0891b2',
  '주차':        '#475569',
  '피난/계단':   '#475569',
  '승강기':      '#475569',
  '방화':        '#7c3aed',
  '기타':        '#94a3b8',
}

// active 항목 하위 상태 분류 — judgeNote 키워드 기반
function getActiveSubStatus(note) {
  if (!note) return 'ok'
  if (/불가|초과|위반|금지/.test(note)) return 'danger'
  if (/추가 필요|검토 필요|협의 필요|주의/.test(note)) return 'warning'
  return 'ok'
}

const ACTIVE_SUB_META = {
  danger:  { label: '❌ 불가/초과', color: '#991b1b', bg: '#fef2f2', border: '#fecaca', dot: '#dc2626' },
  warning: { label: '⚠️ 주의 필요', color: '#92400e', bg: '#fef3c7', border: '#fde68a', dot: '#f59e0b' },
  ok:      { label: '✅ 적합',      color: '#166534', bg: '#f0fdf4', border: '#bbf7d0', dot: '#16a34a' },
}

// 다음 단계 입력 폼 필드 정의
const HINT_INPUT_META = {
  floorArea:           { label: '계획 연면적', unit: 'm²', type: 'number',
                         hint: '설계 개요 또는 건축계획 초안 기준 (지상+지하 합산)' },
  floorCount:          { label: '계획 층수 (지상)', unit: '층', type: 'number',
                         hint: '지상층 기준 — 지하층 제외하여 입력' },
  siteArea:            { label: '대지면적', unit: 'm²', type: 'number',
                         hint: '토지대장 또는 지적도 기준 (토지이음에서 확인 가능)' },
  roadFrontageWidth:   { label: '도로 접면 폭', unit: 'm', type: 'number',
                         hint: '대지 경계에서 접하는 도로의 폭 (현황 도로 포함)' },
  unitCount:           { label: '세대수', unit: '세대', type: 'number',
                         hint: '전체 계획 세대 합계' },
  unitArea:            { label: '세대별 전용면적', unit: 'm²', type: 'number',
                         hint: '전용면적 기준 (공급면적 아님)' },
  housingSubtype:      { label: '공동주택 유형', unit: '', type: 'select',
                         options: ['아파트', '연립', '다세대'] },
  parkingType:         { label: '주차 방식', unit: '', type: 'select',
                         options: ['underground', 'ground', 'mechanical'],
                         display: ['지하', '지상', '기계식'] },
  detailUseSubtype:    { label: '업종 세부', unit: '', type: 'text', placeholder: '예: 의원, 슈퍼마켓, 고시원',
                         hint: '실제 입점 예정 업종을 구체적으로 입력 (다중이용업 해당 여부 판단에 사용)' },
  detailUseFloorArea:  { label: '업종 바닥면적', unit: 'm²', type: 'number',
                         hint: '해당 업종이 사용하는 전용 바닥면적' },
  isMultipleOccupancy: { label: '다중이용업 해당', unit: '', type: 'select',
                         options: ['true', 'false'], display: ['예', '아니오'] },
  officeSubtype:       { label: '업무시설 유형', unit: '', type: 'select',
                         options: ['오피스텔', '일반업무'] },
  occupantCount:       { label: '예상 상주 인원', unit: '명', type: 'number',
                         hint: '상시 근무·거주 인원 (피난 계단 수 산정 기준)' },
  mixedUseRatio:       { label: '복합 용도 비율', unit: '%', type: 'number', placeholder: '예: 30',
                         hint: '전체 연면적 대비 해당 용도 비율 (예: 근린생활 30%)' },
}

// ─────────────────────────────────────────────────────────────────────────────
// 메인 컴포넌트
// ─────────────────────────────────────────────────────────────────────────────

export default function BuildingReviewPanel({ selectedUse, coordinate, onBuildingInputsChange }) {
  const [formValues, setFormValues] = useState({})
  const [data,       setData]       = useState(null)
  const [loading,    setLoading]    = useState(false)
  const [error,      setError]      = useState(null)
  const [hintOpen,   setHintOpen]   = useState(false)
  const [hintForm,   setHintForm]   = useState({})

  // selectedUse / coordinate 변경 → quick 자동 조회
  useEffect(() => {
    if (!selectedUse || !coordinate) { setData(null); return }
    setFormValues({})
    setHintForm({})
    setHintOpen(false)
    fetchReview({}, null)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedUse, coordinate?.lat, coordinate?.lon])

  const fetchReview = useCallback(async (buildingInputs, reviewLevel) => {
    if (!selectedUse || !coordinate) return
    setLoading(true); setError(null)

    const hasInputs = Object.keys(buildingInputs).length > 0
    const parsedInputs = hasInputs ? parseInputs(buildingInputs) : undefined

    try {
      const body = {
        longitude:     coordinate.lon,
        latitude:      coordinate.lat,
        selectedUse,
        ...(parsedInputs && { buildingInputs: parsedInputs }),
        ...(reviewLevel  && { reviewLevel }),
      }
      const res = await fetch(
        `${import.meta.env.VITE_API_BASE_URL ?? ''}/api/regulation-check/review`,
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'ngrok-skip-browser-warning': '1' },
          body: JSON.stringify(body),
        }
      )
      if (!res.ok) throw new Error('api-error')
      setData(await res.json())
    } catch {
      setError('판정 조회에 실패했습니다.')
    } finally {
      setLoading(false)
    }
  }, [selectedUse, coordinate])

  // 다음 단계 입력 폼 제출
  const handleHintSubmit = (e) => {
    e.preventDefault()
    const merged = { ...formValues, ...hintForm }
    setFormValues(merged)
    setHintOpen(false)
    fetchReview(merged, null)
    onBuildingInputsChange?.(parseInputs(merged))
  }

  // ── 데이터 가공 ──────────────────────────────────────────────────────────
  const items   = data?.reviewItems ?? []
  const hint    = data?.nextLevelHint
  const summary = data?.inputSummary
  const zoning  = data?.zoning
  const level   = data?.reviewLevel ?? 'quick'

  // priority 정렬: high → medium → low, 그 안에서 category 순서 유지
  const priorityOrder = { high: 0, medium: 1, low: 2 }
  const sortedItems = [...items].sort((a, b) => {
    const pa = priorityOrder[a.priority] ?? 2
    const pb = priorityOrder[b.priority] ?? 2
    if (pa !== pb) return pa - pb
    return CATEGORY_ORDER.indexOf(a.category) - CATEGORY_ORDER.indexOf(b.category)
  })

  // active/reference/pending 카운트
  const statusCount = items.reduce((acc, it) => {
    acc[it.judgeStatus] = (acc[it.judgeStatus] ?? 0) + 1; return acc
  }, {})

  // ─────────────────────────────────────────────────────────────────────────
  // 렌더
  // ─────────────────────────────────────────────────────────────────────────
  return (
    <div>

      {/* 로딩 */}
      {loading && (
        <div style={{ padding: '16px 0', textAlign: 'center', color: '#6b7280', fontSize: 12 }}>
          판정 중...
        </div>
      )}

      {/* 오류 */}
      {!loading && error && (
        <div style={{ padding: '12px', fontSize: 12, color: '#b91c1c', background: '#fef2f2', borderRadius: 8, border: '1px solid #fecaca' }}>
          {error}
        </div>
      )}

      {/* 결과 */}
      {!loading && !error && data && (
        <>
          {/* ── 상단 요약 바 ─────────────────────────────────────────────── */}
          <SummaryBar level={level} zoning={zoning} statusCount={statusCount} summary={summary} items={sortedItems} selectedUse={selectedUse} />

          {/* ── 검토 항목 목록 ───────────────────────────────────────────── */}
          {sortedItems.length === 0 ? (
            <div style={{ fontSize: 12, color: '#94a3b8', textAlign: 'center', padding: '12px 0' }}>
              검토 항목이 없습니다.
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginTop: 10 }}>
              {sortedItems.map(item => (
                <ReviewItemCard key={item.ruleId ?? item.title} item={item} />
              ))}
            </div>
          )}

          {/* ── nextLevelHint ────────────────────────────────────────────── */}
          {hint && (hint.additionalInputsNeeded?.length > 0 || hint.note) && (
            <NextLevelHintBlock
              hint={hint}
              hintOpen={hintOpen}
              hintForm={hintForm}
              onToggle={() => setHintOpen(v => !v)}
              onChange={(k, v) => setHintForm(prev => ({ ...prev, [k]: v }))}
              onSubmit={handleHintSubmit}
            />
          )}

          {/* 출처 */}
          <div style={{ marginTop: 8, fontSize: 10, color: '#94a3b8', textAlign: 'right' }}>
            초기 검토 기준 · 공간데이터 참고값 · 인허가 확정은 관청 확인 · {data.elapsedMs}ms
          </div>
        </>
      )}
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// 상단 요약 바
// ─────────────────────────────────────────────────────────────────────────────

function SummaryBar({ level, zoning, statusCount, summary, items = [], selectedUse }) {
  const levelLabel = {
    quick:    { label: '빠른 검토',   desc: '좌표·용도지역 기반 — 입력 없이 즉시 판정',                    color: '#475569', bg: '#f1f5f9' },
    standard: { label: '표준 검토',   desc: '연면적·층수 입력 → 밀도·피난·방화 계산 판정',                  color: '#0369a1', bg: '#e0f2fe' },
    detailed: { label: '상세 검토',   desc: '세부 입력 반영 → 주차·다중이용·업종별 항목 포함',              color: '#6d28d9', bg: '#ede9fe' },
    expert:   { label: '전문가 검토', desc: '모든 입력 완비 → 전 항목 계산 판정',                           color: '#065f46', bg: '#d1fae5' },
  }[level] ?? { label: level, desc: '', color: '#475569', bg: '#f1f5f9' }

  // active 항목 분류
  const dangerItems  = items.filter(i => i.judgeStatus === 'active' && getActiveSubStatus(i.judgeNote) === 'danger')
  const warningItems = items.filter(i => i.judgeStatus === 'active' && getActiveSubStatus(i.judgeNote) === 'warning')
  const hasIssues    = dangerItems.length > 0 || warningItems.length > 0
  const criticalItems = [...dangerItems, ...warningItems].slice(0, 4)

  // 핵심 1줄 요약
  let oneLiner = null
  if (items.length > 0) {
    if (!hasIssues) {
      const okCount = items.filter(i => i.judgeStatus === 'active').length
      if (okCount > 0) oneLiner = `${selectedUse ?? '선택 용도'} — 검토 항목 ${okCount}건 적합`
    } else {
      const parts = [
        ...dangerItems.slice(0, 2).map(i => i.title),
        ...warningItems.slice(0, 1).map(i => i.title),
      ]
      oneLiner = parts.join(' / ')
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginBottom: 4 }}>
      {/* 단계 + 용도지역 + 상태 카운트 */}
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 5, alignItems: 'center' }}>
        <span style={{ fontSize: 11, fontWeight: 700, padding: '3px 9px', borderRadius: 5,
          background: levelLabel.bg, color: levelLabel.color }}>
          {levelLabel.label}
        </span>
        {levelLabel.desc && (
          <span style={{ fontSize: 10, color: '#94a3b8', fontWeight: 400 }}>
            {levelLabel.desc}
          </span>
        )}
        {zoning?.zoneName && (
          <span style={{ fontSize: 11, fontWeight: 600, padding: '3px 9px', borderRadius: 5,
            background: '#f0fdf4', color: '#15803d' }}>
            {zoning.zoneName}
            {zoning.farLimitPct != null && ` · 용적률 ${zoning.farLimitPct}%`}
            {zoning.bcRatioLimitPct != null && ` · 건폐율 ${zoning.bcRatioLimitPct}%`}
          </span>
        )}
        {Object.entries(statusCount).map(([s, n]) => {
          const m = JUDGE_META[s]
          if (!m) return null
          return (
            <span key={s} style={{ fontSize: 10, fontWeight: 600, padding: '2px 7px', borderRadius: 4,
              background: m.bg, color: m.color }}>
              {m.label} {n}
            </span>
          )
        })}
      </div>

      {/* 핵심 이슈 1줄 요약 배너 */}
      {oneLiner && (
        <div style={{
          fontSize: 12, fontWeight: 700, lineHeight: 1.4,
          padding: '7px 12px', borderRadius: 7,
          color:      hasIssues ? '#7c2d12' : '#14532d',
          background: hasIssues ? '#fff7ed' : '#f0fdf4',
          border:     `1px solid ${hasIssues ? '#fed7aa' : '#bbf7d0'}`,
        }}>
          {hasIssues ? '⚠️ ' : '✅ '}{oneLiner}
        </div>
      )}

      {/* 위험/주의 항목 빠른 태그 */}
      {criticalItems.length > 0 && (
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
          {criticalItems.map((item, i) => {
            const sm = ACTIVE_SUB_META[getActiveSubStatus(item.judgeNote)]
            return (
              <span key={i} style={{
                fontSize: 10, padding: '2px 8px', borderRadius: 4, fontWeight: 700,
                background: sm.bg, color: sm.color, border: `1px solid ${sm.border}`,
              }}>
                {sm.label.split(' ')[0]} {item.title}
              </span>
            )
          })}
        </div>
      )}

      {/* 누락 입력 안내 */}
      {summary?.missingNote && (
        <div style={{ fontSize: 11, color: '#92400e', background: '#fef3c7',
          padding: '6px 10px', borderRadius: 6, border: '1px solid #fde68a', lineHeight: 1.5 }}>
          💡 {summary.missingNote}
        </div>
      )}
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// 검토 항목 카드
// ─────────────────────────────────────────────────────────────────────────────

function ReviewItemCard({ item }) {
  const [open, setOpen] = useState(false)
  // active 항목은 sub-status 색상, 나머지는 기존 JUDGE_META
  const jm = item.judgeStatus === 'active'
    ? ACTIVE_SUB_META[getActiveSubStatus(item.judgeNote)]
    : (JUDGE_META[item.judgeStatus] ?? JUDGE_META.pending)
  const ps = PRIORITY_STYLE[item.priority] ?? PRIORITY_STYLE.low
  const catColor = CATEGORY_COLOR[item.category] ?? '#94a3b8'
  const sub = item.judgeStatus === 'active' ? getActiveSubStatus(item.judgeNote) : null

  return (
    <div
      onClick={() => setOpen(v => !v)}
      style={{
        padding: '9px 10px', borderRadius: 8,
        border: `1px solid ${sub === 'danger' ? '#fca5a5' : item.priority === 'high' ? '#fca5a5' : '#f1f5f9'}`,
        background: sub === 'danger' ? '#fff7f7' : sub === 'warning' ? '#fffbeb' : '#fff',
        cursor: 'pointer',
        ...ps,
      }}
    >
      {/* 제목 행 */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
        <div style={{ display: 'flex', gap: 5, alignItems: 'center', flexWrap: 'wrap', flex: 1 }}>
          {/* 카테고리 도트 */}
          <span style={{ width: 6, height: 6, borderRadius: '50%',
            background: catColor, display: 'inline-block', flexShrink: 0, marginTop: 1 }} />
          <span style={{ fontSize: 12, fontWeight: 700, color: '#1e293b', lineHeight: 1.4 }}>
            {item.title}
          </span>
        </div>
        <div style={{ display: 'flex', gap: 4, alignItems: 'center', flexShrink: 0 }}>
          {/* priority=high 배지 */}
          {item.priority === 'high' && (
            <span style={{ fontSize: 9, padding: '2px 5px', borderRadius: 3,
              background: '#fef2f2', color: '#b91c1c', fontWeight: 800, whiteSpace: 'nowrap' }}>
              우선
            </span>
          )}
          {/* judgeStatus 배지 */}
          <span style={{ fontSize: 9, padding: '2px 6px', borderRadius: 4, whiteSpace: 'nowrap',
            background: jm.bg, color: jm.color, fontWeight: 700, border: `1px solid ${jm.border}` }}>
            {jm.label}
          </span>
          <span style={{ fontSize: 10, color: '#94a3b8', whiteSpace: 'nowrap' }}>
            {open ? '접기 ▲' : '상세 ▼'}
          </span>
        </div>
      </div>

      {/* judgeNote — 항상 표시 */}
      {item.judgeNote && (
        <div style={{
          marginTop: 6, padding: '6px 8px', borderRadius: 5,
          background: jm.bg, border: `1px solid ${jm.border}`,
          fontSize: 11, color: jm.color, lineHeight: 1.6, fontWeight: 500,
        }}>
          {item.judgeNote}
        </div>
      )}

      {/* 펼치기: description + relatedLaws */}
      {open && (
        <div style={{ marginTop: 8, paddingTop: 8, borderTop: '1px solid #f1f5f9' }}>
          <div style={{ fontSize: 12, color: '#64748b', lineHeight: 1.6, marginBottom: 5 }}>
            {item.description}
          </div>
          {item.requiredInputs?.length > 0 && (
            <div style={{ fontSize: 10, color: '#94a3b8', marginBottom: 3 }}>
              필요 입력: {item.requiredInputs.join(' / ')}
            </div>
          )}
          {item.relatedLaws?.length > 0 && (
            <div style={{ fontSize: 10, color: '#94a3b8' }}>
              {item.relatedLaws.join(' · ')}
            </div>
          )}
        </div>
      )}
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// nextLevelHint 패널
// ─────────────────────────────────────────────────────────────────────────────

function NextLevelHintBlock({ hint, hintOpen, hintForm, onToggle, onChange, onSubmit }) {
  // 단계 완료 안내 (additionalInputsNeeded 없음 + note 있음)
  if (!hint.additionalInputsNeeded?.length) {
    return (
      <div style={{ marginTop: 10, padding: '10px 12px', borderRadius: 8,
        background: '#f0fdf4', border: '1px solid #bbf7d0', fontSize: 11, color: '#15803d' }}>
        ✅ {hint.note ?? '이 단계의 판정이 완료되었습니다.'}
      </div>
    )
  }

  const nextLevelLabel = {
    standard: '표준',
    detailed: '상세',
    expert:   '전문가',
  }[hint.nextLevel] ?? hint.nextLevel

  return (
    <div style={{ marginTop: 10, border: '1px solid #e0e7ff', borderRadius: 10,
      background: '#f5f3ff', overflow: 'hidden' }}>

      {/* 헤더 */}
      <button
        onClick={onToggle}
        style={{ width: '100%', display: 'flex', justifyContent: 'space-between',
          alignItems: 'center', padding: '10px 12px', background: 'none',
          border: 'none', cursor: 'pointer', gap: 8 }}
      >
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: 2 }}>
          <span style={{ fontSize: 12, fontWeight: 700, color: '#4c1d95' }}>
            {nextLevelLabel} 단계로 이동 — 입력 추가 시 {hint.willUnlock?.length ?? 0}개 항목 계산 가능
          </span>
          {hint.note && (
            <span style={{ fontSize: 10, color: '#6d28d9', textAlign: 'left' }}>{hint.note}</span>
          )}
        </div>
        <span style={{ fontSize: 11, color: '#6d28d9', flexShrink: 0 }}>
          {hintOpen ? '▲ 접기' : '▼ 입력하기'}
        </span>
      </button>

      {/* 활성화될 항목 미리보기 (항상 표시) */}
      {hint.willUnlock?.length > 0 && (
        <div style={{ padding: '0 12px 8px', display: 'flex', flexWrap: 'wrap', gap: 4 }}>
          {hint.willUnlock.map((label, i) => (
            <span key={i} style={{ fontSize: 10, padding: '2px 7px', borderRadius: 4,
              background: '#ede9fe', color: '#6d28d9', fontWeight: 500 }}>
              {label.replace(/\s*\(RI-[^)]+\)/, '')}
            </span>
          ))}
        </div>
      )}

      {/* 입력 폼 */}
      {hintOpen && (
        <form onSubmit={onSubmit}
          style={{ padding: '0 12px 12px', borderTop: '1px solid #ddd6fe' }}>
          <div style={{ marginTop: 10, display: 'grid', gap: 8 }}>
            {hint.additionalInputsNeeded.map(({ field, label, reason }) => {
              const meta = HINT_INPUT_META[field]
              if (!meta) return null
              return (
                <div key={field}>
                  <label style={{ fontSize: 10, fontWeight: 700, color: '#6d28d9', display: 'block', marginBottom: 2 }}>
                    {meta.label ?? label}
                    {meta.unit && <span style={{ fontWeight: 400, color: '#94a3b8', marginLeft: 3 }}>({meta.unit})</span>}
                    <span style={{ fontWeight: 400, color: '#a78bfa', marginLeft: 5 }}>{reason}</span>
                  </label>
                  {meta.hint && (
                    <div style={{ fontSize: 10, color: '#94a3b8', marginBottom: 4, lineHeight: 1.4 }}>
                      ℹ {meta.hint}
                    </div>
                  )}
                  {meta.type === 'select' ? (
                    <select
                      value={hintForm[field] ?? ''}
                      onChange={e => onChange(field, e.target.value)}
                      style={inputStyle}
                    >
                      <option value="">선택</option>
                      {meta.options.map((opt, i) => (
                        <option key={opt} value={opt}>
                          {meta.display?.[i] ?? opt}
                        </option>
                      ))}
                    </select>
                  ) : (
                    <input
                      type={meta.type}
                      min="0" step="any"
                      placeholder={meta.placeholder ?? (meta.unit ? `단위: ${meta.unit}` : '')}
                      value={hintForm[field] ?? ''}
                      onChange={e => onChange(field, e.target.value)}
                      style={inputStyle}
                    />
                  )}
                </div>
              )
            })}
          </div>
          <button type="submit"
            style={{ marginTop: 10, width: '100%', padding: '8px', borderRadius: 6,
              border: 'none', background: '#7c3aed', color: '#fff',
              fontSize: 12, fontWeight: 700, cursor: 'pointer' }}>
            {nextLevelLabel} 단계로 판정
          </button>
        </form>
      )}
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// 헬퍼
// ─────────────────────────────────────────────────────────────────────────────

const inputStyle = {
  width: '100%', boxSizing: 'border-box',
  padding: '5px 8px', borderRadius: 6,
  border: '1px solid #c4b5fd', fontSize: 12,
  background: '#fff', outline: 'none',
}

/** form 값(모두 string)을 buildingInputs 타입에 맞게 변환 */
function parseInputs(raw) {
  const out = {}
  const numFields = [
    'floorArea', 'floorCount', 'siteArea', 'roadFrontageWidth',
    'unitCount', 'unitArea', 'buildingHeight', 'detailUseFloorArea',
    'occupantCount', 'mixedUseRatio',
  ]
  const boolFields  = ['isMultipleOccupancy', 'isHighRiskOccupancy', 'hasDisabilityUsers',
                        'hasPublicSpace', 'hasLoadingBay',
                        'hasDistrictUnitPlanDocument', 'hasDevActRestrictionConsult']
  const intFields   = ['floorCount', 'unitCount', 'occupantCount']

  for (const [k, v] of Object.entries(raw)) {
    if (v === '' || v == null) continue
    if (boolFields.includes(k))      { out[k] = v === 'true'; continue }
    if (numFields.includes(k)) {
      const n = Number(v)
      if (!Number.isFinite(n)) continue
      // mixedUseRatio는 UI에서 %로 입력받아 0~1로 변환
      if (k === 'mixedUseRatio') { out[k] = n / 100; continue }
      out[k] = intFields.includes(k) ? Math.round(n) : n
      continue
    }
    out[k] = v   // string (housingSubtype, parkingType, detailUseSubtype, officeSubtype)
  }
  return out
}
