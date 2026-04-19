import { useState, useEffect, useCallback } from 'react'
import { fetchApi } from '../utils/apiClient'

const JUDGE_META = {
  active: { label: '자동 판정 완료', color: '#0369a1', bg: '#e0f2fe', border: '#bae6fd' },
  reference: { label: '법규 확인 필요', color: '#92400e', bg: '#fef3c7', border: '#fde68a' },
  pending: { label: '추가 입력 필요', color: '#64748b', bg: '#f1f5f9', border: '#e2e8f0' },
}

const PRIORITY_STYLE = {
  high: { borderLeft: '3px solid #fca5a5' },
  medium: { borderLeft: '3px solid #fde68a' },
  low: { borderLeft: '3px solid #e2e8f0' },
}

const CATEGORY_ORDER = ['밀도', '접근', '주차', '피난', '방화', '인허가', '조례', '협의', '기타']

const CATEGORY_COLOR = {
  밀도: '#dc2626',
  접근: '#1d4ed8',
  주차: '#0f766e',
  피난: '#d97706',
  방화: '#7c3aed',
  인허가: '#be123c',
  조례: '#4338ca',
  협의: '#0891b2',
  기타: '#94a3b8',
}

function getActiveSubStatus(note) {
  if (!note) return 'ok'
  if (/(불가|초과|위반|금지|부적합|미충족)/.test(note)) return 'danger'
  if (/(추가|확인 필요|검토 필요|주의|협의)/.test(note)) return 'warning'
  return 'ok'
}

const ACTIVE_SUB_META = {
  danger: { label: '위험', color: '#991b1b', bg: '#fef2f2', border: '#fecaca' },
  warning: { label: '주의', color: '#92400e', bg: '#fef3c7', border: '#fde68a' },
  ok: { label: '적합', color: '#166534', bg: '#f0fdf4', border: '#bbf7d0' },
}

const HINT_INPUT_META = {
  siteArea: { label: '대지면적', unit: 'm²', type: 'number', hint: '대상 필지의 면적입니다.' },
  buildingArea: { label: '건축면적', unit: 'm²', type: 'number', hint: '건축선 후퇴를 반영한 건축면적입니다.' },
  floorArea: { label: '연면적', unit: 'm²', type: 'number', hint: '주용도 기준 연면적입니다.' },
  floorCount: { label: '층수', unit: '층', type: 'number', hint: '지상층 기준으로 입력합니다.' },
  buildingHeight: { label: '높이', unit: 'm', type: 'number', hint: '계획 건축물의 최고 높이입니다.' },
  roadFrontageWidth: { label: '도로폭', unit: 'm', type: 'number', hint: '접하는 도로의 유효 폭입니다.' },
  unitCount: { label: '세대수', unit: '세대', type: 'number', hint: '공동주택 총 세대수입니다.' },
  unitArea: { label: '세대당 전용면적', unit: 'm²', type: 'number', hint: '전용면적 기준입니다.' },
  housingSubtype: { label: '공동주택 유형', type: 'select', options: ['아파트', '연립', '다세대'] },
  parkingType: { label: '주차 방식', type: 'select', options: ['underground', 'ground', 'mechanical'], display: ['지하', '지상', '기계식'] },
  detailUseSubtype: { label: '세부 업종', type: 'text', placeholder: '예: 의원, 슈퍼마켓, 학원', hint: '근린생활시설·업무시설의 세부 업종을 입력합니다.' },
  detailUseFloorArea: { label: '업종 바닥면적', unit: 'm²', type: 'number', hint: '세부 업종에 해당하는 면적입니다.' },
  isMultipleOccupancy: { label: '다중이용 여부', type: 'select', options: ['true', 'false'], display: ['예', '아니오'] },
  officeSubtype: { label: '업무시설 유형', type: 'select', options: ['오피스텔', '일반업무'] },
  occupantCount: { label: '수용인원', unit: '명', type: 'number', hint: '동시 점유 기준 인원입니다.' },
  mixedUseRatio: { label: '혼합용도 비율', unit: '%', type: 'number', placeholder: '예: 30', hint: '해당 용도가 차지하는 연면적 비율입니다.' },
  roomCount: { label: '실수', unit: '실', type: 'number', hint: '근린생활·숙박·창고 등에서 사용하는 총 실수입니다.' },
  guestRoomCount: { label: '객실수', unit: '실', type: 'number', hint: '숙박시설 객실 총량입니다.' },
  bedCount: { label: '병상수', unit: '병상', type: 'number', hint: '의료시설 병상 총량입니다.' },
  studentCount: { label: '학생수', unit: '명', type: 'number', hint: '교육시설 학생수입니다.' },
  vehicleIngressType: { label: '차량 출입방식', type: 'text', placeholder: '예: 전면 진출입 / 후면 하역', hint: '물류·공장·창고 차량 동선을 간단히 적습니다.' },
  educationSpecialCriteria: { label: '교육 특수기준', type: 'text', placeholder: '예: 통학로, 운동장, 학년 구성', hint: '교육시설 특수 기준을 적습니다.' },
  medicalSpecialCriteria: { label: '의료 특수기준', type: 'text', placeholder: '예: 응급, 중환자실, 수술실', hint: '의료시설 특별법 검토 포인트입니다.' },
  accommodationSpecialCriteria: { label: '숙박 특수기준', type: 'text', placeholder: '예: 객실형태, 부대시설', hint: '숙박 운영 특수사항을 적습니다.' },
  hazardousMaterialProfile: { label: '위험물 프로필', type: 'text', placeholder: '예: 저장 탱크, 위험물 분류', hint: '공장 위험물 검토 기준입니다.' },
  logisticsOperationProfile: { label: '물류 운영 프로필', type: 'text', placeholder: '예: 하역대수, 출입차량 규모', hint: '물류·창고 운영 특성을 적습니다.' },
}

const JSON_HEADERS = {
  'Content-Type': 'application/json',
}

