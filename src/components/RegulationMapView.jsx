import { useEffect, useMemo, useRef, useState } from 'react'
import { CircleMarker, MapContainer, Polygon, Polyline, Popup, TileLayer, useMap, useMapEvents } from 'react-leaflet'

function MapClickHandler({ onMapClick }) {
  useMapEvents({
    click(event) {
      const { lat, lng } = event.latlng
      onMapClick(lat, lng)
    },
  })

  return null
}

function MapCenterSync({ selectedCoordinate, outlines }) {
  const map = useMap()
  const lastSignatureRef = useRef(null)

  useEffect(() => {
    const validOutlines = Array.isArray(outlines)
      ? outlines.filter((outline) => Array.isArray(outline) && outline.length >= 2)
      : []
    const outlineSignature = JSON.stringify(validOutlines)
    const selectedSignature = selectedCoordinate
      ? `${selectedCoordinate.lat}:${selectedCoordinate.lon}`
      : 'none'
    const nextSignature = `${selectedSignature}|${outlineSignature}`

    if (lastSignatureRef.current === nextSignature) {
      return
    }

    if (validOutlines.length > 0) {
      map.fitBounds(validOutlines.flat(), { padding: [24, 24] })
      lastSignatureRef.current = nextSignature
      return
    }

    if (selectedCoordinate) {
      map.setView([selectedCoordinate.lat, selectedCoordinate.lon], map.getZoom())
      lastSignatureRef.current = nextSignature
    }
  }, [map, outlines, selectedCoordinate])

  return null
}

function SetMapRef({ mapRef }) {
  const map = useMap()

  useEffect(() => {
    if (mapRef) {
      mapRef.current = map
    }
  }, [map, mapRef])

  return null
}

function toPathPositions(outline) {
  if (!Array.isArray(outline)) {
    return []
  }

  return outline
    .map((point) => [Number(point.latitude), Number(point.longitude)])
    .filter((point) => Number.isFinite(point[0]) && Number.isFinite(point[1]))
}

function buildGroupColor(items) {
  const first = items[0]
  return {
    fillColor: first?.fillColor ?? '#cbd5e1',
    strokeColor: first?.strokeColor ?? '#64748b',
  }
}

