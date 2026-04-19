import React, { useCallback, useRef, useState } from 'react'
import RegulationMapView from '../components/RegulationMapView'
import AddressSearchBox from '../components/AddressSearchBox'
import BackendConnectionPanel from '../components/BackendConnectionPanel'
import ResultSummaryPanel from '../components/ResultSummaryPanel'
import RecentHistoryPanel from '../components/RecentHistoryPanel'
import { useRecentHistory, makeDedupKey } from '../hooks/useRecentHistory'
import { fetchApi } from '../utils/apiClient'

const toNumber = (value) => {
  const parsed = Number(value)
  return Number.isFinite(parsed) ? parsed : null
}

const toCoord = (source) => {
  const lat = toNumber(source?.lat ?? source?.latitude)
  const lon = toNumber(source?.lon ?? source?.longitude)
  return lat !== null && lon !== null ? { lat, lon } : null
}

const normalizeResult = (raw) => ({
  ...raw,
  displayName: raw?.displayName?.trim() ? raw.displayName : null,
  summaryText: raw?.summaryText ?? '\uC694\uC57D \uC815\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.',
  mapGeometries: raw?.mapGeometries ?? null,
  overlayFlags: {
    hasAnyRestriction: !!raw?.overlayFlags?.hasAnyRestriction,
    isInDistrictUnitPlan: !!raw?.overlayFlags?.isInDistrictUnitPlan,
    isDevelopmentRestricted: !!raw?.overlayFlags?.isDevelopmentRestricted,
    isDevelopmentActionRestricted: !!raw?.overlayFlags?.isDevelopmentActionRestricted,
    ...raw?.overlayFlags,
  },
})

const S = {
  page: {
    height: '100vh',
    background: '#f8fafc',
    padding: '10px 12px 12px',
    boxSizing: 'border-box',
    display: 'flex',
    flexDirection: 'column',
    gap: 10,
  },
  header: {
    flexShrink: 0,
    position: 'relative',
    zIndex: 30,
    display: 'flex',
    flexDirection: 'column',
    gap: 10,
  },
  layout: {
    display: 'flex',
    flexDirection: 'row',
    gap: 12,
    flex: 1,
    minHeight: 0,
  },
  mapWrap: {
    position: 'relative',
    flex: '0 0 62%',
    borderRadius: 12,
    border: '1px solid #e2e8f0',
    background: '#fff',
    overflow: 'hidden',
  },
  mapInner: {
    width: '100%',
    height: '100%',
  },
  panel: {
    flex: '0 0 38%',
    borderRadius: 12,
    border: '1px solid #e2e8f0',
    background: '#fff',
    overflow: 'hidden',
  },
  scroll: {
    height: '100%',
    overflowY: 'auto',
    padding: 16,
    boxSizing: 'border-box',
  },
  geometryCard: {
    marginBottom: 16,
    border: '1px solid #e2e8f0',
    borderRadius: 12,
    background: '#f8fafc',
    padding: 14,
  },
  geometryTitle: {
    fontSize: 14,
    fontWeight: 800,
    color: '#0f172a',
    marginBottom: 6,
  },
  geometryDesc: {
    fontSize: 12,
    color: '#475569',
    lineHeight: 1.5,
    marginBottom: 12,
  },
  groupWrap: {
    display: 'flex',
    flexWrap: 'wrap',
    gap: 8,
    marginBottom: 12,
  },
  groupBadge: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: 6,
    padding: '6px 10px',
    borderRadius: 999,
    background: '#e2e8f0',
    color: '#1e293b',
    fontSize: 12,
    fontWeight: 700,
  },
  itemList: {
    display: 'flex',
    flexDirection: 'column',
    gap: 8,
  },
  itemRow: {
    display: 'flex',
    flexDirection: 'column',
    gap: 3,
    paddingBottom: 8,
    borderBottom: '1px solid #e2e8f0',
  },
  itemLabel: {
    fontSize: 13,
    fontWeight: 700,
    color: '#0f172a',
  },
  itemMeta: {
    fontSize: 12,
    color: '#64748b',
  },
}

