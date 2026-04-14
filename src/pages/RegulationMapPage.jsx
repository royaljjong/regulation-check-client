import React, { useCallback, useRef, useState } from 'react'
import RegulationMapView    from '../components/RegulationMapView'
import AddressSearchBox     from '../components/AddressSearchBox'
import ResultSummaryPanel   from '../components/ResultSummaryPanel'
import RecentHistoryPanel   from '../components/RecentHistoryPanel'
import { useRecentHistory, makeDedupKey } from '../hooks/useRecentHistory'

const toNumber = (v) => { const n = Number(v); return Number.isFinite(n) ? n : null }
const toCoord  = (s) => {
  const lat = toNumber(s?.lat ?? s?.latitude)
  const lon = toNumber(s?.lon ?? s?.longitude)
  return (lat !== null && lon !== null) ? { lat, lon } : null
}
const normalizeResult = (raw) => ({
  ...raw,
  displayName: (raw?.displayName && raw.displayName.trim() !== '') ? raw.displayName : null,
  summaryText: raw?.summaryText ?? '요약 정보가 없습니다.',
  overlayFlags: {
    hasAnyRestriction:             !!raw?.overlayFlags?.hasAnyRestriction,
    isInDistrictUnitPlan:          !!raw?.overlayFlags?.isInDistrictUnitPlan,
    isDevelopmentRestricted:       !!raw?.overlayFlags?.isDevelopmentRestricted,
    isDevelopmentActionRestricted: !!raw?.overlayFlags?.isDevelopmentActionRestricted,
    ...raw?.overlayFlags,
  },
})

// ── 스타일 ──────────────────────────────────────────────────────────────────
const S = {
  page: {
    height: '100vh', background: '#f8fafc',
    padding: '10px 12px 12px',
    boxSizing: 'border-box',
    display: 'flex', flexDirection: 'column', gap: 10,
  },
  header: {
    flexShrink: 0,
    position: 'relative',
    zIndex: 30,
  },
  layout: {
    display: 'flex', flexDirection: 'row', gap: 12,
    flex: 1, minHeight: 0,
  },
  mapWrap: {
    position: 'relative', flex: '0 0 62%',
    borderRadius: 12, border: '1px solid #e2e8f0',
    background: '#fff', overflow: 'hidden',
  },
  mapInner: { width: '100%', height: '100%' },
  panel: {
    flex: '0 0 38%', borderRadius: 12,
    border: '1px solid #e2e8f0', background: '#fff', overflow: 'hidden',
  },
  scroll: { height: '100%', overflowY: 'auto', padding: 16, boxSizing: 'border-box' },
}

