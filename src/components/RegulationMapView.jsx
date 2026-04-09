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

export default function RegulationMapView({ selectedCoordinate, result, loading, onMapClick, mapRef }) {
  const markerColor = loading
    ? '#9ca3af'
    : result?.overlayFlags?.hasAnyRestriction
    ? '#dc2626'
    : '#16a34a'

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
            pathOptions={{ color: markerColor, fillColor: markerColor, fillOpacity: 0.85, weight: 2 }}
          >
            <Popup>
              <div style={{ maxWidth: 240 }}>
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
