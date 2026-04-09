import { useState } from 'react'

const S = {
  wrap:  { border: '1px solid #cbd5e1', borderRadius: 10, background: '#fff', padding: 12, boxShadow: '0 2px 8px rgba(15,23,42,0.08)' },
  row:   { display: 'flex', gap: 8 },
  input: { flex: 1, minWidth: 0, border: '1px solid #cbd5e1', borderRadius: 8, padding: '8px 10px', fontSize: 14, outline: 'none' },
  btn:   (dis) => ({ border: 'none', background: dis ? '#cbd5e1' : '#0284c7', color: '#fff', borderRadius: 8, padding: '8px 12px', fontSize: 14, cursor: dis ? 'not-allowed' : 'pointer' }),
  msg:   { margin: '8px 0 0', fontSize: 13, color: '#475569' },
  list:  { marginTop: 8, maxHeight: 220, overflowY: 'auto', border: '1px solid #e2e8f0', borderRadius: 8, background: '#fff' },
  item:  { padding: '8px 10px', borderBottom: '1px solid #eef2f7', cursor: 'pointer', fontSize: 13, listStyle: 'none' },
}

// AddressCandidateDto: { address, latitude, longitude, addressType }
function getLabel(c) {
  return c.address || `${c.latitude}, ${c.longitude}`
}

/**
 * onCandidateSelect(lat, lon, regulationResult?, addressText?)
 *   - lat, lon: 선택된 좌표
 *   - regulationResult: POST /address 응답에 포함된 법규 결과 (단일 후보 자동선택 시만 전달)
 *   - addressText: 선택된 주소 문자열
 */
export default function AddressSearchBox({ searchText, onSearchTextChange, onCandidateSelect, disabled }) {
  const [candidates, setCandidates] = useState([])
  const [searching, setSearching]   = useState(false)
  const [msg, setMsg]               = useState('')

  function clear() { setCandidates([]); setMsg('') }

  async function handleSearch(e) {
    e.preventDefault()
    const kw = (searchText || '').trim()
    if (!kw || disabled || searching) return
    setSearching(true); clear()
    try {
      const res = await fetch(`${import.meta.env.VITE_API_BASE_URL ?? ''}/api/regulation-check/address`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'ngrok-skip-browser-warning': '1',
        },
        body: JSON.stringify({ query: kw }),
      })
      if (res.status === 404) { setMsg('검색 결과 없음'); return }
      if (!res.ok) throw new Error()

      const data = await res.json()
      const arr = Array.isArray(data.candidates) ? data.candidates : []
      if (!arr.length) { setMsg('검색 결과 없음'); return }

      if (arr.length === 1) {
        selectWithResult(arr[0], data.regulationResult ?? null)
        return
      }
      setCandidates(arr)
      setMsg('동일/유사 주소가 여러 건 있습니다. 아래 목록에서 선택하세요.')
    } catch { setMsg('주소 검색에 실패했습니다.') }
    finally { setSearching(false) }
  }

  function selectWithResult(c, regulationResult) {
    const lat = c.latitude
    const lon = c.longitude
    if (lat == null || lon == null) { setMsg('좌표 정보를 찾을 수 없습니다.'); return }
    
    const addr = c.address || ''
    // clear local search state
    clear()
    // pass selected data to page
    onCandidateSelect(lat, lon, regulationResult, addr)
  }

  function selectFromList(c) {
    selectWithResult(c, null)
  }

  return (
    <form onSubmit={handleSearch} style={S.wrap}>
      <div style={S.row}>
        <input
          value={searchText}
          onChange={(e) => { onSearchTextChange(e.target.value); clear() }}
          placeholder="주소 검색"
          disabled={disabled}
          style={S.input}
        />
        <button type="submit" disabled={disabled || searching} style={S.btn(disabled || searching)}>
          {searching ? '검색중...' : '검색'}
        </button>
      </div>
      {msg && <p style={S.msg}>{msg}</p>}
      {candidates.length > 0 && (
        <ul style={S.list}>
          {candidates.map((c, i) => (
            <li key={i} style={S.item} onClick={() => selectFromList(c)}>
              {getLabel(c)}
            </li>
          ))}
        </ul>
      )}
    </form>
  )
}
