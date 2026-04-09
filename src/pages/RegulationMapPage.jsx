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
  const [searchText, setSearchText]                 = useState('')
  const [selectedCoordinate, setSelectedCoordinate] = useState(null)
  const [lookupInfo, setLookupInfo]                 = useState({ mode: 'none', label: '' })
  const [result, setResult]                         = useState(null)
  const [savedResults, setSavedResults]             = useState([])
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
    setLookupInfo({ mode: 'coordinate', label: '좌표 기준 조회' })
    setLoading(true); setError(null); setResult(null)
    // Map click must NOT modify searchText

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

  // ── 세션 저장 핸들러 ────────────────────────────────────────────────
  const handleSaveResult = useCallback(() => {
    if (!result || loading) return;
    const newItem = {
      id: Date.now(),
      label: lookupInfo.label || '선택 지점',
      coordinate: selectedCoordinate,
      lookupInfo: { ...lookupInfo },
      result: { ...result },
      timestamp: new Date().toLocaleTimeString()
    };
    setSavedResults(prev => [newItem, ...prev].slice(0, 5)); // 최대 5건 유지
  }, [result, lookupInfo, selectedCoordinate, loading]);

  const handleRestoreResult = useCallback((item) => {
    setSelectedCoordinate(item.coordinate);
    setLookupInfo(item.lookupInfo);
    setResult(item.result);
    mapRef.current?.setView([item.coordinate.lat, item.coordinate.lon], 16);
  }, []);

  const handleDeleteSaved = useCallback((id) => {
    setSavedResults(prev => prev.filter(it => it.id !== id));
  }, []);

  const handleAddressSelect = useCallback((lat, lon, preloadedResult, address) => {
    const coord = toCoord({ lat, lon })
    if (!coord) { setError('좌표 정보를 찾을 수 없습니다.'); return }
    
    // Update searchText only when an address is explicitly selected
    if (address) setSearchText(address)
    mapRef.current?.setView([coord.lat, coord.lon], 16)

    setSelectedCoordinate(coord)
    setLookupInfo({ mode: 'address', label: address || '주소 기준 조회' })

    if (preloadedResult) {
      setResult(normalizeResult(preloadedResult))
      setError(null)
      setLoading(false)
    } else {
      setLoading(true); setError(null); setResult(null)
      handleMapClick(coord.lat, coord.lon)
    }
  }, [handleMapClick])

  return (
    <div style={S.page}>
      <div className="rmp-layout" style={S.layout}>
        <section className="rmp-map" style={S.mapWrap}>
          <div style={S.search}>
            <AddressSearchBox 
              searchText={searchText}
              onSearchTextChange={setSearchText}
              onCandidateSelect={handleAddressSelect} 
              disabled={loading} 
            />
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
              lookupInfo={lookupInfo}
              result={result}
              loading={loading}
              error={error}
              savedResults={savedResults}
              onSave={handleSaveResult}
              onRestore={handleRestoreResult}
              onDeleteSaved={handleDeleteSaved}
            />
          </div>
        </aside>
      </div>
    </div>
  )
}