export default function BuildingReviewPanel({ selectedUse, coordinate, onBuildingInputsChange }) {
  const [formValues, setFormValues] = useState({})
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [hintOpen, setHintOpen] = useState(false)
  const [hintForm, setHintForm] = useState({})
  const [aiStatus, setAiStatus] = useState(null)
  const [researchStatus, setResearchStatus] = useState(null)
  const [aiPreview, setAiPreview] = useState(null)
  const [aiLoading, setAiLoading] = useState(false)
  const [aiError, setAiError] = useState(null)
  const [aiQuery, setAiQuery] = useState('')
  const [aiRunResult, setAiRunResult] = useState(null)
  const [aiRunLoading, setAiRunLoading] = useState(false)
  const [officialLawQuery, setOfficialLawQuery] = useState('')
  const [officialLawSearch, setOfficialLawSearch] = useState(null)
  const [officialLawBody, setOfficialLawBody] = useState(null)
  const [officialLawLoading, setOfficialLawLoading] = useState(false)
  const [officialLawError, setOfficialLawError] = useState(null)

  useEffect(() => {
    let cancelled = false

    async function loadStatuses() {
      try {
        const [aiRes, researchRes] = await Promise.all([
          fetchApi('/api/regulation-check/research/ai-assist/status'),
          fetchApi('/api/regulation-check/research/status'),
        ])
        if (!aiRes.ok || !researchRes.ok) throw new Error('status-error')
        const [aiPayload, researchPayload] = await Promise.all([
          aiRes.json(),
          researchRes.json(),
        ])
        if (!cancelled) {
          setAiStatus(aiPayload)
          setResearchStatus(researchPayload)
        }
      } catch {
        if (!cancelled) {
          setAiStatus(null)
          setResearchStatus(null)
        }
      }
    }

    loadStatuses()
    return () => { cancelled = true }
  }, [])

  const fetchReview = useCallback(async (buildingInputs, reviewLevel) => {
    if (!selectedUse || !coordinate) return
    setLoading(true)
    setError(null)

    const hasInputs = Object.keys(buildingInputs).length > 0
    const parsedInputs = hasInputs ? parseInputs(buildingInputs) : undefined

    try {
      const body = {
        longitude: coordinate.lon,
        latitude: coordinate.lat,
        selectedUse,
        ...(parsedInputs && { buildingInputs: parsedInputs }),
        ...(reviewLevel && { reviewLevel }),
      }
      const res = await fetchApi('/api/regulation-check/review', {
        method: 'POST',
        headers: JSON_HEADERS,
        body: JSON.stringify(body),
      })
      if (!res.ok) throw new Error('api-error')
      const payload = await res.json()
      setData(payload)
    } catch {
      setError('판정 조회에 실패했습니다.')
    } finally {
      setLoading(false)
    }
  }, [selectedUse, coordinate])

  useEffect(() => {
    if (!selectedUse || !coordinate) {
      setData(null)
      setAiPreview(null)
      setAiRunResult(null)
      setOfficialLawQuery('')
      setOfficialLawSearch(null)
      setOfficialLawBody(null)
      return
    }
    setFormValues({})
    setHintForm({})
    setHintOpen(false)
    fetchReview({}, null)
  }, [selectedUse, coordinate?.lat, coordinate?.lon, fetchReview])

  useEffect(() => {
    if (!data) return

    const zoningLabel = data?.zoning?.zoneName
    const suggested = [selectedUse, zoningLabel, '건축법'].filter(Boolean).join(' ')
    setOfficialLawQuery(prev => prev || suggested)
  }, [data, selectedUse])

  useEffect(() => {
    if (!selectedUse || !data) {
      setAiPreview(null)
      return
    }

    const requestBody = {
      selectedUse,
      useProfile: data.useProfile ?? null,
      planningContext: {
        reviewLevel: data.reviewLevel,
        zoning: data.zoning ? { zoneName: data.zoning.zoneName, districtUnitPlan: data.zoning.isDistrictUnitPlan } : null,
        checklistSummary: data.projectChecklist?.summaryLines ?? [],
        applicableLawSummary: data.applicableLaws?.summaryLines ?? [],
      },
      reviewItems: [],
      tasks: [],
      manualReviewSet: [],
      ordinanceRegion: data.location?.address ?? data.location?.regionName ?? null,
    }

    let cancelled = false

    async function loadPreview() {
      setAiLoading(true)
      setAiError(null)
      try {
        const res = await fetchApi('/api/regulation-check/research/ai-assist-preview', {
          method: 'POST',
          headers: JSON_HEADERS,
          body: JSON.stringify(requestBody),
        })
        if (!res.ok) throw new Error('preview-error')
        const payload = await res.json()
        if (!cancelled) setAiPreview(payload)
      } catch {
        if (!cancelled) {
          setAiPreview(null)
          setAiError('Gemma4 미리보기를 불러오지 못했습니다.')
        }
      } finally {
        if (!cancelled) setAiLoading(false)
      }
    }

    loadPreview()
    return () => { cancelled = true }
  }, [selectedUse, data])

  const handleAiRun = async () => {
    if (!selectedUse || !data) return

    const prompt = aiQuery.trim() || '현재 프로젝트에서 토지와 수치만으로 확인 가능한 주요 법규와 추가 확인 필요 항목을 요약해줘.'
    setOfficialLawQuery(prompt)
    void runOfficialLawSearch(prompt)
    const requestBody = {
      selectedUse,
      userPrompt: prompt,
      useProfile: data.useProfile ?? null,
      planningContext: {
        reviewLevel: data.reviewLevel,
        zoning: data.zoning ? { zoneName: data.zoning.zoneName, districtUnitPlan: data.zoning.isDistrictUnitPlan } : null,
        checklistSummary: data.projectChecklist?.summaryLines ?? data.checklist?.summaryLines ?? [],
        applicableLawSummary: data.applicableLaws?.summaryLines ?? [],
        reviewTriggerTitles: (data.reviewTriggers ?? []).map(trigger => trigger.title).slice(0, 8),
      },
      reviewItems: [],
      tasks: [],
      manualReviewSet: [],
      ordinanceRegion: data.location?.resolvedAddress ?? data.location?.inputAddress ?? null,
    }

    setAiRunLoading(true)
    setAiError(null)
    try {
      const res = await fetchApi('/api/regulation-check/research/ai-assist/run', {
        method: 'POST',
        headers: JSON_HEADERS,
        body: JSON.stringify(requestBody),
      })
      if (!res.ok) throw new Error('run-error')
      const payload = await res.json()
      setAiRunResult(payload)
    } catch {
      setAiRunResult(null)
      setAiError('AI 보조 기능 실행에 실패했습니다.')
    } finally {
      setAiRunLoading(false)
    }
  }

  const runOfficialLawSearch = useCallback(async (overrideQuery) => {
    const query = (overrideQuery ?? officialLawQuery).trim()
    if (!query) return

    setOfficialLawLoading(true)
    setOfficialLawError(null)
    setOfficialLawBody(null)

    try {
      const res = await fetchApi('/api/regulation-check/research/official-law/search', {
        method: 'POST',
        headers: JSON_HEADERS,
        body: JSON.stringify({
          query,
          target: 'law',
          display: 10,
        }),
      })
      if (!res.ok) throw new Error('official-law-search-error')
      const payload = await res.json()
      setOfficialLawSearch(payload)
    } catch {
      setOfficialLawSearch(null)
      setOfficialLawError('공식 법령 검색을 불러오지 못했습니다.')
    } finally {
      setOfficialLawLoading(false)
    }
  }, [officialLawQuery])

  const handleOfficialLawSearch = useCallback(() => {
    runOfficialLawSearch()
  }, [runOfficialLawSearch])

  const handleOfficialLawBody = async (item) => {
    if (!item) return

    setOfficialLawLoading(true)
    setOfficialLawError(null)

    try {
      const res = await fetchApi('/api/regulation-check/research/official-law/body', {
        method: 'POST',
        headers: JSON_HEADERS,
        body: JSON.stringify({
          target: item.target ?? 'law',
          id: item.id ?? null,
          mst: item.mst ?? null,
        }),
      })
      if (!res.ok) throw new Error('official-law-body-error')
      const payload = await res.json()
      setOfficialLawBody(payload)
    } catch {
      setOfficialLawBody(null)
      setOfficialLawError('법령 본문을 불러오지 못했습니다.')
    } finally {
      setOfficialLawLoading(false)
    }
  }

  const handleHintSubmit = (e) => {
    e.preventDefault()
    const merged = { ...formValues, ...hintForm }
    setFormValues(merged)
    setHintOpen(false)
    fetchReview(merged, null)
    onBuildingInputsChange?.(parseInputs(merged))
  }

  const items = data?.reviewItems ?? []
  const hint = data?.nextLevelHint
  const summary = data?.inputSummary
  const zoning = data?.zoning
  const level = data?.reviewLevel ?? 'quick'

  const priorityOrder = { high: 0, medium: 1, low: 2 }
  const sortedItems = [...items].sort((a, b) => {
    const pa = priorityOrder[a.priority] ?? 2
    const pb = priorityOrder[b.priority] ?? 2
    if (pa !== pb) return pa - pb
    const ai = CATEGORY_ORDER.indexOf(a.category)
    const bi = CATEGORY_ORDER.indexOf(b.category)
    return (ai === -1 ? 99 : ai) - (bi === -1 ? 99 : bi)
  })

  const statusCount = items.reduce((acc, it) => {
    acc[it.judgeStatus] = (acc[it.judgeStatus] ?? 0) + 1
    return acc
  }, {})

  return (
    <div>
      {loading && (
        <div style={{ padding: '16px 0', textAlign: 'center', color: '#6b7280', fontSize: 12 }}>
          판정 중...
        </div>
      )}

      {!loading && error && (
        <div style={{ padding: '12px', fontSize: 12, color: '#b91c1c', background: '#fef2f2', borderRadius: 8, border: '1px solid #fecaca' }}>
          {error}
        </div>
      )}

      {!loading && !error && data && (
        <>
          <SummaryBar level={level} zoning={zoning} statusCount={statusCount} summary={summary} items={sortedItems} selectedUse={selectedUse} />
          <ApplicableLawPanel catalog={data?.applicableLaws} triggers={data?.reviewTriggers} />
          <OfficialLawWidget
            status={researchStatus}
            query={officialLawQuery}
            onQueryChange={setOfficialLawQuery}
            onSearch={handleOfficialLawSearch}
            searchResult={officialLawSearch}
            bodyResult={officialLawBody}
            loading={officialLawLoading}
            error={officialLawError}
            onSelectItem={handleOfficialLawBody}
          />
          <AiAssistWidget
            status={aiStatus}
            preview={aiPreview}
            loading={aiLoading}
            error={aiError}
            query={aiQuery}
            onQueryChange={setAiQuery}
            onRun={handleAiRun}
            runLoading={aiRunLoading}
            runResult={aiRunResult}
          />

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

          <div style={{ marginTop: 8, fontSize: 10, color: '#94a3b8', textAlign: 'right' }}>
            초기 검토 기준 · 공간데이터 참고값 · 허가 확정은 관할부서 확인 · {data.elapsedMs}ms
          </div>
        </>
      )}
    </div>
  )
}

