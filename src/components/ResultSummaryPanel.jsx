import { useState, useEffect } from 'react'
import { buildReportText }   from '../utils/reportFormatter.js'
import BuildingReviewPanel   from './BuildingReviewPanel.jsx'
import { fetchApi } from '../utils/apiClient'

// ─────────────────────────────────────────────────────────────────────────────
// 상수
// ─────────────────────────────────────────────────────────────────────────────

const CATEGORY_ORDER = [
  '중첩규제', '지구단위계획', '허용용도', '밀도', '도로/건축선',
  '주차', '피난/계단', '승강기', '방화', '기타',
]
const CATEGORY_COLORS = {
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
const DEFAULT_USE_OPTIONS = ['공동주택', '제1종근린생활시설', '제2종근린생활시설', '업무시설', '교육시설', '의료시설', '숙박시설', '공장', '창고시설', '물류시설']

// [3][4][5] 법규 레이어는 백엔드 POST /api/regulation-check/law-layers 에서 조회

// ─────────────────────────────────────────────────────────────────────────────
// 컴포넌트
// ─────────────────────────────────────────────────────────────────────────────
export default function ResultSummaryPanel({
  selectedCoordinate, lookupInfo, result, loading, error,
  selectedUse,   // 페이지에서 관리 (이력 복원 지원)
  onUseSelect,   // (use: string|null) => void
}) {
  // reviewItems / reviewLoading / reviewMeta は BuildingReviewPanel 내부로 이전

  const [lawLayers,        setLawLayers]        = useState(null)  // { coreLaws, extendedCoreLaws, mepLaws, dataSource, confidence }
  const [lawLayersLoading, setLawLayersLoading] = useState(false)
  const [useOptions,       setUseOptions]       = useState(DEFAULT_USE_OPTIONS)

  const [calcInputs,    setCalcInputs]    = useState({ siteArea: '', buildingArea: '', totalFloorArea: '' })
  const [calcResult,    setCalcResult]    = useState(null)
  const [calcMeta,      setCalcMeta]      = useState(null) // { dataSource, confidence }
  const [calcLoading,   setCalcLoading]   = useState(false)
  const [calcError,     setCalcError]     = useState(null)
  const [lawLayersOpen, setLawLayersOpen] = useState(false)

  // 빈 상태에서 미리 선택한 용도 — result 도착 시 자동 적용
  const [pendingUse, setPendingUse] = useState(null)

  useEffect(() => {
    let cancelled = false

    async function loadUseProfiles() {
      try {
        const res = await fetchApi('/api/regulation-check/use-profiles')
        if (!res.ok) throw new Error('use-profiles')
        const payload = await res.json()
        if (!cancelled && Array.isArray(payload) && payload.length > 0) {
          setUseOptions(payload.map(item => item.displayName).filter(Boolean))
        }
      } catch {
        if (!cancelled) setUseOptions(DEFAULT_USE_OPTIONS)
      }
    }

    loadUseProfiles()
    return () => { cancelled = true }
  }, [])

  useEffect(() => {
    if (result && pendingUse && !selectedUse) {
      handleUseSelectInternal(pendingUse)
      setPendingUse(null)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [result])

  // result 변경 시 로컬 하위 상태 초기화
  // (selectedUse 초기화는 부모 RegulationMapPage에서 처리)
  useEffect(() => {
    setLawLayers(null)
    setCalcResult(null)
    setCalcMeta(null)
    setCalcInputs({ siteArea: '', buildingArea: '', totalFloorArea: '' })
    setCalcError(null)
  }, [result])

  // ── BuildingReviewPanel 입력값 → 간이계산기 동기화 ──────────────────────
  function handleBuildingInputsChange(inputs) {
    if (!inputs) return
    setCalcInputs(prev => ({
      ...prev,
      ...(inputs.siteArea  != null ? { siteArea:       String(inputs.siteArea)  } : {}),
      ...(inputs.floorArea != null ? { totalFloorArea: String(inputs.floorArea) } : {}),
    }))
  }

  // ── [6] 계산기 실행 ──────────────────────────────────────────────────────
  async function handleCalcSubmit(e) {
    e.preventDefault()
    const { siteArea, buildingArea, totalFloorArea } = calcInputs
    if (!siteArea || !buildingArea || !totalFloorArea) return
    setCalcLoading(true)
    setCalcError(null)
    try {
      const res = await fetchApi('/api/calculator/basic', {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          siteArea:       parseFloat(siteArea),
          buildingArea:   parseFloat(buildingArea),
          totalFloorArea: parseFloat(totalFloorArea),
          zoneName:       result?.zoning?.zoneName ?? null,
        }),
      })
      if (res.ok) {
        const data = await res.json()
        setCalcResult(data.results ?? [])
        setCalcMeta({
          source:     data.source ?? data.dataSource ?? 'calculated',  // 표준 → deprecated fallback
          confidence: data.confidence ?? 'low',
          dataSource: data.dataSource ?? 'calculated',  // deprecated
        })
      } else {
        setCalcError('계산 요청 실패')
      }
    } catch {
      setCalcError('서버 연결 오류')
    } finally {
      setCalcLoading(false)
    }
  }

  // ── [2] 용도 선택 + [3][4][5] 법규 레이어 + [6] ReviewItems 조회 ─────────
  async function handleUseSelectInternal(use) {
    if (!result) return
    if (use === selectedUse) {
      // 같은 용도 재클릭 → 해제
      onUseSelect(null)
      setLawLayers(null)
      setCalcResult(null)
      setCalcInputs({ siteArea: '', buildingArea: '', totalFloorArea: '' })
      return
    }
    onUseSelect(use)
    setLawLayers(null)
    setCalcResult(null)
    setCalcInputs({ siteArea: '', buildingArea: '', totalFloorArea: '' })

    const headers = { 'Content-Type': 'application/json' }

    setLawLayersLoading(true)

    // 법규 레이어 조회 (ReviewItems는 BuildingReviewPanel이 /review 로 직접 조회)
    const layersRes = await fetchApi('/api/regulation-check/law-layers', {
      method: 'POST', headers,
      body: JSON.stringify({
        selectedUse:                    use,
        districtUnitPlanIsInside:       result?.extraLayers?.districtUnitPlan?.isInside ?? null,
        developmentRestrictionIsInside: result?.extraLayers?.developmentRestriction?.isInside ?? null,
      }),
    }).catch(() => null)

    if (layersRes?.ok) {
      const data = await layersRes.json()
      setLawLayers({
        coreLaws:         data.coreLaws         ?? [],
        extendedCoreLaws: data.extendedCoreLaws ?? [],
        mepLaws:          data.mepLaws          ?? [],
        source:           data.source           ?? data.dataSource ?? 'rule',
        confidence:       data.confidence       ?? 'low',
        dataSource:       data.dataSource       ?? 'rule',
      })
    }
    setLawLayersLoading(false)
  }

  // ── 헬퍼 함수 ────────────────────────────────────────────────────────────
  function formatCoord(v) { return typeof v === 'number' ? v.toFixed(6) : '-' }
  function toYesNo(v)     { return v === true ? '예' : v === false ? '아니오' : '미확인' }

  function getConfidence(res) {
    if (!res?.zoning) return { label: '신뢰도 낮음', explanation: '데이터 미매칭 (참고용)', color: '#991b1b', bg: '#fee2e2' }
    const dist = res.debug?.nearestDistanceMeters || 0
    if (dist > 0.1) return { label: '신뢰도 중간', explanation: '경계 인접 또는 일부 불확실 요소', color: '#92400e', bg: '#fef3c7' }
    return { label: '신뢰도 높음', explanation: '데이터 직접 매칭 (법적 확정 아님)', color: '#166534', bg: '#f0fdf4' }
  }

  function getDiagnosticMessage(reason, dist) {
    if (!reason) return '분석 데이터 매칭됨'
    if (dist > 0 && dist < 1.0) return '필지 경계선 인접 (정밀 확인)'
    if (reason.includes('Successful zoning hit')) return '공간 데이터와 직접 매칭됨'
    if (reason.includes('No matching feature'))   return '범위 내 매칭 정보 없음'
    return '자동 분석 결과 (참고)'
  }

  function getDevRestrictionDisplay(res) {
    const dr = res?.extraLayers?.developmentRestriction
    if (!dr) {
      const v = res?.overlayFlags?.isDevelopmentRestricted
      return { text: toYesNo(v), isYes: v === true, badge: null, gray: false, note: null, confidence: null }
    }
    if (dr.status === 'unavailable') return { text: '판정 불가', isYes: false, badge: null, gray: true, note: dr.note, confidence: 'low' }
    return {
      text:       dr.isInside ? '개발제한구역' : '해당 없음',
      isYes:      dr.isInside === true,
      badge:      dr.status === 'fallback' ? '(참고값)' : null,
      gray:       false,
      note:       dr.note,
      source:     dr.source,
      confidence: dr.confidence ?? dr.confidenceLevel ?? null,  // 표준 → deprecated fallback
    }
  }

  function getDistrictUnitPlanDisplay(res) {
    const dup = res?.extraLayers?.districtUnitPlan
    if (!dup) {
      const v = res?.overlayFlags?.isInDistrictUnitPlan
      return { text: toYesNo(v), isYes: v === true, badge: null, gray: v == null, note: null, confidence: null }
    }
    if (dup.status === 'unavailable') return { text: '판정 불가', isYes: false, badge: null, gray: true, note: dup.note, confidence: 'low' }
    return {
      text:       dup.isInside ? '지구단위계획구역' : '해당 없음',
      isYes:      dup.isInside === true,
      badge:      dup.status === 'fallback' ? '(참고값)' : null,
      gray:       false,
      note:       dup.note,
      source:     dup.source,
      confidence: dup.confidence ?? dup.confidenceLevel ?? null,  // 표준 → deprecated fallback
    }
  }

  // ── Export ───────────────────────────────────────────────────────────────
  const handleExport = () => {
    if (!result) return
    const text = buildReportText({
      lookupInfo,
      selectedCoordinate,
      result,
      selectedUse,
      lawLayers,
      reviewItems: null,   // BuildingReviewPanel(/review)이 관리
      reviewMeta:  null,
      calcResult,
      calcMeta,
    })
    const blob = new Blob([text], { type: 'text/plain;charset=utf-8' })
    const url  = URL.createObjectURL(blob)
    const a    = document.createElement('a')
    a.href     = url
    a.download = `건축법규검토_${lookupInfo?.label?.replace(/\s+/g, '_') || '결과'}.txt`
    a.click()
    URL.revokeObjectURL(url)
  }

  // ── 데이터 출처 표시 텍스트 ──────────────────────────────────────────────
  function sourceDisplayText(source, confidence) {
    const srcLabel = {
      rule:       '내부 규칙 테이블',
      calculated: '입력값 산술 계산',
      api:        'API 직접 확인',
      shp:        'SHP 공간 데이터',
      none:       '데이터 없음',
    }[source] ?? source ?? '미상'
    const confLabel = confidence === 'high' ? '높음' : confidence === 'medium' ? '중간' : '낮음'
    return `출처: ${srcLabel}  신뢰도: ${confLabel}`
  }

  // ── 신뢰도 배지 색상 ─────────────────────────────────────────────────────
  function confBadgeProps(level) {
    return level === 'high'   ? { bg: '#f0fdf4', color: '#15803d', label: '높음' }
         : level === 'medium' ? { bg: '#fef3c7', color: '#92400e', label: '중간' }
         :                      { bg: '#fef2f2', color: '#991b1b', label: '낮음' }
  }

  // ── 스타일 헬퍼 ──────────────────────────────────────────────────────────
  const card   = (extra = {}) => ({
    padding: '14px 12px', border: '1px solid #e5e7eb', borderRadius: 10,
    marginBottom: 12, backgroundColor: '#fff', ...extra,
  })
  const accent = (color = '#3b82f6') => ({
    width: 3, height: 13, background: color, borderRadius: 2, flexShrink: 0,
  })
  const sh = (color = '#475569') => ({
    margin: '0 0 10px', fontSize: 13, fontWeight: 700, color,
    display: 'flex', alignItems: 'center', gap: 6,
  })

  // ── sortedRegulations ─────────────────────────────────────────────────────
  const sortedRegulations = [...(result?.relatedRegulations || [])].sort((a, b) => {
    const ord = { '[제한]': 1, '[계획]': 2, '[용도]': 3, '[밀도]': 4, '[공통]': 5 }
    const get = s => { for (const k in ord) if (s.startsWith(k)) return ord[k]; return 99 }
    return get(a) - get(b)
  })
  const overlays = [{ label: '개발행위허가제한지역', key: 'isDevelopmentActionRestricted' }]

  // ─────────────────────────────────────────────────────────────────────────
  // 렌더
  // ─────────────────────────────────────────────────────────────────────────
  return (
    <aside style={{ width: '100%', boxSizing: 'border-box', color: '#1f2937' }}>

      {/* 패널 헤더 */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 16 }}>
        <h2 style={{ margin: 0, fontSize: 22, fontWeight: 800, letterSpacing: '-0.025em' }}>건축 법규 사전 검토</h2>
        {!loading && !error && result && (
          <button
            onClick={handleExport}
            style={{ padding: '8px 16px', borderRadius: 8, border: '1px solid #0284c7', background: '#0284c7', fontSize: 13, fontWeight: 700, cursor: 'pointer', color: '#fff' }}
          >
            검토 리포트 (.txt) 저장
          </button>
        )}
      </div>

      {loading && <div style={{ padding: 20, textAlign: 'center', color: '#6b7280' }}>데이터 로드 중...</div>}
      {error   && <div style={{ padding: 20, textAlign: 'center', color: '#b91c1c', background: '#fef2f2', borderRadius: 10 }}>조회 실패: 서버 연결을 확인하세요.</div>}
      {!loading && !error && !result && (
        <EntryGuide pendingUse={pendingUse} onPendingUseSelect={setPendingUse} useOptions={useOptions} />
      )}

      {!loading && !error && result && (() => {
        const conf = getConfidence(result)
        return (
          <>
            {/* ══════════════════════════════════════════════════════ */}
            {/* [1] 토지 법규 조회 결과                               */}
            {/* ══════════════════════════════════════════════════════ */}
            <StepLabel step={1} title="토지 법규 조회 결과" />

            {/* 신뢰도 배지 */}
            <div style={{ marginBottom: 12 }}>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginBottom: 6 }}>
                <StatusBadge label={conf.label} bg={conf.bg} color={conf.color} />
                <StatusBadge label={lookupInfo?.mode === 'address' ? '주소 기준' : '좌표 기준'} bg="#f1f5f9" color="#475569" />
                <StatusBadge
                  label={result.overlayFlags?.hasAnyRestriction ? '중첩 규제 있음' : '주요 규제 없음*'}
                  bg={result.overlayFlags?.hasAnyRestriction ? '#fff1f2' : '#f0fdf4'}
                  color={result.overlayFlags?.hasAnyRestriction ? '#be123c' : '#15803d'}
                />
              </div>
              <div style={{ fontSize: 11, color: conf.color, fontWeight: 500, marginBottom: 2 }}>• {conf.explanation}</div>
              {result.dataTrust && (
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4, marginBottom: 4 }}>
                  <SourceBadge label={`용도지역: ${result.dataTrust.zoningSource === 'api' ? 'API 확인' : 'SHP 데이터'}`} />
                  {(() => { const c = confBadgeProps(result.dataTrust.overallConfidence); return <span style={{ fontSize: 10, padding: '2px 7px', borderRadius: 4, background: c.bg, color: c.color, fontWeight: 600 }}>종합 신뢰도 {c.label}</span> })()}
                  {result.debug?.responseTimeMs != null && (
                    <SourceBadge label={`응답 ${result.debug.responseTimeMs}ms`} />
                  )}
                </div>
              )}
              {result.dataTrust?.overallNote && (
                <div style={{ fontSize: 10, color: '#64748b', marginBottom: 4 }}>ℹ {result.dataTrust.overallNote}</div>
              )}
              <div style={{ fontSize: 10, color: '#94a3b8', fontStyle: 'italic' }}>
                * 신뢰도는 공간 데이터 매칭 정확도이며 인허가 확정 판단이 아닙니다.
              </div>
            </div>

            {/* 조회 기준 */}
            <div style={card({ backgroundColor: '#f8fafc' })}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <div>
                  <div style={{ fontSize: 10, color: '#94a3b8', fontWeight: 600, marginBottom: 2 }}>조회 위치 및 근거</div>
                  <div style={{ fontSize: 16, fontWeight: 700, color: '#0f172a' }}>
                    {lookupInfo?.label || (selectedCoordinate ? '선택된 좌표 지점' : '위치 미지정')}
                  </div>
                </div>
                <div style={{ textAlign: 'right' }}>
                  <div style={{ fontSize: 10, color: '#94a3b8', fontWeight: 600, marginBottom: 2 }}>매칭 상태</div>
                  <div style={{ fontSize: 11, fontWeight: 600, color: '#3b82f6' }}>
                    {getDiagnosticMessage(result.debug?.reason, result.debug?.nearestDistanceMeters)}
                  </div>
                </div>
              </div>
              <div style={{ marginTop: 8, paddingTop: 8, borderTop: '1px solid #f1f5f9', fontSize: 12, color: '#64748b' }}>
                <div style={{ display: 'flex', gap: 4, marginBottom: 2 }}>
                  <span style={{ fontWeight: 600, color: '#94a3b8', minWidth: 50 }}>입력 방식:</span>
                  <span>{lookupInfo?.mode === 'address' ? '정규화 주소 매칭' : '좌표(WGS84) 직접 지정'}</span>
                </div>
                <div style={{ display: 'flex', gap: 4 }}>
                  <span style={{ fontWeight: 600, color: '#94a3b8', minWidth: 50 }}>데이터원:</span>
                  <span style={{ fontSize: 11 }}>{result.meta?.layerName?.split('(')[0] || '공공 공간 데이터'}</span>
                </div>
              </div>
            </div>

            {/* 1차 판단 요약 */}
            <div style={card()}>
              <h3 style={sh()}>
                <span style={accent('#3b82f6')} />1차 판단 요약
              </h3>
              <p style={{ margin: 0, fontSize: 14, whiteSpace: 'pre-line', lineHeight: 1.6, color: '#334155' }}>
                {result.summaryText}
              </p>
            </div>

            {/* 공간 규제 레이어 상태 */}
            <div style={card()}>
              <h3 style={sh()}>
                <span style={accent('#10b981')} />공간 규제 레이어 상태
              </h3>
              <div style={{ display: 'grid', gap: 6 }}>
                {[
                  { label: '개발제한구역',  display: getDevRestrictionDisplay(result) },
                  { label: '지구단위계획',  display: getDistrictUnitPlanDisplay(result) },
                ].map(({ label, display: d }) => {
                  const bg = d.gray ? '#f8fafc' : d.isYes ? '#fff1f2' : '#f8fafc'
                  const tc = d.gray ? '#94a3b8' : d.isYes ? '#991b1b' : '#475569'
                  const vc = d.gray ? '#94a3b8' : d.isYes ? '#e11d48' : '#64748b'
                  return (
                    <div key={label} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '8px 10px', borderRadius: 8, border: '1px solid #f1f5f9', backgroundColor: bg }}>
                      <span style={{ fontSize: 13, color: tc, fontWeight: d.isYes ? 600 : 400 }}>{label}</span>
                      <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
                        {d.badge && <span style={{ fontSize: 10, padding: '2px 5px', borderRadius: 4, background: '#fef3c7', color: '#92400e', fontWeight: 600 }}>{d.badge}</span>}
                        {d.confidence && (() => { const c = confBadgeProps(d.confidence); return <span style={{ fontSize: 9, padding: '1px 5px', borderRadius: 4, background: c.bg, color: c.color, fontWeight: 600 }}>{c.label}</span> })()}
                        <span title={d.note ?? undefined} style={{ fontSize: 12, fontWeight: 700, color: vc }}>{d.text}</span>
                      </div>
                    </div>
                  )
                })}
                {overlays.map(({ label, key }) => {
                  const yn    = toYesNo(result?.overlayFlags?.[key])
                  const isYes = yn === '예'
                  return (
                    <div key={key} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '8px 10px', borderRadius: 8, border: '1px solid #f1f5f9', backgroundColor: isYes ? '#fff1f2' : '#f8fafc' }}>
                      <span style={{ fontSize: 13, color: isYes ? '#991b1b' : '#475569', fontWeight: isYes ? 600 : 400 }}>{label}</span>
                      <span style={{ fontSize: 12, fontWeight: 700, color: isYes ? '#e11d48' : '#64748b' }}>{yn}</span>
                    </div>
                  )
                })}
              </div>
            </div>

            {/* 1차 판정 법규 목록 */}
            {sortedRegulations.length > 0 && (
              <div style={card()}>
                <h3 style={sh('#f59e0b')}>
                  <span style={accent('#f59e0b')} />1차 판정 법규 목록
                </h3>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                  {sortedRegulations.map((reg, i) => {
                    const match  = reg.match(/^(\[[^\]]+\])\s*(.*)$/)
                    const prefix = match ? match[1] : ''
                    const body   = match ? match[2] : reg
                    return (
                      <div key={i} style={{ fontSize: 13, lineHeight: 1.5, display: 'flex', gap: 8 }}>
                        {prefix && <span style={{ fontWeight: 700, color: '#d97706', whiteSpace: 'nowrap' }}>{prefix}</span>}
                        <span style={{ color: '#334155' }}>{body}</span>
                      </div>
                    )
                  })}
                </div>
              </div>
            )}

            {/* ══════════════════════════════════════════════════════ */}
            {/* [2] 계획 용도 선택                                    */}
            {/* ══════════════════════════════════════════════════════ */}
            <StepLabel step={2} title="계획 용도 선택" />
            <div style={card()}>
              <p style={{ margin: '0 0 10px', fontSize: 12, color: '#94a3b8' }}>
                계획 용도를 선택하면 관련 법규와 검토 항목을 확인할 수 있습니다.
              </p>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                {useOptions.map(use => (
                  <button
                    key={use}
                    onClick={() => handleUseSelectInternal(use)}
                    style={{
                      padding: '7px 16px', borderRadius: 20, fontSize: 12, fontWeight: 600,
                      cursor: 'pointer', border: '1px solid',
                      borderColor: selectedUse === use ? '#7c3aed' : '#e2e8f0',
                      background:  selectedUse === use ? '#7c3aed' : '#f8fafc',
                      color:       selectedUse === use ? '#fff'    : '#475569',
                    }}
                  >
                    {use}
                  </button>
                ))}
              </div>
            </div>

            {/* ══ 용도 선택 후 섹션 ════════════════════════════════ */}
            {selectedUse && (
              <>
                {/* ══════════════════════════════════════════════════ */}
                {/* [3] 단계별 판정 (BuildingReviewPanel) — 상단 이동  */}
                {/* ══════════════════════════════════════════════════ */}
                <StepLabel step={3} title="단계별 판정" sub="항목별 결과 · 상세 입력" />
                <div style={card()}>
                  <BuildingReviewPanel
                    selectedUse={selectedUse}
                    coordinate={selectedCoordinate}
                    onBuildingInputsChange={handleBuildingInputsChange}
                  />
                </div>

                {/* ══════════════════════════════════════════════════ */}
                {/* [4] 법규 레이어 — 기본 접힘, 클릭 시 펼치기        */}
                {/* ══════════════════════════════════════════════════ */}
                {(() => {
                  const totalLawCount = (lawLayers?.coreLaws?.length ?? 0)
                    + (lawLayers?.extendedCoreLaws?.length ?? 0)
                    + (lawLayers?.mepLaws?.length ?? 0)
                  return (
                <div style={{ marginBottom: 12 }}>
                  <button
                    onClick={() => setLawLayersOpen(v => !v)}
                    style={{
                      width: '100%', display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                      padding: '10px 14px',
                      background: lawLayersOpen ? '#eff6ff' : '#f0f9ff',
                      border: `1px solid ${lawLayersOpen ? '#bfdbfe' : '#bae6fd'}`,
                      borderRadius: lawLayersOpen ? '10px 10px 0 0' : 10,
                      cursor: 'pointer', textAlign: 'left',
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <span style={{
                        width: 20, height: 20, borderRadius: '50%', background: '#dbeafe', color: '#1d4ed8',
                        fontSize: 10, fontWeight: 800, display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0,
                      }}>4</span>
                      <span style={{ fontSize: 13, fontWeight: 700, color: '#1e3a5f' }}>📘 법규 근거 보기</span>
                      {lawLayersLoading ? (
                        <span style={{ fontSize: 11, color: '#94a3b8' }}>로딩 중...</span>
                      ) : totalLawCount > 0 ? (
                        <span style={{ fontSize: 11, padding: '2px 9px', borderRadius: 10, background: '#dbeafe', color: '#1d4ed8', fontWeight: 700 }}>
                          {totalLawCount}건
                        </span>
                      ) : null}
                    </div>
                    <span style={{
                      fontSize: 12, fontWeight: 600,
                      color: lawLayersOpen ? '#1d4ed8' : '#0284c7',
                      transition: 'transform 0.15s', display: 'inline-block',
                      transform: lawLayersOpen ? 'rotate(180deg)' : 'none',
                    }}>
                      ▼
                    </span>
                  </button>

                  {lawLayersOpen && (
                    <div style={{ border: '1px solid #e2e8f0', borderTop: 'none', borderRadius: '0 0 10px 10px', overflow: 'hidden', background: '#fff' }}>

                      {/* 건축 기본 법규 */}
                      <div style={{ padding: '12px 14px', borderBottom: '1px solid #f1f5f9' }}>
                        <div style={{ fontSize: 12, fontWeight: 700, color: '#1e293b', marginBottom: 8 }}>건축 기본 법규 <span style={{ fontSize: 11, fontWeight: 500, color: '#94a3b8' }}>적용 법규 목록</span></div>
                        {lawLayersLoading && (
                          <div style={{ padding: '6px 0', fontSize: 12, color: '#6b7280', textAlign: 'center' }}>로딩 중...</div>
                        )}
                        {!lawLayersLoading && (
                          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                            {(lawLayers?.coreLaws ?? []).map((item, i) => (
                              <div key={i} style={{ padding: '8px 10px', borderRadius: 7, border: '1px solid #f1f5f9', background: '#f8fafc' }}>
                                <div style={{ fontSize: 11, fontWeight: 700, color: '#1e3a5f', marginBottom: 2 }}>{item.law}</div>
                                <div style={{ fontSize: 12, color: '#475569' }}>{item.scope}</div>
                              </div>
                            ))}
                            {lawLayers && <SourceNote text={sourceDisplayText(lawLayers.source, lawLayers.confidence)} />}
                          </div>
                        )}
                      </div>

                      {/* 건축 필수 연계 법규 */}
                      <div style={{ padding: '12px 14px', borderBottom: '1px solid #f1f5f9' }}>
                        <div style={{ fontSize: 12, fontWeight: 700, color: '#1e293b', marginBottom: 8 }}>건축 필수 연계 법규 <span style={{ fontSize: 11, fontWeight: 500, color: '#94a3b8' }}>연계 검토 항목</span></div>
                        {lawLayersLoading && (
                          <div style={{ padding: '6px 0', fontSize: 12, color: '#6b7280', textAlign: 'center' }}>로딩 중...</div>
                        )}
                        {!lawLayersLoading && (
                          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                            {(lawLayers?.extendedCoreLaws ?? []).map((item, i) => (
                              <div key={i} style={{ padding: '8px 10px', borderRadius: 7, border: '1px solid #f1f5f9', background: '#f8fafc' }}>
                                <div style={{ fontSize: 11, fontWeight: 700, color: '#1e3a5f', marginBottom: 2 }}>{item.law}</div>
                                <div style={{ fontSize: 12, color: '#475569' }}>{item.scope}</div>
                              </div>
                            ))}
                            {lawLayers && <SourceNote text={sourceDisplayText(lawLayers.source, lawLayers.confidence)} />}
                          </div>
                        )}
                      </div>

                      {/* 협력사 법규 (MEP) */}
                      <div style={{ padding: '12px 14px' }}>
                        <div style={{ fontSize: 12, fontWeight: 700, color: '#1e293b', marginBottom: 8 }}>
                          협력사 법규 <span style={{ fontSize: 11, fontWeight: 500, color: '#94a3b8' }}>소방·설비·전기 연계</span>
                        </div>
                        <div style={{ marginBottom: 8, display: 'flex', alignItems: 'center', gap: 6 }}>
                          <span style={{ fontSize: 11, padding: '3px 8px', borderRadius: 5, background: '#fef3c7', color: '#92400e', fontWeight: 700 }}>
                            연계 검토 필요
                          </span>
                          <span style={{ fontSize: 11, color: '#94a3b8' }}>
                            자동 판정 대상이 아닙니다. 각 담당팀과 협의하세요.
                          </span>
                        </div>
                        {lawLayersLoading && (
                          <div style={{ padding: '6px 0', fontSize: 12, color: '#6b7280', textAlign: 'center' }}>로딩 중...</div>
                        )}
                        {!lawLayersLoading && (
                          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                            {(lawLayers?.mepLaws ?? []).map((item, i) => (
                              <div key={i} style={{ padding: '8px 10px', borderRadius: 7, border: '1px solid #fef3c7', background: '#fffbeb', display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8 }}>
                                <span style={{ fontSize: 12, color: '#334155' }}>{item.title}</span>
                                <span style={{ fontSize: 10, padding: '2px 7px', borderRadius: 4, background: '#fde68a', color: '#92400e', fontWeight: 700, whiteSpace: 'nowrap', flexShrink: 0 }}>
                                  {item.teamTag}
                                </span>
                              </div>
                            ))}
                            {lawLayers && <SourceNote text={sourceDisplayText(lawLayers.source, lawLayers.confidence)} />}
                          </div>
                        )}
                      </div>

                    </div>
                  )}
                </div>
              )
            })()}
              </>
            )}

            {/* ══════════════════════════════════════════════════════ */}
            {/* [5] 주의사항 — 항상 표시                              */}
            {/* ══════════════════════════════════════════════════════ */}
            <StepLabel step={5} title="주의사항" />
            <div style={{ padding: '16px', fontSize: 11, color: '#94a3b8', lineHeight: 1.7, backgroundColor: '#f8fafc', borderRadius: 10, border: '1px solid #f1f5f9', marginBottom: 12 }}>
              <p style={{ margin: '0 0 8px', fontWeight: 700, color: '#64748b' }}>[분석 범위 및 한계]</p>
              {/* 조례 미반영 — 강조 callout */}
              <div style={{ margin: '0 0 8px', padding: '7px 10px', borderRadius: 6, background: '#fef3c7', border: '1px solid #fde68a', color: '#92400e', fontWeight: 600, fontSize: 11, lineHeight: 1.5 }}>
                ⚠️ 국토계획법 기준 판정입니다 — 지자체 조례·지구단위계획이 우선 적용되며 결과가 달라질 수 있습니다.
              </div>
              <p style={{ margin: '0 0 3px' }}>• 면적·층수·용도 혼합 여부에 따라 적용 법규가 달라집니다. 개별 조건을 반드시 확인하세요.</p>
              <p style={{ margin: '0 0 3px' }}>• 소방·설비·전기 기준은 협력사와 별도 협의가 필요합니다.</p>
              <p style={{ margin: 0 }}>• 본 분석은 공공 공간데이터 기반 1차 참고용 결과입니다. 실제 인허가 가능 여부는 관할 관청에 확인하십시오.</p>
            </div>

            {/* ══════════════════════════════════════════════════════ */}
            {/* [6] 간이 계산기 (BCR/FAR) — 용도 선택 후 활성화      */}
            {/* ══════════════════════════════════════════════════════ */}
            {selectedUse && (
              <>
                <StepLabel step={6} title="간이 계산기" sub="BCR · FAR" />
                <div style={{ ...card(), background: '#f5f3ff', border: '1px solid #e0e7ff' }}>
                  <div style={{ fontSize: 11, color: '#6d28d9', marginBottom: 10 }}>
                    계산 결과는 검토 참고값입니다. 법적 확정값이 아닙니다.
                  </div>
                  <form onSubmit={handleCalcSubmit}>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 8, marginBottom: 8 }}>
                      {[
                        { key: 'siteArea',       label: '대지면적 (㎡)' },
                        { key: 'buildingArea',   label: '건축면적 (㎡)' },
                        { key: 'totalFloorArea', label: '연면적 (㎡)'   },
                      ].map(({ key, label }) => (
                        <div key={key}>
                          <div style={{ fontSize: 10, color: '#6d28d9', fontWeight: 600, marginBottom: 3 }}>{label}</div>
                          <input
                            type="number" min="0.01" step="any" placeholder="0"
                            value={calcInputs[key]}
                            onChange={e => setCalcInputs(prev => ({ ...prev, [key]: e.target.value }))}
                            style={{ width: '100%', boxSizing: 'border-box', padding: '5px 7px', borderRadius: 6, border: '1px solid #c4b5fd', fontSize: 12, background: '#fff', outline: 'none' }}
                          />
                        </div>
                      ))}
                    </div>
                    <button
                      type="submit"
                      disabled={calcLoading || !calcInputs.siteArea || !calcInputs.buildingArea || !calcInputs.totalFloorArea}
                      style={{ width: '100%', padding: '7px', borderRadius: 6, border: 'none', background: '#7c3aed', color: '#fff', fontSize: 12, fontWeight: 700, cursor: 'pointer', opacity: calcLoading ? 0.6 : 1 }}
                    >
                      {calcLoading ? '계산 중...' : '계산하기'}
                    </button>
                  </form>
                  {calcError && <div style={{ marginTop: 8, fontSize: 11, color: '#b91c1c' }}>{calcError}</div>}
                  {calcResult && !calcLoading && (
                    <div style={{ marginTop: 10, display: 'flex', flexDirection: 'column', gap: 6 }}>
                      {calcResult.map(item => {
                        const exceeded = item.isExceeded
                        const hasLimit = item.limit != null
                        const bg       = exceeded ? '#fef2f2' : hasLimit ? '#f0fdf4' : '#f8fafc'
                        const clr      = exceeded ? '#dc2626' : hasLimit ? '#16a34a' : '#64748b'
                        const brd      = exceeded ? '#fecaca' : hasLimit ? '#bbf7d0' : '#e2e8f0'
                        return (
                          <div key={item.type} style={{ padding: '9px 10px', borderRadius: 7, border: `1px solid ${brd}`, background: bg }}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 3 }}>
                              <span style={{ fontSize: 12, fontWeight: 700, color: '#1e293b' }}>{item.label} ({item.type})</span>
                              <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                                {hasLimit && <span style={{ fontSize: 11, color: '#94a3b8' }}>한도 {item.limit}%</span>}
                                <span style={{ fontSize: 15, fontWeight: 800, color: clr }}>{item.value}%</span>
                              </div>
                            </div>
                            {hasLimit && (
                              <div style={{ height: 5, borderRadius: 3, background: '#e5e7eb', overflow: 'hidden', marginBottom: 4 }}>
                                <div style={{ height: '100%', borderRadius: 3, background: exceeded ? '#dc2626' : '#16a34a', width: `${Math.min(item.value / item.limit * 100, 100)}%`, transition: 'width 0.4s ease' }} />
                              </div>
                            )}
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                              <span style={{ fontSize: 10, color: '#94a3b8' }}>{item.note}</span>
                              {exceeded && <span style={{ fontSize: 10, fontWeight: 700, color: '#dc2626', background: '#fef2f2', padding: '1px 6px', borderRadius: 4 }}>법정 상한 초과</span>}
                            </div>
                          </div>
                        )
                      })}
                      {calcMeta && <SourceNote text={sourceDisplayText(calcMeta.source, calcMeta.confidence)} />}
                    </div>
                  )}
                </div>
              </>
            )}

            {/* 좌표 정보 */}
            <div style={{ padding: '0 4px', marginBottom: 20, marginTop: 4 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11, color: '#94a3b8' }}>
                <span>WGS84 Lon: {formatCoord(selectedCoordinate.lon)} / Lat: {formatCoord(selectedCoordinate.lat)}</span>
                <span title={`Feature Count: ${result.meta?.featureCount}`}>Data Ver: {new Date(result.meta?.loadedAt).toLocaleDateString()}</span>
              </div>
            </div>
          </>
        )
      })()}
    </aside>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// 서브 컴포넌트