function MatchedGeometrySummary({ mapGeometries }) {
  const groups = Array.isArray(mapGeometries?.legendGroups) ? mapGeometries.legendGroups : []
  const items = Array.isArray(mapGeometries?.matchedItems) ? mapGeometries.matchedItems : []

  if (!groups.length || !items.length) {
    return null
  }

  return (
    <section style={S.geometryCard}>
      <div style={S.geometryTitle}>{'\uC9C0\uB3C4 \uB9E4\uCE6D \uC2DC\uC124 \uC694\uC57D'}</div>
      <div style={S.geometryDesc}>
        {'\uD604\uC7AC \uC88C\uD45C\uC5D0\uC11C \uACB9\uCE5C \uC6A9\uB3C4\uC9C0\uC5ED, \uADDC\uC81C\uAD6C\uC5ED, \uB3C4\uC2DC\uACC4\uD68D\uC2DC\uC124\uC744 \uADF8\uB8F9\uBCC4\uB85C \uC815\uB9AC\uD588\uC2B5\uB2C8\uB2E4.'}
      </div>

      <div style={S.groupWrap}>
        {groups.map((group) => (
          <div key={group.key} style={S.groupBadge}>
            <span>{group.label}</span>
            <span>{group.itemCount}{'\uAC74'}</span>
          </div>
        ))}
      </div>

      <div style={S.itemList}>
        {items.map((item) => (
          <div key={item.key} style={S.itemRow}>
            <div style={S.itemLabel}>
              {item.label}
              {item.geometryType === 'line' && (
                <span style={{ marginLeft: 6, fontSize: 11, color: '#64748b' }}>{'\uC120\uD615'}</span>
              )}
            </div>
            <div style={S.itemMeta}>{item.legendGroupLabel}</div>
          </div>
        ))}
      </div>
    </section>
  )
}