function SummaryBar({ level, zoning, statusCount, summary, items = [], selectedUse }) {
  const levelLabel = {
    quick: { label: '빠른 검토', desc: '좌표·용도지역 기반 — 입력 없이 즉시 판정', color: '#475569', bg: '#f1f5f9' },
    standard: { label: '표준 검토', desc: '연면적·층수 입력 → 밀도·피난·방화 계산 판정', color: '#0369a1', bg: '#e0f2fe' },
    detailed: { label: '상세 검토', desc: '세부 입력 반영 · Task/Checklist/수동검토 포함', color: '#6d28d9', bg: '#ede9fe' },
    expert: { label: '전문가 검토', desc: '모든 입력 기반 전체 항목 판정', color: '#065f46', bg: '#d1fae5' },
  }[level] ?? { label: level, desc: '', color: '#475569', bg: '#f1f5f9' }

  const dangerItems = items.filter(i => i.judgeStatus === 'active' && getActiveSubStatus(i.judgeNote) === 'danger')
  const warningItems = items.filter(i => i.judgeStatus === 'active' && getActiveSubStatus(i.judgeNote) === 'warning')
  const hasIssues = dangerItems.length > 0 || warningItems.length > 0
  const criticalItems = [...dangerItems, ...warningItems].slice(0, 4)

  let oneLiner = null
  if (items.length > 0) {
    if (!hasIssues) {
      const okCount = items.filter(i => i.judgeStatus === 'active').length
      if (okCount > 0) oneLiner = `${selectedUse ?? '선택 용도'} · 검토 항목 ${okCount}건 적합`
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
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 5, alignItems: 'center' }}>
        <span style={{ fontSize: 11, fontWeight: 700, padding: '3px 9px', borderRadius: 5, background: levelLabel.bg, color: levelLabel.color }}>
          {levelLabel.label}
        </span>
        {levelLabel.desc && <span style={{ fontSize: 10, color: '#94a3b8', fontWeight: 400 }}>{levelLabel.desc}</span>}
        {zoning?.zoneName && (
          <span style={{ fontSize: 11, fontWeight: 600, padding: '3px 9px', borderRadius: 5, background: '#f0fdf4', color: '#15803d' }}>
            {zoning.zoneName}
            {zoning.farLimitPct != null && ` · 용적률 ${zoning.farLimitPct}%`}
            {zoning.bcRatioLimitPct != null && ` · 건폐율 ${zoning.bcRatioLimitPct}%`}
          </span>
        )}
        {Object.entries(statusCount).map(([status, count]) => {
          const meta = JUDGE_META[status]
          if (!meta) return null
          return (
            <span key={status} style={{ fontSize: 10, fontWeight: 600, padding: '2px 7px', borderRadius: 4, background: meta.bg, color: meta.color }}>
              {meta.label} {count}
            </span>
          )
        })}
      </div>

      {oneLiner && (
        <div
          style={{
            fontSize: 12,
            fontWeight: 700,
            lineHeight: 1.4,
            padding: '7px 12px',
            borderRadius: 7,
            color: hasIssues ? '#7c2d12' : '#14532d',
            background: hasIssues ? '#fff7ed' : '#f0fdf4',
            border: `1px solid ${hasIssues ? '#fed7aa' : '#bbf7d0'}`,
          }}
        >
          {hasIssues ? '주의 ' : 'OK '}{oneLiner}
        </div>
      )}

      {criticalItems.length > 0 && (
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
          {criticalItems.map((item, index) => {
            const meta = ACTIVE_SUB_META[getActiveSubStatus(item.judgeNote)]
            return (
              <span key={`${item.title}-${index}`} style={{ fontSize: 10, padding: '2px 8px', borderRadius: 4, fontWeight: 700, background: meta.bg, color: meta.color, border: `1px solid ${meta.border}` }}>
                {meta.label} {item.title}
              </span>
            )
          })}
        </div>
      )}

      {summary?.missingNote && (
        <div style={{ fontSize: 11, color: '#92400e', background: '#fef3c7', padding: '6px 10px', borderRadius: 6, border: '1px solid #fde68a', lineHeight: 1.5 }}>
          입력 안내 · {summary.missingNote}
        </div>
      )}
    </div>
  )
}