// ─────────────────────────────────────────────────────────────────────────────

// ─────────────────────────────────────────────────────────────────────────────
// 진입 가이드 — 결과 없을 때 표시
// ─────────────────────────────────────────────────────────────────────────────

const USE_ICONS = {
  '공동주택':          '🏢',
  '제1종근린생활시설': '🏪',
  '제2종근린생활시설': '🏬',
  '업무시설':          '🏗️',
  '교육시설':          '🏫',
  '의료시설':          '🏥',
  '숙박시설':          '🏨',
  '공장':             '🏭',
  '창고시설':          '📦',
  '물류시설':          '🚚',
}

function EntryGuide({ pendingUse, onPendingUseSelect, useOptions }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 0 }}>

      {/* ── STEP 1 ── */}
      <div style={{
        display: 'flex', alignItems: 'flex-start', gap: 12,
        padding: '16px 4px 14px',
        borderBottom: '1px solid #f1f5f9',
      }}>
        <StepDot n={1} done={false} />
        <div>
          <div style={{ fontSize: 14, fontWeight: 700, color: '#0f172a', marginBottom: 3 }}>
            주소를 검색하세요
          </div>
          <div style={{ fontSize: 12, color: '#64748b', lineHeight: 1.6 }}>
            위 검색창에 도로명 또는 지번 주소를 입력하거나,<br />
            지도를 직접 클릭해도 됩니다.
          </div>
        </div>
      </div>

      {/* ── STEP 2 ── */}
      <div style={{
        display: 'flex', alignItems: 'flex-start', gap: 12,
        padding: '16px 4px 14px',
        borderBottom: '1px solid #f1f5f9',
      }}>
        <StepDot n={2} done={false} />
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 14, fontWeight: 700, color: '#0f172a', marginBottom: 6 }}>
            계획 용도를 선택하세요
            <span style={{ marginLeft: 8, fontSize: 11, fontWeight: 500, color: '#94a3b8' }}>
              미리 선택해도 됩니다
            </span>
          </div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
            {useOptions.map(use => {
              const selected = pendingUse === use
              return (
                <button
                  key={use}
                  onClick={() => onPendingUseSelect(selected ? null : use)}
                  style={{
                    display: 'flex', alignItems: 'center', gap: 5,
                    padding: '7px 13px', borderRadius: 20, fontSize: 12, fontWeight: 600,
                    cursor: 'pointer', border: '1px solid',
                    borderColor: selected ? '#7c3aed' : '#e2e8f0',
                    background:  selected ? '#7c3aed' : '#f8fafc',
                    color:       selected ? '#fff'    : '#475569',
                    transition: 'all 0.15s',
                  }}
                >
                  <span style={{ fontSize: 14 }}>{USE_ICONS[use] ?? '📋'}</span>
                  {use}
                </button>
              )
            })}
          </div>
          {pendingUse && (
            <div style={{
              marginTop: 8, fontSize: 11, color: '#7c3aed',
              background: '#ede9fe', padding: '5px 10px', borderRadius: 6,
              fontWeight: 600,
            }}>
              ✓ {pendingUse} 선택됨 — 주소 검색 후 자동으로 판정이 시작됩니다
            </div>
          )}
        </div>
      </div>

      {/* ── STEP 3 ── */}
      <div style={{
        display: 'flex', alignItems: 'flex-start', gap: 12,
        padding: '16px 4px 8px',
      }}>
        <StepDot n={3} done={false} dim />
        <div>
          <div style={{ fontSize: 14, fontWeight: 700, color: '#94a3b8', marginBottom: 3 }}>
            Quick 판정 결과 확인
          </div>
          <div style={{ fontSize: 12, color: '#94a3b8', lineHeight: 1.6 }}>
            용적률·건폐율·피난계단·방화구획 등<br />
            핵심 항목을 즉시 확인할 수 있습니다.
          </div>
        </div>
      </div>

      {/* 하단 안내 */}
      <div style={{
        marginTop: 16, padding: '10px 12px', borderRadius: 8,
        background: '#f8fafc', border: '1px solid #f1f5f9',
        fontSize: 10, color: '#94a3b8', lineHeight: 1.7, textAlign: 'center',
      }}>
        본 판정은 공간데이터 기반 1차 참고용 결과입니다.<br />
        실제 인허가 확정은 관할 관청에서 확인하세요.
      </div>
    </div>
  )
}

