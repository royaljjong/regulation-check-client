export default function ResultSummaryPanel({ selectedCoordinate, result, loading, error }) {
  function formatCoord(v) { return typeof v === 'number' ? v.toFixed(6) : '-' }
  function toYesNo(v) { return v === true ? '예' : v === false ? '아니오' : '미확인' }

  function getBadge() {
    if (loading) return { label: '조회 중',   bg: '#f59e0b', color: '#1f2937' }
    if (error)   return { label: '조회 실패', bg: '#ef4444', color: '#ffffff' }
    if (!result) return { label: '조회 대기', bg: '#9ca3af', color: '#ffffff' }
    if (result?.overlayFlags?.hasAnyRestriction)
                 return { label: '제한 있음', bg: '#dc2626', color: '#ffffff' }
    return       { label: '제한 없음',        bg: '#16a34a', color: '#ffffff' }
  }

  const badge = getBadge()
  const overlays = [
    { label: '지구단위계획',         key: 'isInDistrictUnitPlan' },
    { label: '개발제한구역',         key: 'isDevelopmentRestricted' },
    { label: '개발행위허가제한지역',  key: 'isDevelopmentActionRestricted' },
    { label: '기타 제한 존재',       key: 'hasAnyRestriction' },
  ]

  const sec = (extra) => ({
    padding: 12, border: '1px solid #e5e7eb', borderRadius: 10,
    marginBottom: 12, backgroundColor: '#fafafa', ...extra,
  })

  return (
    <aside style={{ width: '100%', boxSizing: 'border-box' }}>
      <h2 style={{ margin: '0 0 12px', fontSize: 20 }}>결과 요약</h2>

      {/* 배지 */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 12 }}>
        <span style={{ padding: '6px 10px', borderRadius: 999, backgroundColor: badge.bg, color: badge.color, fontSize: 13, fontWeight: 700 }}>
          {badge.label}
        </span>
        {selectedCoordinate && (
          <span style={{ fontSize: 12, color: '#6b7280' }}>
            {formatCoord(selectedCoordinate.lat)}, {formatCoord(selectedCoordinate.lon)}
          </span>
        )}
      </div>

      {/* 위치명 */}
      <div style={sec()}>
        <h3 style={{ margin: '0 0 8px', fontSize: 14, color: '#374151' }}>위치명</h3>
        <div style={{ fontWeight: 700 }}>
          {result?.displayName || (selectedCoordinate ? '위치 선택됨' : '아직 조회 대상 없음')}
        </div>
      </div>

      {/* 요약 */}
      <div style={sec()}>
        <h3 style={{ margin: '0 0 8px', fontSize: 14, color: '#374151' }}>요약</h3>
        {loading && <p style={{ margin: 0 }}>법규 조회 중...</p>}
        {error   && <p style={{ margin: 0, color: '#b42318' }}>법규 조회에 실패했습니다.</p>}
        {!loading && !error && result && (
          <p style={{ margin: 0, whiteSpace: 'pre-line', lineHeight: 1.6 }}>{result.summaryText}</p>
        )}
        {!loading && !error && !result && (
          <p style={{ margin: 0, color: '#6b7280' }}>지도를 클릭하거나 주소를 검색해 조회하세요.</p>
        )}
      </div>

      {/* 좌표 */}
      <div style={sec()}>
        <h3 style={{ margin: '0 0 8px', fontSize: 14, color: '#374151' }}>좌표</h3>
        {selectedCoordinate ? (
          <div style={{ fontFamily: 'monospace', lineHeight: 1.6 }}>
            <div>위도: {formatCoord(selectedCoordinate.lat)}</div>
            <div>경도: {formatCoord(selectedCoordinate.lon)}</div>
          </div>
        ) : <div style={{ color: '#6b7280' }}>선택 좌표 없음</div>}
      </div>

      {/* 상세 상태 */}
      <div style={sec({ marginBottom: 0 })}>
        <h3 style={{ margin: '0 0 8px', fontSize: 14, color: '#374151' }}>상세 상태</h3>
        <div style={{ display: 'grid', gap: 8 }}>
          {overlays.map(({ label, key }) => {
            const yn = toYesNo(result?.overlayFlags?.[key])
            return (
              <div key={key} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '8px 10px', borderRadius: 8, border: '1px solid #e5e7eb', backgroundColor: '#fff' }}>
                <span>{label}</span>
                <span style={{ padding: '2px 8px', borderRadius: 999, fontSize: 12, fontWeight: 700,
                  backgroundColor: yn === '예' ? '#dcfce7' : '#f3f4f6',
                  color: yn === '예' ? '#166534' : '#374151' }}>
                  {yn}
                </span>
              </div>
            )
          })}
        </div>
      </div>
    </aside>
  )
}
