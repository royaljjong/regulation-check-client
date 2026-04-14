import { useEffect } from 'react'
import { CircleMarker, MapContainer, Popup, TileLayer, useMap, useMapEvents } from 'react-leaflet'

function MapClickHandler({ onMapClick }) {
  useMapEvents({
    click(e) {
      const { lat, lng } = e.latlng
      onMapClick(lat, lng)
    },
  })
  return null
}

function MapCenterSync({ selectedCoordinate }) {
  const map = useMap()
  useEffect(() => {
    if (selectedCoordinate) {
      map.setView([selectedCoordinate.lat, selectedCoordinate.lon], map.getZoom())
    }
  }, [map, selectedCoordinate])
  return null
}

function SetMapRef({ mapRef }) {
  const map = useMap()
  useEffect(() => {
    if (mapRef) mapRef.current = map
  }, [map, mapRef])
  return null
}

/**
 * lookupMode: 'address' | 'coordinate' | 'none'
 *
 * 마커 구분:
 *   address    → 파란 테두리 링 (fillColor는 규제 여부 기반)
 *   coordinate → 테두리 = fillColor (기존 방식)
 *   loading    → 회색
 */
export default function RegulationMapView({ selectedCoordinate, result, loading, lookupMode, onMapClick, mapRef }) {
  const isAddress = lookupMode === 'address'

  const fillColor = loading
    ? '#9ca3af'
    : result?.overlayFlags?.hasAnyRestriction
    ? '#dc2626'
    : '#16a34a'

  // 주소 검색: 파란 테두리로 클릭 마커와 구분
  const strokeColor = loading
    ? '#9ca3af'
    : isAddress
    ? '#1d4ed8'
    : fillColor

  const strokeWeight = isAddress ? 3 : 2

  const displayName = result?.displayName ?? '선택 위치'
  const summaryText = result?.summaryText ?? ''

  return (
    <div style={{ width: '100%', height: '100%', minHeight: 400 }}>
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
        <MapCenterSync selectedCoordinate={selectedCoordinate} />

        {selectedCoordinate && (
          <CircleMarker
            center={[selectedCoordinate.lat, selectedCoordinate.lon]}
            radius={10}
            pathOptions={{
              color:       strokeColor,
              fillColor:   fillColor,
              fillOpacity: 0.85,
              weight:      strokeWeight,
            }}
          >
            <Popup>
              <div style={{ maxWidth: 240 }}>
                {/* 주소/클릭 출처 표시 */}
                {isAddress && (
                  <div style={{ fontSize: 11, color: '#1d4ed8', fontWeight: 600, marginBottom: 4 }}>
                    주소 검색
                  </div>
                )}
                <div style={{ fontWeight: 600, marginBottom: 6 }}>{displayName}</div>
                <div style={{ fontSize: 13, lineHeight: 1.5 }}>
                  {loading ? '법규 조회 중...' : summaryText}
                </div>
              </div>
            </Popup>
          </CircleMarker>
        )}
      </MapContainer>
    </div>
  )
}