function StepDot({ n, done, dim }) {
  return (
    <div style={{
      width: 24, height: 24, borderRadius: '50%', flexShrink: 0, marginTop: 1,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontSize: 11, fontWeight: 800,
      background: dim ? '#f1f5f9' : done ? '#7c3aed' : '#0f172a',
      color: dim ? '#94a3b8' : '#fff',
    }}>
      {done ? '✓' : n}
    </div>
  )
}

function StepLabel({ step, title, sub }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, margin: '16px 0 6px' }}>
      <span style={{
        width: 20, height: 20, borderRadius: '50%', background: '#e2e8f0', color: '#64748b',
        fontSize: 10, fontWeight: 800, display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0,
      }}>
        {step}
      </span>
      <span style={{ fontSize: 13, fontWeight: 700, color: '#0f172a' }}>{title}</span>
      {sub && <span style={{ fontSize: 11, color: '#94a3b8', fontWeight: 500 }}>{sub}</span>}
    </div>
  )
}

function StatusBadge({ label, bg, color, border = 'none' }) {
  return (
    <span style={{ padding: '4px 10px', borderRadius: 6, fontSize: 11, fontWeight: 700, backgroundColor: bg, color, border, whiteSpace: 'nowrap' }}>
      {label}
    </span>
  )
}

function SourceBadge({ label }) {
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', fontSize: 10, padding: '2px 7px', borderRadius: 4, background: '#f1f5f9', color: '#64748b', fontWeight: 500 }}>
      {label}
    </span>
  )
}

function SourceNote({ text }) {
  return (
    <div style={{ marginTop: 6, fontSize: 10, color: '#94a3b8', textAlign: 'right' }}>
      {text}
    </div>
  )
}
