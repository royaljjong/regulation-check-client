import { useState, useRef } from 'react'

const S = {
  // 헤더 안에서도 자연스럽게 보이는 카드 스타일
  wrap: {
    border: '1px solid #cbd5e1', borderRadius: 10, background: '#fff',
    padding: '10px 12px', boxShadow: '0 2px 8px rgba(15,23,42,0.08)',
  },
  row:  { display: 'flex', gap: 8 },
  input: {
    flex: 1, minWidth: 0, border: '1px solid #cbd5e1', borderRadius: 8,
    padding: '9px 12px', fontSize: 14, outline: 'none',
  },
  btn: (dis) => ({
    border: 'none', background: dis ? '#cbd5e1' : '#0284c7', color: '#fff',
    borderRadius: 8, padding: '9px 16px', fontSize: 14, fontWeight: 600,
    cursor: dis ? 'not-allowed' : 'pointer', whiteSpace: 'nowrap',
  }),
  msg: { margin: '6px 0 0', fontSize: 12, color: '#64748b' },
  // 드롭다운: 부모(position:relative div) 기준 absolute — 지도 위에 오버레이됨
  list: {
    position: 'absolute', left: 0, right: 0, top: '100%',
    zIndex: 200,
    maxHeight: 240, overflowY: 'auto',
    border: '1px solid #e2e8f0', borderTop: 'none',
    borderRadius: '0 0 10px 10px', background: '#fff',
    boxShadow: '0 6px 16px rgba(0,0,0,0.12)',
  },
  item: (hov) => ({
    padding: '10px 14px', borderBottom: '1px solid #f1f5f9',
    cursor: 'pointer', fontSize: 13, listStyle: 'none',
    background: hov ? '#f0f9ff' : '#fff', color: hov ? '#0284c7' : '#1e293b',
    transition: 'background 0.1s',
  }),
}

function getLabel(c) {
  return c.address || c.Address ||
    `${c.latitude ?? c.Latitude}, ${c.longitude ?? c.Longitude}`
}

function getCoord(c) {
  return {
    lat: c.latitude  ?? c.Latitude,
    lon: c.longitude ?? c.Longitude,
  }
}

/**
 * AddressSearchBox — 헤더 배치 전용 (드롭다운은 지도 위에 오버레이)
 *
 * UX 흐름:
 *   입력 → Enter/검색 → POST /address
 *     ↳ 후보 1개: 법규결과 포함 자동선택
 *     ↳ 후보 여러개: 드롭다운 표시 → 클릭 → POST /address/select → 법규결과 포함 선택
 *
 * onCandidateSelect(lat, lon, regulationResult?, addressText?)
 */
export default function AddressSearchBox({ searchText, onSearchTextChange, onCandidateSelect, disabled }) {
  const [candidates, setCandidates] = useState([])
  const [searching,  setSearching]  = useState(false)
  const [selecting,  setSelecting]  = useState(false)
  const [hoveredIdx, setHoveredIdx] = useState(null)
  const [msg,        setMsg]        = useState('')
  const lastQueryRef = useRef('')

  const isBusy = disabled || searching || selecting

  function clear() { setCandidates([]); setMsg(''); setHoveredIdx(null) }

  function passToParent(c, regulationResult) {
    const { lat, lon } = getCoord(c)
    if (lat == null || lon == null) { setMsg('좌표 정보를 찾을 수 없습니다.'); return }
    const addr = c.address ?? c.Address ?? ''
    clear()
    onCandidateSelect(lat, lon, regulationResult, addr)
  }

  // POST /address ─────────────────────────────────────────────────────────────
  async function handleSearch(e) {
    e.preventDefault()
    const kw = (searchText || '').trim()
    if (!kw || isBusy) return

    setSearching(true); clear()
    lastQueryRef.current = kw

    try {
      const res = await fetch(
        `${import.meta.env.VITE_API_BASE_URL ?? ''}/api/regulation-check/address`,
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'ngrok-skip-browser-warning': '1' },
          body: JSON.stringify({ query: kw }),
        }
      )
      if (res.status === 404) { setMsg('주소를 찾을 수 없습니다.'); return }
      if (!res.ok) throw new Error(`HTTP ${res.status}`)

      const data = await res.json()
      const arr  = Array.isArray(data.candidates) ? data.candidates : []
      if (!arr.length) { setMsg('검색 결과가 없습니다.'); return }

      if (arr.length === 1) {
        passToParent(arr[0], data.regulationResult ?? null)
        return
      }

      setCandidates(arr)
      setMsg(`${arr.length}건의 유사 주소 — 아래에서 선택하세요`)
    } catch {
      setMsg('주소 검색에 실패했습니다. 서버 연결을 확인하세요.')
    } finally {
      setSearching(false)
    }
  }

  // POST /address/select ──────────────────────────────────────────────────────
  async function handleCandidateClick(c, index) {
    if (selecting) return
    setSelecting(true); clear()

    const query = lastQueryRef.current
    try {
      const res = await fetch(
        `${import.meta.env.VITE_API_BASE_URL ?? ''}/api/regulation-check/address/select`,
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'ngrok-skip-browser-warning': '1' },
          body: JSON.stringify({ query, candidateIndex: index }),
        }
      )
      if (res.ok) {
        const data     = await res.json()
        const selected = data.selectedCandidate ?? data.SelectedCandidate ?? c
        passToParent(selected, data.regulationResult ?? data.RegulationResult ?? null)
        return
      }
    } catch { /* fallback */ }
    finally { setSelecting(false) }

    passToParent(c, null)
  }

  const btnLabel = searching ? '검색중...' : selecting ? '조회중...' : '검색'

  return (
    // position:relative → 드롭다운의 absolute 포지션 기준점
    <div style={{ position: 'relative' }}>
      <form onSubmit={handleSearch} style={S.wrap}>
        <div style={S.row}>
          <input
            value={searchText}
            onChange={(e) => { onSearchTextChange(e.target.value); clear() }}
            placeholder="주소 또는 지번 검색 (예: 창원시 의창구, 서울 강남구 테헤란로...)"
            disabled={isBusy}
            style={S.input}
            autoComplete="off"
          />
          <button type="submit" disabled={isBusy} style={S.btn(isBusy)}>
            {btnLabel}
          </button>
        </div>
        {/* 상태 메시지 — 폼 안에 (크기 변화 없이 안내만) */}
        {(msg || selecting) && (
          <p style={{ ...S.msg, color: selecting ? '#3b82f6' : '#64748b' }}>
            {selecting ? '법규 데이터 조회 중...' : msg}
          </p>
        )}
      </form>

      {/* 후보 드롭다운 — absolute: 지도 위에 오버레이, 레이아웃 밀지 않음 */}
      {candidates.length > 0 && !selecting && (
        <ul style={S.list}>
          {candidates.map((c, i) => (
            <li
              key={i}
              style={S.item(hoveredIdx === i)}
              onMouseEnter={() => setHoveredIdx(i)}
              onMouseLeave={() => setHoveredIdx(null)}
              onClick={() => handleCandidateClick(c, i)}
            >
              {getLabel(c)}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