export default function RegulationMapPage() {
  const [searchText, setSearchText] = useState('')
  const [selectedCoordinate, setSelectedCoordinate] = useState(null)
  const [lookupInfo, setLookupInfo] = useState({ mode: 'none', label: '' })
  const [result, setResult] = useState(null)
  const [selectedUse, setSelectedUse] = useState(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)

  const mapRef = useRef(null)
  const inFlightRef = useRef(null)
  const seqRef = useRef(0)

  const { history, saveEntry, patchEntry, clearHistory } = useRecentHistory()

  const fetchMapGeometries = useCallback(async (coord, signal) => {
    const response = await fetchApi('/api/regulation-check/map-geometries', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ latitude: coord.lat, longitude: coord.lon }),
      signal,
    })

    if (!response.ok) {
      throw new Error('map-geometries-api-failed')
    }

    return response.json()
  }, [])

  const handleMapClick = useCallback(async (lat, lon) => {
    if (loading) return

    const coord = toCoord({ lat, lon })
    if (!coord) {
      setError('\uC88C\uD45C \uC815\uBCF4\uB97C \uC77D\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.')
      return
    }

    inFlightRef.current?.abort()
    const abort = new AbortController()
    inFlightRef.current = abort
    const requestId = ++seqRef.current

    setSelectedCoordinate(coord)
    setLookupInfo({ mode: 'coordinate', label: '\uC88C\uD45C \uAE30\uC900 \uC870\uD68C' })
    setSelectedUse(null)
    setLoading(true)
    setError(null)
    setResult(null)

    try {
      const response = await fetchApi('/api/regulation-check/coordinate', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ latitude: coord.lat, longitude: coord.lon }),
        signal: abort.signal,
      })

      if (abort.signal.aborted || requestId !== seqRef.current) return
      if (!response.ok) throw new Error('coordinate-api-failed')

      const raw = await response.json()
      if (abort.signal.aborted || requestId !== seqRef.current) return

      let mapGeometries = null
      try {
        mapGeometries = await fetchMapGeometries(coord, abort.signal)
      } catch {
        mapGeometries = null
      }

      const normalized = normalizeResult({ ...raw, mapGeometries })
      const resultCoord = toCoord(raw?.input) ?? coord

      setResult(normalized)
      setSelectedCoordinate(resultCoord)

      saveEntry({
        mode: 'coordinate',
        label: '\uC88C\uD45C \uAE30\uC900 \uC870\uD68C',
        coordinate: resultCoord,
        result: normalized,
        lookupInfo: { mode: 'coordinate', label: '\uC88C\uD45C \uAE30\uC900 \uC870\uD68C' },
        selectedUse: null,
      })
    } catch (fetchError) {
      if (fetchError?.name === 'AbortError' || requestId !== seqRef.current) return
      setError('\uBC95\uADDC \uC870\uD68C\uC5D0 \uC2E4\uD328\uD588\uC2B5\uB2C8\uB2E4.')
      setResult(null)
    } finally {
      if (requestId === seqRef.current) {
        setLoading(false)
      }
    }
  }, [fetchMapGeometries, loading, saveEntry])

  const handleAddressSelect = useCallback((lat, lon, preloadedResult, address) => {
    const coord = toCoord({ lat, lon })
    if (!coord) {
      setError('\uC88C\uD45C \uC815\uBCF4\uB97C \uC77D\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.')
      return
    }

    if (address) setSearchText(address)
    mapRef.current?.setView([coord.lat, coord.lon], 16)

    const info = { mode: 'address', label: address || '\uC8FC\uC18C \uAE30\uC900 \uC870\uD68C' }
    setSelectedCoordinate(coord)
    setLookupInfo(info)
    setSelectedUse(null)

    if (!preloadedResult) {
      setLoading(true)
      setError(null)
      setResult(null)
      handleMapClick(coord.lat, coord.lon)
      return
    }

    setLoading(true)
    setError(null)
    setResult(null)

    fetchMapGeometries(coord)
      .then((mapGeometries) => {
        const normalized = normalizeResult({ ...preloadedResult, mapGeometries })
        setResult(normalized)
        setError(null)
        setLoading(false)

        saveEntry({
          mode: 'address',
          label: address || '\uC8FC\uC18C \uAE30\uC900 \uC870\uD68C',
          coordinate: coord,
          result: normalized,
          lookupInfo: info,
          selectedUse: null,
        })
      })
      .catch(() => {
        const normalized = normalizeResult(preloadedResult)
        setResult(normalized)
        setError(null)
        setLoading(false)

        saveEntry({
          mode: 'address',
          label: address || '\uC8FC\uC18C \uAE30\uC900 \uC870\uD68C',
          coordinate: coord,
          result: normalized,
          lookupInfo: info,
          selectedUse: null,
        })
      })
  }, [fetchMapGeometries, handleMapClick, saveEntry])

  const handleUseSelect = useCallback((use) => {
    setSelectedUse(use)
    if (lookupInfo && selectedCoordinate) {
      const key = makeDedupKey(lookupInfo.mode, lookupInfo.label, selectedCoordinate)
      patchEntry(key, { selectedUse: use })
    }
  }, [lookupInfo, patchEntry, selectedCoordinate])

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
      <div style={S.header}>
        {!result && !loading && (
          <div style={{ marginBottom: 8, display: 'flex', alignItems: 'baseline', gap: 10, flexWrap: 'wrap' }}>
            <span style={{ fontSize: 18, fontWeight: 800, color: '#0f172a', letterSpacing: '-0.03em' }}>
              {'\uC8FC\uC18C\uB9CC \uC785\uB825\uD574\uB3C4 \uBB34\uC5C7\uC774 \uC9C0\uC815\uB3FC \uC788\uB294\uC9C0 \uBE60\uB974\uAC8C \uD655\uC778\uD569\uB2C8\uB2E4.'}
            </span>
            <span style={{ fontSize: 12, color: '#94a3b8', fontWeight: 500 }}>
              {'\uC8FC\uC18C \uAC80\uC0C9 \uB4A4 \uC6A9\uB3C4 \uC120\uD0DD\uACFC Quick \uAC80\uD1A0\uB85C \uC774\uC5B4\uC9D1\uB2C8\uB2E4.'}
            </span>
          </div>
        )}
        <AddressSearchBox
          searchText={searchText}
          onSearchTextChange={setSearchText}
          onCandidateSelect={handleAddressSelect}
          disabled={loading}
        />
        <BackendConnectionPanel />
      </div>

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
            <RecentHistoryPanel
              history={history}
              onRestore={handleRestoreFromHistory}
              onClear={clearHistory}
            />

            <MatchedGeometrySummary mapGeometries={result?.mapGeometries} />

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