function ReviewItemCard({ item }) {
  const [open, setOpen] = useState(false)
  const judgeMeta = item.judgeStatus === 'active'
    ? ACTIVE_SUB_META[getActiveSubStatus(item.judgeNote)]
    : (JUDGE_META[item.judgeStatus] ?? JUDGE_META.pending)
  const priorityStyle = PRIORITY_STYLE[item.priority] ?? PRIORITY_STYLE.low
  const categoryColor = CATEGORY_COLOR[item.category] ?? '#94a3b8'
  const subStatus = item.judgeStatus === 'active' ? getActiveSubStatus(item.judgeNote) : null

  return (
    <div
      onClick={() => setOpen(v => !v)}
      style={{
        padding: '9px 10px',
        borderRadius: 8,
        border: `1px solid ${subStatus === 'danger' ? '#fca5a5' : item.priority === 'high' ? '#fca5a5' : '#f1f5f9'}`,
        background: subStatus === 'danger' ? '#fff7f7' : subStatus === 'warning' ? '#fffbeb' : '#fff',
        cursor: 'pointer',
        ...priorityStyle,
      }}
    >
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
        <div style={{ display: 'flex', gap: 5, alignItems: 'center', flexWrap: 'wrap', flex: 1 }}>
          <span style={{ width: 6, height: 6, borderRadius: '50%', background: categoryColor, display: 'inline-block', flexShrink: 0, marginTop: 1 }} />
          <span style={{ fontSize: 12, fontWeight: 700, color: '#1e293b', lineHeight: 1.4 }}>{item.title}</span>
        </div>
        <div style={{ display: 'flex', gap: 4, alignItems: 'center', flexShrink: 0 }}>
          {item.priority === 'high' && (
            <span style={{ fontSize: 9, padding: '2px 5px', borderRadius: 3, background: '#fef2f2', color: '#b91c1c', fontWeight: 800, whiteSpace: 'nowrap' }}>
              우선
            </span>
          )}
          <span style={{ fontSize: 9, padding: '2px 6px', borderRadius: 4, whiteSpace: 'nowrap', background: judgeMeta.bg, color: judgeMeta.color, fontWeight: 700, border: `1px solid ${judgeMeta.border}` }}>
            {judgeMeta.label}
          </span>
          <span style={{ fontSize: 10, color: '#94a3b8', whiteSpace: 'nowrap' }}>{open ? '접기' : '상세'}</span>
        </div>
      </div>

      {item.judgeNote && (
        <div style={{ marginTop: 6, padding: '6px 8px', borderRadius: 5, background: judgeMeta.bg, border: `1px solid ${judgeMeta.border}`, fontSize: 11, color: judgeMeta.color, lineHeight: 1.6, fontWeight: 500 }}>
          {item.judgeNote}
        </div>
      )}

      {open && (
        <div style={{ marginTop: 8, paddingTop: 8, borderTop: '1px solid #f1f5f9' }}>
          <div style={{ fontSize: 12, color: '#64748b', lineHeight: 1.6, marginBottom: 5 }}>{item.description}</div>
          {item.requiredInputs?.length > 0 && <div style={{ fontSize: 10, color: '#94a3b8', marginBottom: 3 }}>필요 입력: {item.requiredInputs.join(' / ')}</div>}
          {item.relatedLaws?.length > 0 && <div style={{ fontSize: 10, color: '#94a3b8' }}>{item.relatedLaws.join(' · ')}</div>}
        </div>
      )}
    </div>
  )
}