export default function RegulationMapView({ selectedCoordinate, result, loading, lookupMode, onMapClick, mapRef }) {
  const isAddress = lookupMode === 'address'

  const geometries = Array.isArray(result?.mapGeometries?.polygons)
    ? result.mapGeometries.polygons
        .map((item) => ({
          ...item,
          geometryType: item?.geometryType === 'line' ? 'line' : 'polygon',
          legendGroupKey: item?.legendGroupKey ?? item?.key ?? 'etc',
          legendGroupLabel: item?.legendGroupLabel ?? '\uAE30\uD0C0 \uB808\uC774\uC5B4',
          legendSortOrder: Number.isFinite(item?.legendSortOrder) ? item.legendSortOrder : 999,
          positions: toPathPositions(item?.outline),
        }))
        .filter((item) => item.positions.length >= (item.geometryType === 'line' ? 2 : 3))
    : []

  const [layerVisibility, setLayerVisibility] = useState({})

  useEffect(() => {
    if (!geometries.length) {
      setLayerVisibility({})
      return
    }

    setLayerVisibility((current) => {
      const next = {}
      for (const geometry of geometries) {
        next[geometry.key] = current[geometry.key] ?? true
      }
      return next
    })
  }, [geometries])

  const visibleGeometries = useMemo(
    () => geometries.filter((geometry) => layerVisibility[geometry.key] ?? true),
    [layerVisibility, geometries]
  )

  const geometryGroups = useMemo(() => {
    const grouped = new Map()

    for (const geometry of geometries) {
      const groupKey = geometry.legendGroupKey
      if (!grouped.has(groupKey)) {
        grouped.set(groupKey, {
          key: groupKey,
          label: geometry.legendGroupLabel,
          sortOrder: geometry.legendSortOrder,
          items: [],
        })
      }

      grouped.get(groupKey).items.push(geometry)
    }

    return Array.from(grouped.values())
      .map((group) => ({
        ...group,
        items: group.items.sort((left, right) => left.label.localeCompare(right.label, 'ko')),
        ...buildGroupColor(group.items),
      }))
      .sort((left, right) => left.sortOrder - right.sortOrder || left.label.localeCompare(right.label, 'ko'))
  }, [geometries])

  const pointFillColor = loading
    ? '#9ca3af'
    : result?.overlayFlags?.hasAnyRestriction
      ? '#dc2626'
      : '#16a34a'

  const pointStrokeColor = loading ? '#9ca3af' : isAddress ? '#1d4ed8' : pointFillColor
  const pointStrokeWeight = isAddress ? 3 : 2
  const displayName = result?.displayName ?? '선택 위치'
  const summaryText = result?.summaryText ?? ''

  return (
    <div style={{ width: '100%', height: '100%', minHeight: 400, position: 'relative' }}>
      <MapContainer
        center={[37.5665, 126.978]}
        zoom={13}
        style={{ width: '100%', height: '100%' }}
        scrollWheelZoom
      >
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />

        <SetMapRef mapRef={mapRef} />
        <MapClickHandler onMapClick={onMapClick} />
        <MapCenterSync
          selectedCoordinate={selectedCoordinate}
          outlines={visibleGeometries.map((geometry) => geometry.positions)}
        />

        {visibleGeometries.map((geometry) => {
          const isParcelBoundary = geometry.key === 'parcel_boundary'
          const popup = (
            <Popup>
              <div style={{ maxWidth: 280 }}>
                <div style={{ fontWeight: 700, marginBottom: 6 }}>{geometry.label}</div>
                <div style={{ fontSize: 12, color: '#475569', marginBottom: 6 }}>{geometry.legendGroupLabel}</div>
                <div style={{ fontSize: 13, lineHeight: 1.5 }}>
                  {isParcelBoundary
                    ? '\uD604\uC7AC \uC88C\uD45C\uB97C \uD3EC\uD568\uD558\uB294 V-World \uD544\uC9C0 \uACBD\uACC4 \uD6C4\uBCF4\uC785\uB2C8\uB2E4. \uC2E4\uC81C \uD5C8\uAC00 \uB3C4\uC11C\uC640 \uB300\uC870\uD558\uC5EC \uD544\uC9C0 \uD569\uBCC1\u00B7\uBD84\uD560\u00B7\uB3C4\uB85C \uC9C0\uC815 \uC5EC\uBD80\uB97C \uD55C \uBC88 \uB354 \uD655\uC778\uD558\uC138\uC694.'
                    : geometry.geometryType === 'line'
                    ? '\uB3C4\uB85C\u00B7\uAD50\uD1B5 \uACC4\uC5F4\uCC98\uB7FC \uC120\uD615 \uC131\uACA9\uC774 \uAC15\uD55C \uB3C4\uC2DC\uACC4\uD68D\uC2DC\uC124\uC785\uB2C8\uB2E4. \uB300\uC9C0\uC640\uC758 \uC815\uD655\uD55C \uC911\uCCA9 \uD310\uB2E8\uC740 \uB3C4\uBA74\uACFC \uD568\uAED8 \uD655\uC778\uD558\uB294 \uAC83\uC774 \uC548\uC804\uD569\uB2C8\uB2E4.'
                    : 'SHP \uB610\uB294 \uC678\uBD80 \uACC4\uD68D \uB808\uC774\uC5B4\uC5D0\uC11C \uC77D\uC740 \uB300\uD45C \uAD6C\uC5ED\uC785\uB2C8\uB2E4. \uD544\uC9C0 \uACBD\uACC4\uC640 \uC644\uC804\uD788 \uC77C\uCE58\uD558\uB294 \uC9C0\uC801\uB3C4\uB294 \uC544\uB2C8\uBBC0\uB85C \uCD5C\uC885 \uD310\uB2E8 \uC804 \uBCC4\uB3C4 \uD655\uC778\uC774 \uD544\uC694\uD569\uB2C8\uB2E4.'}
                </div>
              </div>
            </Popup>
          )

          if (geometry.geometryType === 'line') {
            return (
              <Polyline
                key={geometry.key}
                positions={geometry.positions}
                pathOptions={{
                  color: geometry.strokeColor,
                  weight: isParcelBoundary ? 5 : 4,
                  opacity: 0.9,
                  dashArray: '8 6',
                }}
              >
                {popup}
              </Polyline>
            )
          }

          return (
            <Polygon
              key={geometry.key}
              positions={geometry.positions}
              pathOptions={{
                color: geometry.strokeColor,
                fillColor: geometry.fillColor,
                fillOpacity: geometry.fillOpacity,
                weight: isParcelBoundary ? 3 : 2,
                dashArray: isParcelBoundary ? '5 4' : undefined,
              }}
            >
              {popup}
            </Polygon>
          )
        })}

        {selectedCoordinate && (
          <CircleMarker
            center={[selectedCoordinate.lat, selectedCoordinate.lon]}
            radius={10}
            pathOptions={{
              color: pointStrokeColor,
              fillColor: pointFillColor,
              fillOpacity: 0.85,
              weight: pointStrokeWeight,
            }}
          >
            <Popup>
              <div style={{ maxWidth: 240 }}>
                {isAddress && (
                  <div style={{ fontSize: 11, color: '#1d4ed8', fontWeight: 700, marginBottom: 4 }}>
                    \uC8FC\uC18C \uAC80\uC0C9
                  </div>
                )}
                <div style={{ fontWeight: 700, marginBottom: 6 }}>{displayName}</div>
                <div style={{ fontSize: 13, lineHeight: 1.5 }}>
                  {loading ? '\uBC95\uADDC\uC640 \uACF5\uAC04 \uB808\uC774\uC5B4\uB97C \uBD88\uB7EC\uC624\uB294 \uC911\uC785\uB2C8\uB2E4.' : summaryText}
                </div>
              </div>
            </Popup>
          </CircleMarker>
        )}
      </MapContainer>

      {geometryGroups.length > 0 && (
        <div
          style={{
            position: 'absolute',
            right: 12,
            top: 12,
            zIndex: 500,
            width: 320,
            maxHeight: 'calc(100% - 24px)',
            overflowY: 'auto',
            background: 'rgba(255,255,255,0.97)',
            border: '1px solid #cbd5e1',
            borderRadius: 14,
            boxShadow: '0 10px 28px rgba(15, 23, 42, 0.14)',
            padding: 14,
            boxSizing: 'border-box',
          }}
        >
          <div style={{ fontWeight: 800, fontSize: 13, color: '#0f172a', marginBottom: 6 }}>{'\uC9C0\uB3C4 \uBC94\uB840'}</div>
          <div style={{ fontSize: 12, color: '#475569', lineHeight: 1.5, marginBottom: 12 }}>
            {'\uB3C4\uC2DC\uACC4\uD68D\uC2DC\uC124\uC740 \uC131\uACA9\uBCC4\uB85C \uADF8\uB8F9\uD654\uD588\uACE0, \uB3C4\uB85C\u00B7\uAD50\uD1B5 \uACC4\uC5F4\uCC98\uB7FC \uC120\uD615 \uC2DC\uC124\uC740 \uC810\uC120\uC73C\uB85C \uD45C\uC2DC\uD569\uB2C8\uB2E4.'}
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            {geometryGroups.map((group) => {
              const visibleCount = group.items.filter((item) => layerVisibility[item.key] ?? true).length
              const allVisible = visibleCount === group.items.length

              return (
                <div key={group.key} style={{ borderTop: '1px solid #e2e8f0', paddingTop: 10 }}>
                  <label
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 8,
                      cursor: 'pointer',
                      fontWeight: 700,
                      color: '#1e293b',
                      fontSize: 13,
                    }}
                  >
                    <input
                      type="checkbox"
                      checked={allVisible}
                      onChange={() => {
                        const nextVisible = !allVisible
                        setLayerVisibility((current) => {
                          const next = { ...current }
                          for (const item of group.items) {
                            next[item.key] = nextVisible
                          }
                          return next
                        })
                      }}
                    />
                    <span
                      style={{
                        width: 12,
                        height: 12,
                        borderRadius: 3,
                        background: group.fillColor,
                        border: `2px solid ${group.strokeColor}`,
                        boxSizing: 'border-box',
                        flexShrink: 0,
                      }}
                    />
                    <span style={{ flex: 1 }}>{group.label}</span>
                    <span style={{ fontSize: 11, color: '#64748b' }}>{visibleCount}/{group.items.length}</span>
                  </label>

                  <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginTop: 8, paddingLeft: 22 }}>
                    {group.items.map((item) => (
                      <label
                        key={item.key}
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          gap: 8,
                          fontSize: 12,
                          color: '#334155',
                          cursor: 'pointer',
                        }}
                      >
                        <input
                          type="checkbox"
                          checked={layerVisibility[item.key] ?? true}
                          onChange={() => {
                            setLayerVisibility((current) => ({
                              ...current,
                              [item.key]: !(current[item.key] ?? true),
                            }))
                          }}
                        />
                        <span>{item.label}</span>
                        {item.geometryType === 'line' && (
                          <span style={{ fontSize: 11, color: '#64748b' }}>{'\uC120\uD615'}</span>
                        )}
                      </label>
                    ))}
                  </div>
                </div>
              )
            })}
          </div>
        </div>
      )}
    </div>
  )
}
