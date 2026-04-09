import React, { useCallback, useRef, useState } from 'react'
import RegulationMapView from '../components/RegulationMapView'
import AddressSearchBox from '../components/AddressSearchBox'
import ResultSummaryPanel from '../components/ResultSummaryPanel'

const toNumber = (v) => { const n = Number(v); return Number.isFinite(n) ? n : null }
const toCoord  = (s) => {
  const lat = toNumber(s?.lat ?? s?.latitude)
  const lon = toNumber(s?.lon ?? s?.longitude)
  return (lat !== null && lon !== null) ? { lat, lon } : null
}
const normalizeResult = (raw) => ({
  ...raw,
  displayName: raw?.displayName ?? '결과 없음',
  summaryText: raw?.summaryText ?? '요약 정보가 없습니다.',
  overlayFlags: {
    hasAnyRestriction:             !!raw?.overlayFlags?.hasAnyRestriction,
    isInDistrictUnitPlan:          !!raw?.overlayFlags?.isInDistrictUnitPlan,
    isDevelopmentRestricted:       !!raw?.overlayFlags?.isDevelopmentRestricted,
    isDevelopmentActionRestricted: !!raw?.overlayFlags?.isDevelopmentActionRestricted,
    ...raw?.overlayFlags,
  },
})

const S = {
  page:    { minHeight: '100vh', height: '100vh', background: '#f8fafc', padding: 12, boxSizing: 'border-box', overflow: 'hidden' },
  layout:  { display: 'flex', flexDirection: 'row', gap: 12, height: '100%' },
  mapWrap: { position: 'relative', flex: '0 0 62%', borderRadius: 12, border: '1px solid #e2e8f0', background: '#fff', overflow: 'hidden' },
  mapInner:{ width: '100%', height: '100%' },
  search:  { position: 'absolute', left: 12, top: 12, zIndex: 20, width: 'min(92%, 360px)' },
  panel:   { flex: '0 0 38%', borderRadius: 12, border: '1px solid #e2e8f0', background: '#fff', overflow: 'hidden' },
  scroll:  { height: '100%', overflowY: 'auto', padding: 16, boxSizing: 'border-box' },
}

export default function RegulationMapPage() {
  const [selectedCoordinate, setSelectedCoordinate] = useState(null)
  const [result, setResult]                         = useState(null)
  const [loading, setLoading]                       = useState(false)
  const [error, setError]                           = useState(null)
  const mapRef      = useRef(null)
  const inFlightRef = useRef(null)
  const seqRef      = useRef(0)

  const handleMapClick = useCallback(async (lat, lon) => {
    if (loading) return
    const coord = toCoord({ lat, lon })
    if (!coord) { setError('좌표 정보를 찾을 수 없습니다.'); return }

    inFlightRef.current?.abort()
    const abort = new AbortController()
    inFlightRef.current = abort
    const id = ++seqRef.current

    setSelectedCoordinate(coord)
    setLoading(true); setError(null); setResult(null)

    try {
      const res = await fetch(`${import.meta.env.VITE_API_BASE_URL ?? ''}/api/regulation-check/coordinate`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'ngrok-skip-browser-warning': '1',
        },
        body: JSON.stringify({ latitude: coord.lat, longitude: coord.lon }),
        signal: abort.signal,
      })
      if (abort.signal.aborted || id !== seqRef.current) return
      if (!res.ok) throw new Error('coordinate-api-failed')

      const raw = await res.json()
      if (abort.signal.aborted || id !== seqRef.current) return

      const safe = normalizeResult(raw)
      setResult(safe)

      // 백엔드는 result.coordinate가 아닌 result.input에 좌표를 반환
      const resultCoord = toCoord(raw?.input)
      if (resultCoord) setSelectedCoordinate(resultCoord)
    } catch (e) {
      if (e?.name === 'AbortError' || id !== seqRef.current) return
      setError('법규 조회에 실패했습니다.')
      setResult(null)
    } finally {
      if (id === seqRef.current) setLoading(false)
    }
  }, [loading])

  const handleAddressSelect = useCallback((lat, lon, preloadedResult) => {
    const coord = toCoord({ lat, lon })
    if (!coord) { setError('좌표 정보를 찾을 수 없습니다.'); return }
    mapRef.current?.setView([coord.lat, coord.lon], 16)

    if (preloadedResult) {
      // 단일 후보: POST /address 응답의 regulationResult를 직접 사용 (/coordinate 재호출 불필요)
      setSelectedCoordinate(coord)
      setResult(normalizeResult(preloadedResult))
      setError(null)
      setLoading(false)
    } else {
      // 복수 후보 수동 선택: /coordinate 호출
      handleMapClick(coord.lat, coord.lon)
    }
  }, [handleMapClick])

  return (
    <div style={S.page}>
      <div className="rmp-layout" style={S.layout}>
        <section className="rmp-map" style={S.mapWrap}>
          <div style={S.search}>
            <AddressSearchBox onCandidateSelect={handleAddressSelect} disabled={loading} />
          </div>
          <div style={S.mapInner}>
            <RegulationMapView
              selectedCoordinate={selectedCoordinate}
              result={result}
              loading={loading}
              onMapClick={handleMapClick}
              mapRef={mapRef}
            />
          </div>
        </section>
        <aside className="rmp-panel" style={S.panel}>
          <div style={S.scroll}>
            <ResultSummaryPanel
              selectedCoordinate={selectedCoordinate}
              result={result}
              loading={loading}
              error={error}
            />
          </div>
        </aside>
      </div>
    </div>
  )
}