export default function RegulationMapPage() {
  const [searchText,         setSearchText]         = useState('')
  const [selectedCoordinate, setSelectedCoordinate] = useState(null)
  const [lookupInfo,         setLookupInfo]         = useState({ mode: 'none', label: '' })
  const [result,             setResult]             = useState(null)
  // selectedUse는 이력 복원을 위해 페이지 레벨에서 관리
  const [selectedUse,        setSelectedUse]        = useState(null)
  const [loading,            setLoading]            = useState(false)
  const [error,              setError]              = useState(null)

  const mapRef      = useRef(null)
  const inFlightRef = useRef(null)
  const seqRef      = useRef(0)

  // ── 최근 이력 훅 ──────────────────────────────────────────────────────────
  const { history, saveEntry, patchEntry, clearHistory } = useRecentHistory()

  // ─────────────────────────────────────────────────────────────────────────
  // 지도 클릭 → 좌표 기반 조회
  // ─────────────────────────────────────────────────────────────────────────
  const handleMapClick = useCallback(async (lat, lon) => {
    if (loading) return
    const coord = toCoord({ lat, lon })
    if (!coord) { setError('좌표 정보를 찾을 수 없습니다.'); return }

    inFlightRef.current?.abort()
    const abort = new AbortController()
    inFlightRef.current = abort
    const id = ++seqRef.current

    setSelectedCoordinate(coord)
    setLookupInfo({ mode: 'coordinate', label: '좌표 기준 조회' })
    setSelectedUse(null)   // 새 조회 → 용도 초기화
    setLoading(true); setError(null); setResult(null)

    try {
      const res = await fetch(
        `${import.meta.env.VITE_API_BASE_URL ?? ''}/api/regulation-check/coordinate`,
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'ngrok-skip-browser-warning': '1' },
          body: JSON.stringify({ latitude: coord.lat, longitude: coord.lon }),
          signal: abort.signal,
        }
      )
      if (abort.signal.aborted || id !== seqRef.current) return
      if (!res.ok) throw new Error('coordinate-api-failed')

      const raw = await res.json()
      if (abort.signal.aborted || id !== seqRef.current) return

      const normalized = normalizeResult(raw)
      setResult(normalized)

      const resultCoord = toCoord(raw?.input) ?? coord
      setSelectedCoordinate(resultCoord)

      // 이력 저장
      saveEntry({
        mode:       'coordinate',
        label:      '좌표 기준 조회',
        coordinate: resultCoord,
        result:     normalized,
        lookupInfo: { mode: 'coordinate', label: '좌표 기준 조회' },
        selectedUse: null,
      })
    } catch (e) {
      if (e?.name === 'AbortError' || id !== seqRef.current) return
      setError('법규 조회에 실패했습니다.')
      setResult(null)
    } finally {
      if (id === seqRef.current) setLoading(false)
    }
  }, [loading, saveEntry])

  // ─────────────────────────────────────────────────────────────────────────
  // 주소 검색 후보 선택 → 지도 이동 + 결과 표시
  // ─────────────────────────────────────────────────────────────────────────
  const handleAddressSelect = useCallback((lat, lon, preloadedResult, address) => {
    const coord = toCoord({ lat, lon })
    if (!coord) { setError('좌표 정보를 찾을 수 없습니다.'); return }

    if (address) setSearchText(address)
    mapRef.current?.setView([coord.lat, coord.lon], 16)

    const info = { mode: 'address', label: address || '주소 기준 조회' }
    setSelectedCoordinate(coord)
    setLookupInfo(info)
    setSelectedUse(null)   // 새 조회 → 용도 초기화

    if (preloadedResult) {
      const normalized = normalizeResult(preloadedResult)
      setResult(normalized)
      setError(null)
      setLoading(false)

      // 이력 저장
      saveEntry({
        mode:       'address',
        label:      address || '주소 기준 조회',
        coordinate: coord,
        result:     normalized,
        lookupInfo: info,
        selectedUse: null,
      })
    } else {
      setLoading(true); setError(null); setResult(null)
      handleMapClick(coord.lat, coord.lon)
    }
  }, [handleMapClick, saveEntry])

  // ─────────────────────────────────────────────────────────────────────────
  // 용도 선택 (ResultSummaryPanel 콜백)
  //   - selectedUse 상태 갱신
  //   - 이력 항목 selectedUse 패치 (목록 순서/savedAt 변경 없음)
  // ─────────────────────────────────────────────────────────────────────────
  const handleUseSelect = useCallback((use) => {
    setSelectedUse(use)
    if (lookupInfo && selectedCoordinate) {
      const key = makeDedupKey(lookupInfo.mode, lookupInfo.label, selectedCoordinate)
      patchEntry(key, { selectedUse: use })
    }
  }, [lookupInfo, selectedCoordinate, patchEntry])

  // ─────────────────────────────────────────────────────────────────────────
  // 이력 복원 — 지도 이동 + 상태 복원 + selectedUse 복원
  // ─────────────────────────────────────────────────────────────────────────
  const handleRestoreFromHistory = useCallback((entry) => {
    setSelectedCoordinate(entry.coordinate)
    setLookupInfo(entry.lookupInfo)
    setResult(entry.result)
    setSelectedUse(entry.selectedUse ?? null)
    setError(null)
    setLoading(false)
    mapRef.current?.setView([entry.coordinate.lat, entry.coordinate.lon], 16)
  }, [])

  return (
    <div style={S.page}>

      {/* ── 상단 주소 검색 헤더 ── */}
      <div style={S.header}>
        {/* 서비스 핵심 문구 — 결과 없을 때만 표시 */}
        {!result && !loading && (
          <div style={{
            marginBottom: 8,
            display: 'flex', alignItems: 'baseline', gap: 10, flexWrap: 'wrap',
          }}>
            <span style={{ fontSize: 18, fontWeight: 800, color: '#0f172a', letterSpacing: '-0.03em' }}>
              이 땅에 무엇을 지을 수 있나요?
            </span>
            <span style={{ fontSize: 12, color: '#94a3b8', fontWeight: 500 }}>
              주소 검색 → 용도 선택 → Quick 판정
            </span>
          </div>
        )}
        <AddressSearchBox
          searchText={searchText}
          onSearchTextChange={setSearchText}
          onCandidateSelect={handleAddressSelect}
          disabled={loading}
        />
      </div>

      {/* ── 지도 + 결과 패널 ── */}
      <div className="rmp-layout" style={S.layout}>

        <section className="rmp-map" style={S.mapWrap}>
          <div style={S.mapInner}>
            <RegulationMapView
              selectedCoordinate={selectedCoordinate}
              result={result}
              loading={loading}
              lookupMode={lookupInfo.mode}
              onMapClick={handleMapClick}
              mapRef={mapRef}
            />
          </div>
        </section>

        <aside className="rmp-panel" style={S.panel}>
          <div style={S.scroll}>

            {/* 최근 조회 이력 패널 */}
            <RecentHistoryPanel
              history={history}
              onRestore={handleRestoreFromHistory}
              onClear={clearHistory}
            />

            {/* 법규 검토 결과 패널 */}
            <ResultSummaryPanel
              selectedCoordinate={selectedCoordinate}
              lookupInfo={lookupInfo}
              result={result}
              loading={loading}
              error={error}
              selectedUse={selectedUse}
              onUseSelect={handleUseSelect}
            />
          </div>
        </aside>

      </div>
    </div>
  )
}