function NextLevelHintBlock({ hint, hintOpen, hintForm, onToggle, onChange, onSubmit }) {
  if (!hint.additionalInputsNeeded?.length) {
    return (
      <div style={{ marginTop: 10, padding: '10px 12px', borderRadius: 8, background: '#f0fdf4', border: '1px solid #bbf7d0', fontSize: 11, color: '#15803d' }}>
        {hint.note ?? '다음 단계에 필요한 입력이 모두 충족되었습니다.'}
      </div>
    )
  }

  const nextLevelLabel = {
    standard: '표준',
    detailed: '상세',
    expert: '전문가',
  }[hint.nextLevel] ?? hint.nextLevel

  return (
    <div style={{ marginTop: 10, border: '1px solid #e0e7ff', borderRadius: 10, background: '#f5f3ff', overflow: 'hidden' }}>
      <button
        onClick={onToggle}
        style={{ width: '100%', display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '10px 12px', background: 'none', border: 'none', cursor: 'pointer', gap: 8 }}
      >
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: 2 }}>
          <span style={{ fontSize: 12, fontWeight: 700, color: '#4c1d95' }}>
            {nextLevelLabel} 단계로 이동하면 추가 Task {hint.willUnlock?.length ?? 0}개를 더 계산합니다.
          </span>
          {hint.note && <span style={{ fontSize: 10, color: '#6d28d9', textAlign: 'left' }}>{hint.note}</span>}
        </div>
        <span style={{ fontSize: 11, color: '#6d28d9', flexShrink: 0 }}>{hintOpen ? '입력 닫기' : '입력하기'}</span>
      </button>

      {hint.willAddTaskCategories?.length > 0 && (
        <div style={{ padding: '0 12px 8px', display: 'flex', flexWrap: 'wrap', gap: 4 }}>
          {hint.willAddTaskCategories.map((label, index) => (
            <span key={`${label}-${index}`} style={{ fontSize: 10, padding: '2px 7px', borderRadius: 4, background: '#ede9fe', color: '#6d28d9', fontWeight: 500 }}>
              {label}
            </span>
          ))}
        </div>
      )}

      {hintOpen && (
        <form onSubmit={onSubmit} style={{ padding: '0 12px 12px', borderTop: '1px solid #ddd6fe' }}>
          <div style={{ marginTop: 10, display: 'grid', gap: 8 }}>
            {hint.additionalInputsNeeded.map(({ field, label, reason }) => {
              const meta = HINT_INPUT_META[field] ?? { label, type: 'text' }
              return (
                <div key={field}>
                  <label style={{ fontSize: 10, fontWeight: 700, color: '#6d28d9', display: 'block', marginBottom: 2 }}>
                    {meta.label ?? label}
                    {meta.unit && <span style={{ fontWeight: 400, color: '#94a3b8', marginLeft: 3 }}>({meta.unit})</span>}
                    {reason && <span style={{ fontWeight: 400, color: '#a78bfa', marginLeft: 5 }}>{reason}</span>}
                  </label>
                  {meta.hint && <div style={{ fontSize: 10, color: '#94a3b8', marginBottom: 4, lineHeight: 1.4 }}>입력 힌트 · {meta.hint}</div>}
                  {meta.type === 'select' ? (
                    <select value={hintForm[field] ?? ''} onChange={e => onChange(field, e.target.value)} style={inputStyle}>
                      <option value="">선택</option>
                      {meta.options?.map((option, index) => (
                        <option key={option} value={option}>{meta.display?.[index] ?? option}</option>
                      ))}
                    </select>
                  ) : (
                    <input
                      type={meta.type ?? 'text'}
                      min={meta.type === 'number' ? '0' : undefined}
                      step={meta.type === 'number' ? 'any' : undefined}
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
          <button type="submit" style={{ marginTop: 10, width: '100%', padding: '8px', borderRadius: 6, border: 'none', background: '#7c3aed', color: '#fff', fontSize: 12, fontWeight: 700, cursor: 'pointer' }}>
            {nextLevelLabel} 단계로 재판정
          </button>
        </form>
      )}
    </div>
  )
}

function AiAssistCard({ status, preview, loading, error }) {
  const configuredLabel = status?.isConfigured ? '연결 준비 완료' : '미설정'
  const configuredColor = status?.isConfigured ? '#166534' : '#92400e'
  const configuredBg = status?.isConfigured ? '#f0fdf4' : '#fef3c7'

  return (
    <div style={{ marginTop: 10, border: '1px solid #dbeafe', borderRadius: 10, background: '#f8fbff', padding: '12px 14px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
        <div>
          <div style={{ fontSize: 12, fontWeight: 800, color: '#1d4ed8', marginBottom: 4 }}>AI Assist · Gemma4</div>
          <div style={{ fontSize: 11, color: '#64748b', lineHeight: 1.6 }}>
            법규 위치 안내, 조문 번호 안내, 검색 키워드 추천, 조례 탐색, 누락 검토 리마인드만 제공합니다.
          </div>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4, alignItems: 'flex-end' }}>
          {status && (
            <>
              <span style={{ fontSize: 10, padding: '2px 7px', borderRadius: 4, background: configuredBg, color: configuredColor, fontWeight: 700 }}>
                {configuredLabel}
              </span>
              <span style={{ fontSize: 10, padding: '2px 7px', borderRadius: 4, background: '#e0f2fe', color: '#0369a1', fontWeight: 700 }}>
                {status.provider ?? 'gemma4'} · {status.executionMode ?? 'preview'}
              </span>
            </>
          )}
        </div>
      </div>

      {status?.summaryLines?.length > 0 && (
        <div style={{ marginTop: 8, display: 'flex', flexDirection: 'column', gap: 4 }}>
          {status.summaryLines.map((line, index) => (
            <div key={`${line}-${index}`} style={{ fontSize: 11, color: '#475569' }}>{line}</div>
          ))}
        </div>
      )}

      {loading && <div style={{ marginTop: 10, fontSize: 11, color: '#64748b' }}>Gemma4 미리보기를 생성 중입니다...</div>}
      {!loading && error && <div style={{ marginTop: 10, fontSize: 11, color: '#b91c1c' }}>{error}</div>}

      {!loading && preview && (
        <div style={{ marginTop: 10, display: 'grid', gap: 8 }}>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
            <span style={{ fontSize: 10, padding: '2px 7px', borderRadius: 4, background: '#eff6ff', color: '#1d4ed8', fontWeight: 700 }}>{preview.provider ?? 'gemma4'}</span>
            <span style={{ fontSize: 10, padding: '2px 7px', borderRadius: 4, background: '#eff6ff', color: '#1d4ed8', fontWeight: 700 }}>{preview.model ?? 'gemma4'}</span>
            <span style={{ fontSize: 10, padding: '2px 7px', borderRadius: 4, background: '#eff6ff', color: '#1d4ed8', fontWeight: 700 }}>{preview.executionMode ?? 'preview'}</span>
          </div>

          {preview.hints?.length > 0 ? (
            <div style={{ display: 'grid', gap: 6 }}>
              {preview.hints.map((hint, index) => (
                <div key={`${hint.title ?? 'hint'}-${index}`} style={{ border: '1px solid #dbeafe', borderRadius: 8, background: '#fff', padding: '10px 11px' }}>
                  <div style={{ fontSize: 11, fontWeight: 700, color: '#0f172a', marginBottom: 4 }}>{hint.title ?? `안내 ${index + 1}`}</div>
                  {hint.message && <div style={{ fontSize: 11, color: '#475569', lineHeight: 1.6, marginBottom: 4 }}>{hint.message}</div>}
                  {hint.suggestedKeywords?.length > 0 && <div style={{ fontSize: 10, color: '#64748b' }}>검색 키워드 · {hint.suggestedKeywords.join(' / ')}</div>}
                  {hint.linkedCardIds?.length > 0 && <div style={{ fontSize: 10, color: '#64748b', marginTop: 2 }}>연결 카드 · {hint.linkedCardIds.join(' / ')}</div>}
                </div>
              ))}
            </div>
          ) : (
            <div style={{ fontSize: 11, color: '#64748b' }}>현재 표시할 Gemma4 미리보기 힌트가 없습니다.</div>
          )}
        </div>
      )}
    </div>
  )
}

function ApplicableLawPanel({ catalog, triggers }) {
  if (!catalog && (!triggers || triggers.length === 0)) return null

  return (
    <div style={{ marginTop: 10, border: '1px solid #e2e8f0', borderRadius: 10, background: '#fff', padding: '12px 14px' }}>
      <div style={{ fontSize: 12, fontWeight: 800, color: '#0f172a', marginBottom: 6 }}>
        적용 가능 법규
      </div>
      <div style={{ fontSize: 11, color: '#64748b', lineHeight: 1.6 }}>
        토지와 입력 수치만으로 확인 가능한 법규를 우선 정리하고, 추가 입력 또는 수동 검토가 필요한 항목은 별도로 표시합니다.
      </div>

      {catalog?.summaryLines?.length > 0 && (
        <div style={{ marginTop: 10, display: 'grid', gap: 4 }}>
          {catalog.summaryLines.map((line, index) => (
            <div key={`${line}-${index}`} style={{ fontSize: 11, color: '#334155', lineHeight: 1.6 }}>
              • {line}
            </div>
          ))}
        </div>
      )}

      {catalog?.sections?.length > 0 && (
        <div style={{ marginTop: 10, display: 'grid', gap: 8 }}>
          {catalog.sections.map((section) => (
            <div key={section.sectionId} style={{ border: '1px solid #e2e8f0', borderRadius: 8, background: '#f8fafc', padding: '10px 11px' }}>
              <div style={{ fontSize: 11, fontWeight: 800, color: '#1e293b', marginBottom: 6 }}>
                {section.title}
              </div>
              <div style={{ display: 'grid', gap: 6 }}>
                {section.items.map((item) => (
                  <div key={item.itemId} style={{ border: '1px solid #e5e7eb', borderRadius: 7, background: '#fff', padding: '9px 10px' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, alignItems: 'flex-start' }}>
                      <div style={{ fontSize: 11, fontWeight: 700, color: '#0f172a', flex: 1 }}>{item.title}</div>
                      <span style={{ fontSize: 10, padding: '2px 7px', borderRadius: 4, background: '#eff6ff', color: '#1d4ed8', fontWeight: 700, whiteSpace: 'nowrap' }}>
                        {item.status || 'info'}
                      </span>
                    </div>
                    {item.summary && (
                      <div style={{ marginTop: 5, fontSize: 11, color: '#475569', lineHeight: 1.6 }}>
                        {item.summary}
                      </div>
                    )}
                    {(item.lawName || item.articleRef) && (
                      <div style={{ marginTop: 5, fontSize: 10, color: '#64748b' }}>
                        {[item.lawName, item.articleRef].filter(Boolean).join(' · ')}
                      </div>
                    )}
                    {item.requiredInputs?.length > 0 && (
                      <div style={{ marginTop: 5, fontSize: 10, color: '#64748b' }}>
                        필요 입력: {item.requiredInputs.join(' / ')}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}

      {triggers?.length > 0 && (
        <div style={{ marginTop: 10 }}>
          <div style={{ fontSize: 11, fontWeight: 800, color: '#7c2d12', marginBottom: 6 }}>추가 검토 트리거</div>
          <div style={{ display: 'grid', gap: 6 }}>
            {triggers.map((trigger) => (
              <div key={trigger.triggerId} style={{ border: '1px solid #fed7aa', borderRadius: 7, background: '#fff7ed', padding: '9px 10px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, alignItems: 'flex-start' }}>
                  <div style={{ fontSize: 11, fontWeight: 700, color: '#9a3412', flex: 1 }}>{trigger.title}</div>
                  <span style={{ fontSize: 10, color: '#9a3412', fontWeight: 700 }}>{trigger.status}</span>
                </div>
                <div style={{ marginTop: 4, fontSize: 11, color: '#7c2d12', lineHeight: 1.6 }}>{trigger.basis}</div>
                {trigger.requiredInputs?.length > 0 && (
                  <div style={{ marginTop: 4, fontSize: 10, color: '#9a3412' }}>
                    추가 입력: {trigger.requiredInputs.join(' / ')}
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

function AiAssistWidget({ status, preview, loading, error, query, onQueryChange, onRun, runLoading, runResult }) {
  const configuredLabel = status?.isConfigured ? '연결 준비 완료' : '미설정'
  const configuredColor = status?.isConfigured ? '#166534' : '#92400e'
  const configuredBg = status?.isConfigured ? '#f0fdf4' : '#fef3c7'
  let parsedRunOutput = null
  if (runResult?.structuredOutputJson) {
    try {
      parsedRunOutput = JSON.parse(runResult.structuredOutputJson)
    } catch {
      parsedRunOutput = null
    }
  }
  const fallbackResponseText = parsedRunOutput?.answer ?? parsedRunOutput?.responseText
  const relatedLaws = Array.isArray(parsedRunOutput?.relatedLaws) ? parsedRunOutput.relatedLaws : []
  const searchKeywords = Array.isArray(parsedRunOutput?.searchKeywords) ? parsedRunOutput.searchKeywords : []
  const manualReviewNeeded = Array.isArray(parsedRunOutput?.manualReviewNeeded) ? parsedRunOutput.manualReviewNeeded : []

  return (
    <div style={{ marginTop: 10, border: '1px solid #dbeafe', borderRadius: 10, background: '#f8fbff', padding: '12px 14px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
        <div>
          <div style={{ fontSize: 12, fontWeight: 800, color: '#1d4ed8', marginBottom: 4 }}>AI 보조 기능</div>
          <div style={{ fontSize: 11, color: '#64748b', lineHeight: 1.6 }}>
            법규 위치 안내, 조문 번호 안내, 검색 키워드 추천, 조례 탐색, 누락 검토 리마인드와 함께 질문형 법규 보조 응답을 제공합니다.
          </div>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4, alignItems: 'flex-end' }}>
          {status && (
            <>
              <span style={{ fontSize: 10, padding: '2px 7px', borderRadius: 4, background: configuredBg, color: configuredColor, fontWeight: 700 }}>
                {configuredLabel}
              </span>
              <span style={{ fontSize: 10, padding: '2px 7px', borderRadius: 4, background: '#e0f2fe', color: '#0369a1', fontWeight: 700 }}>
                {status.provider ?? 'gemma4'} · {status.executionMode ?? 'preview'}
              </span>
            </>
          )}
        </div>
      </div>

      {status?.summaryLines?.length > 0 && (
        <div style={{ marginTop: 8, display: 'flex', flexDirection: 'column', gap: 4 }}>
          {status.summaryLines.map((line, index) => (
            <div key={`${line}-${index}`} style={{ fontSize: 11, color: '#475569' }}>{line}</div>
          ))}
        </div>
      )}

      <div style={{ marginTop: 10, border: '1px solid #dbeafe', borderRadius: 8, background: '#fff', padding: '10px 11px' }}>
        <div style={{ fontSize: 11, fontWeight: 700, color: '#0f172a', marginBottom: 6 }}>법규 질문하기</div>
        <textarea
          value={query}
          onChange={e => onQueryChange?.(e.target.value)}
          placeholder="예: 이 대지에서 토지와 수치만으로 확인 가능한 법규를 정리해줘. 지구단위계획에서 우선 봐야 할 항목도 알려줘."
          style={{ width: '100%', minHeight: 88, boxSizing: 'border-box', resize: 'vertical', padding: '9px 10px', borderRadius: 8, border: '1px solid #bfdbfe', fontSize: 12, lineHeight: 1.6, outline: 'none' }}
        />
        <div style={{ marginTop: 8, display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8 }}>
          <div style={{ fontSize: 10, color: '#64748b', lineHeight: 1.5 }}>
            수치 계산이나 허용/불허 확정 판정이 아니라, 관련 법규 설명과 검색·검토 보조 용도로 답변합니다.
          </div>
          <button
            type="button"
            onClick={onRun}
            disabled={runLoading}
            style={{ flexShrink: 0, padding: '8px 12px', borderRadius: 7, border: 'none', background: runLoading ? '#93c5fd' : '#2563eb', color: '#fff', fontSize: 11, fontWeight: 700, cursor: runLoading ? 'default' : 'pointer' }}
          >
            {runLoading ? '질의 중...' : 'AI 답변 받기'}
          </button>
        </div>
      </div>

      {loading && <div style={{ marginTop: 10, fontSize: 11, color: '#64748b' }}>AI 보조 기능 미리보기를 생성 중입니다...</div>}
      {!loading && error && <div style={{ marginTop: 10, fontSize: 11, color: '#b91c1c' }}>{error}</div>}

      {!loading && preview && (
        <div style={{ marginTop: 10, display: 'grid', gap: 8 }}>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
            <span style={{ fontSize: 10, padding: '2px 7px', borderRadius: 4, background: '#eff6ff', color: '#1d4ed8', fontWeight: 700 }}>{preview.provider ?? 'gemma4'}</span>
            <span style={{ fontSize: 10, padding: '2px 7px', borderRadius: 4, background: '#eff6ff', color: '#1d4ed8', fontWeight: 700 }}>{preview.model ?? 'gemma4'}</span>
            <span style={{ fontSize: 10, padding: '2px 7px', borderRadius: 4, background: '#eff6ff', color: '#1d4ed8', fontWeight: 700 }}>{preview.executionMode ?? 'preview'}</span>
          </div>

          {preview.hints?.length > 0 ? (
            <div style={{ display: 'grid', gap: 6 }}>
              {preview.hints.map((hint, index) => (
                <div key={`${hint.title ?? 'hint'}-${index}`} style={{ border: '1px solid #dbeafe', borderRadius: 8, background: '#fff', padding: '10px 11px' }}>
                  <div style={{ fontSize: 11, fontWeight: 700, color: '#0f172a', marginBottom: 4 }}>{hint.title ?? `안내 ${index + 1}`}</div>
                  {hint.message && <div style={{ fontSize: 11, color: '#475569', lineHeight: 1.6, marginBottom: 4 }}>{hint.message}</div>}
                  {hint.suggestedKeywords?.length > 0 && <div style={{ fontSize: 10, color: '#64748b' }}>검색 키워드: {hint.suggestedKeywords.join(' / ')}</div>}
                  {hint.linkedCardIds?.length > 0 && <div style={{ fontSize: 10, color: '#64748b', marginTop: 2 }}>연결 카드: {hint.linkedCardIds.join(' / ')}</div>}
                </div>
              ))}
            </div>
          ) : (
            <div style={{ fontSize: 11, color: '#64748b' }}>현재 표시할 AI 보조 기능 미리보기 힌트가 없습니다.</div>
          )}
        </div>
      )}

      {runResult && (
        <div style={{ marginTop: 10, border: '1px solid #bfdbfe', borderRadius: 8, background: '#fff', padding: '10px 11px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, alignItems: 'flex-start' }}>
            <div style={{ fontSize: 11, fontWeight: 700, color: '#0f172a' }}>AI 응답</div>
            <span style={{ fontSize: 10, padding: '2px 7px', borderRadius: 4, background: runResult.success ? '#ecfdf5' : '#fef2f2', color: runResult.success ? '#166534' : '#991b1b', fontWeight: 700 }}>
              {runResult.success ? '응답 수신' : '응답 실패'}
            </span>
          </div>

          {!runResult.success && runResult.summaryLines?.length > 0 && (
            <div style={{ marginTop: 6, display: 'grid', gap: 4 }}>
              {runResult.summaryLines.map((line, index) => (
                <div key={`${line}-${index}`} style={{ fontSize: 11, color: '#334155', lineHeight: 1.6 }}>
                  • {line}
                </div>
              ))}
            </div>
          )}

          {fallbackResponseText && (
            <div style={{ marginTop: 8, padding: '10px 11px', borderRadius: 8, background: '#eff6ff', border: '1px solid #bfdbfe', fontSize: 12, color: '#1e293b', lineHeight: 1.7, whiteSpace: 'pre-wrap' }}>
              {fallbackResponseText}
            </div>
          )}

          {relatedLaws.length > 0 && (
            <div style={{ marginTop: 8 }}>
              <div style={{ fontSize: 11, fontWeight: 700, color: '#0f172a', marginBottom: 4 }}>관련 법령</div>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                {relatedLaws.map((law, index) => (
                  <span key={`${law}-${index}`} style={{ fontSize: 10, padding: '3px 7px', borderRadius: 999, background: '#f8fafc', border: '1px solid #cbd5e1', color: '#334155' }}>
                    {law}
                  </span>
                ))}
              </div>
            </div>
          )}

          {searchKeywords.length > 0 && (
            <div style={{ marginTop: 8 }}>
              <div style={{ fontSize: 11, fontWeight: 700, color: '#0f172a', marginBottom: 4 }}>추천 검색어</div>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                {searchKeywords.map((keyword, index) => (
                  <span key={`${keyword}-${index}`} style={{ fontSize: 10, padding: '3px 7px', borderRadius: 999, background: '#eef2ff', border: '1px solid #c7d2fe', color: '#4338ca' }}>
                    {keyword}
                  </span>
                ))}
              </div>
            </div>
          )}

          {manualReviewNeeded.length > 0 && (
            <div style={{ marginTop: 8 }}>
              <div style={{ fontSize: 11, fontWeight: 700, color: '#0f172a', marginBottom: 4 }}>추가 확인 필요</div>
              <div style={{ display: 'grid', gap: 4 }}>
                {manualReviewNeeded.map((item, index) => (
                  <div key={`${item}-${index}`} style={{ fontSize: 11, color: '#475569', lineHeight: 1.6 }}>
                    • {item}
                  </div>
                ))}
              </div>
            </div>
          )}

          {runResult.structuredOutputJson && !fallbackResponseText && (
            <pre style={{ marginTop: 8, padding: '10px', borderRadius: 8, background: '#0f172a', color: '#e2e8f0', fontSize: 10, lineHeight: 1.6, overflowX: 'auto', whiteSpace: 'pre-wrap' }}>
              {runResult.structuredOutputJson}
            </pre>
          )}

          {!runResult.structuredOutputJson && runResult.rawResponse && (
            <pre style={{ marginTop: 8, padding: '10px', borderRadius: 8, background: '#0f172a', color: '#e2e8f0', fontSize: 10, lineHeight: 1.6, overflowX: 'auto', whiteSpace: 'pre-wrap' }}>
              {runResult.rawResponse}
            </pre>
          )}
        </div>
      )}
    </div>
  )
}

function OfficialLawWidget({ status, query, onQueryChange, onSearch, searchResult, bodyResult, loading, error, onSelectItem }) {
  const configured = status?.officialLawApiConfigured
  const configuredLabel = configured ? '공식 법령 검색 가능' : '법제처 OC 필요'
  const configuredColor = configured ? '#166534' : '#92400e'
  const configuredBg = configured ? '#f0fdf4' : '#fef3c7'

  return (
    <div style={{ marginTop: 10, border: '1px solid #dbeafe', borderRadius: 10, background: '#f8fbff', padding: '12px 14px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
        <div>
          <div style={{ fontSize: 12, fontWeight: 800, color: '#1d4ed8', marginBottom: 4 }}>공식 법령 검색</div>
          <div style={{ fontSize: 11, color: '#64748b', lineHeight: 1.6 }}>
            법제처 API가 연결되면 선택한 용도와 지역 기준으로 관련 법령 제목과 본문을 직접 확인할 수 있습니다.
          </div>
        </div>
        <span style={{ fontSize: 10, padding: '2px 7px', borderRadius: 4, background: configuredBg, color: configuredColor, fontWeight: 700 }}>
          {configuredLabel}
        </span>
      </div>

      {status?.summaryLines?.length > 0 && (
        <div style={{ marginTop: 8, display: 'grid', gap: 4 }}>
          {status.summaryLines.map((line, index) => (
            <div key={`${line}-${index}`} style={{ fontSize: 11, color: '#475569' }}>{line}</div>
          ))}
        </div>
      )}

      <div style={{ marginTop: 10, border: '1px solid #dbeafe', borderRadius: 8, background: '#fff', padding: '10px 11px' }}>
        <div style={{ fontSize: 11, fontWeight: 700, color: '#0f172a', marginBottom: 6 }}>법령 키워드 검색</div>
        <div style={{ display: 'flex', gap: 8 }}>
          <input
            value={query}
            onChange={e => onQueryChange?.(e.target.value)}
            placeholder="예: 제3종일반주거지역 제1종근린생활시설 건축법"
            style={{ ...inputStyle, flex: 1 }}
          />
          <button
            type="button"
            onClick={onSearch}
            disabled={loading}
            style={{ flexShrink: 0, padding: '8px 12px', borderRadius: 7, border: 'none', background: loading ? '#93c5fd' : '#2563eb', color: '#fff', fontSize: 11, fontWeight: 700, cursor: loading ? 'default' : 'pointer' }}
          >
            {loading ? '검색 중..' : '검색'}
          </button>
        </div>
        {!configured && (
          <div style={{ marginTop: 8, fontSize: 10, color: '#92400e', lineHeight: 1.5 }}>
            현재는 법제처 DRF용 OC 값이 없어 검색 API가 실제로 동작하지 않을 수 있습니다. 구조는 연결되어 있습니다.
          </div>
        )}
      </div>

      {error && <div style={{ marginTop: 10, fontSize: 11, color: '#b91c1c' }}>{error}</div>}

      {searchResult?.items?.length > 0 && (
        <div style={{ marginTop: 10, display: 'grid', gap: 6 }}>
          {searchResult.items.map((item) => (
            <div key={`${item.target}-${item.id}-${item.mst ?? 'no-mst'}`} style={{ border: '1px solid #dbeafe', borderRadius: 8, background: '#fff', padding: '10px 11px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, alignItems: 'flex-start' }}>
                <div style={{ flex: 1 }}>
                  <div style={{ fontSize: 11, fontWeight: 700, color: '#0f172a' }}>{item.title}</div>
                  {item.summary && <div style={{ marginTop: 4, fontSize: 11, color: '#475569', lineHeight: 1.6 }}>{item.summary}</div>}
                  <div style={{ marginTop: 4, fontSize: 10, color: '#64748b' }}>
                    {[item.department, item.promulgationDate].filter(Boolean).join(' / ')}
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => onSelectItem?.(item)}
                  disabled={loading}
                  style={{ flexShrink: 0, padding: '6px 10px', borderRadius: 6, border: '1px solid #bfdbfe', background: '#eff6ff', color: '#1d4ed8', fontSize: 10, fontWeight: 700, cursor: loading ? 'default' : 'pointer' }}
                >
                  본문 보기
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {bodyResult?.rawBody && (
        <div style={{ marginTop: 10, border: '1px solid #bfdbfe', borderRadius: 8, background: '#fff', padding: '10px 11px' }}>
          <div style={{ fontSize: 11, fontWeight: 700, color: '#0f172a', marginBottom: 6 }}>
            {bodyResult.title ?? '법령 본문'}
          </div>
          {bodyResult.link && (
            <a href={bodyResult.link} target="_blank" rel="noreferrer" style={{ display: 'inline-block', marginBottom: 8, fontSize: 10, color: '#2563eb' }}>
              법제처 원문 열기
            </a>
          )}
          <pre style={{ margin: 0, padding: '10px', borderRadius: 8, background: '#0f172a', color: '#e2e8f0', fontSize: 10, lineHeight: 1.6, overflowX: 'auto', whiteSpace: 'pre-wrap' }}>
            {bodyResult.rawBody}
          </pre>
        </div>
      )}
    </div>
  )
}

const inputStyle = {
  width: '100%',
  boxSizing: 'border-box',
  padding: '5px 8px',
  borderRadius: 6,
  border: '1px solid #c4b5fd',
  fontSize: 12,
  background: '#fff',
  outline: 'none',
}

function parseInputs(raw) {
  const out = {}
  const numberFields = [
    'floorArea',
    'floorCount',
    'siteArea',
    'buildingArea',
    'roadFrontageWidth',
    'unitCount',
    'unitArea',
    'buildingHeight',
    'detailUseFloorArea',
    'occupantCount',
    'mixedUseRatio',
    'roomCount',
    'guestRoomCount',
    'bedCount',
    'studentCount',
  ]
  const booleanFields = [
    'isMultipleOccupancy',
    'isHighRiskOccupancy',
    'hasDisabilityUsers',
    'hasPublicSpace',
    'hasLoadingBay',
    'hasDistrictUnitPlanDocument',
    'hasDevActRestrictionConsult',
  ]
  const integerFields = ['floorCount', 'unitCount', 'occupantCount', 'roomCount', 'guestRoomCount', 'bedCount', 'studentCount']

  for (const [key, value] of Object.entries(raw)) {
    if (value === '' || value == null) continue
    if (booleanFields.includes(key)) {
      out[key] = value === 'true'
      continue
    }
    if (numberFields.includes(key)) {
      const parsed = Number(value)
      if (!Number.isFinite(parsed)) continue
      if (key === 'mixedUseRatio') {
        out[key] = parsed / 100
        continue
      }
      out[key] = integerFields.includes(key) ? Math.round(parsed) : parsed
      continue
    }
    out[key] = value
  }

  return out
}
