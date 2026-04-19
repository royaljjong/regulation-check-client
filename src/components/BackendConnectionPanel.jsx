import { useEffect, useMemo, useState } from 'react'
import { buildApiUrl, clearApiBaseUrl, fetchApi, getApiBaseUrl, setApiBaseUrl } from '../utils/apiClient'

const ENV_DEFAULT = (import.meta.env.VITE_API_BASE_URL ?? '').trim().replace(/\/+$/, '')

const S = {
  wrap: {
    border: '1px solid #dbeafe',
    borderRadius: 10,
    background: '#f8fbff',
    padding: '10px 12px',
  },
  row: {
    display: 'flex',
    gap: 8,
    alignItems: 'center',
    flexWrap: 'wrap',
  },
  input: {
    flex: 1,
    minWidth: 260,
    border: '1px solid #cbd5e1',
    borderRadius: 8,
    padding: '8px 10px',
    fontSize: 12,
    background: '#fff',
  },
  button: (tone = 'primary') => ({
    border: '1px solid',
    borderColor: tone === 'primary' ? '#2563eb' : '#cbd5e1',
    background: tone === 'primary' ? '#2563eb' : '#fff',
    color: tone === 'primary' ? '#fff' : '#334155',
    borderRadius: 8,
    padding: '8px 10px',
    fontSize: 12,
    fontWeight: 700,
    cursor: 'pointer',
  }),
  meta: {
    fontSize: 11,
    color: '#64748b',
    lineHeight: 1.5,
  },
  badge: (ok) => ({
    display: 'inline-flex',
    alignItems: 'center',
    gap: 6,
    padding: '3px 8px',
    borderRadius: 999,
    fontSize: 11,
    fontWeight: 700,
    background: ok ? '#f0fdf4' : '#fef3c7',
    color: ok ? '#166534' : '#92400e',
  }),
}

export default function BackendConnectionPanel() {
  const [draft, setDraft] = useState(getApiBaseUrl())
  const [activeBaseUrl, setActiveBaseUrl] = useState(getApiBaseUrl())
  const [health, setHealth] = useState({ loading: false, ok: false, message: '' })

  useEffect(() => {
    setDraft(getApiBaseUrl())
    setActiveBaseUrl(getApiBaseUrl())
  }, [])

  const healthUrl = useMemo(() => buildApiUrl('/api/regulation-check/health'), [activeBaseUrl])

  async function runHealthCheck() {
    setHealth({ loading: true, ok: false, message: '' })

    try {
      const res = await fetchApi('/api/regulation-check/health')
      if (!res.ok) throw new Error(`HTTP ${res.status}`)
      const payload = await res.json()
      setHealth({
        loading: false,
        ok: payload?.status === 'ok',
        message: payload?.status === 'ok'
          ? `백엔드 연결 정상 · ${payload?.service ?? 'automationrawcheck'}`
          : '헬스 응답은 왔지만 상태가 ok가 아닙니다.',
      })
    } catch (error) {
      setHealth({
        loading: false,
        ok: false,
        message: `연결 실패 · ${error instanceof Error ? error.message : 'unknown error'}`,
      })
    }
  }

  const handleSave = () => {
    setApiBaseUrl(draft)
    setActiveBaseUrl(getApiBaseUrl())
    setHealth({ loading: false, ok: false, message: '백엔드 주소를 저장했습니다. 헬스체크로 확인하세요.' })
  }

  const handleReset = () => {
    clearApiBaseUrl()
    const fallback = getApiBaseUrl()
    setDraft(fallback)
    setActiveBaseUrl(fallback)
    setHealth({ loading: false, ok: false, message: '환경변수 기본 주소로 되돌렸습니다.' })
  }

  return (
    <section style={S.wrap}>
      <div style={{ ...S.row, marginBottom: 8 }}>
        <strong style={{ fontSize: 13, color: '#1d4ed8' }}>백엔드 연결</strong>
        <span style={S.badge(!!activeBaseUrl)}>{activeBaseUrl ? '주소 설정됨' : '주소 비어 있음'}</span>
      </div>

      <div style={{ ...S.row, marginBottom: 8 }}>
        <input
          value={draft}
          onChange={(event) => setDraft(event.target.value)}
          placeholder="https://your-ngrok-domain.ngrok-free.dev"
          style={S.input}
        />
        <button type="button" onClick={handleSave} style={S.button('primary')}>
          저장
        </button>
        <button type="button" onClick={runHealthCheck} style={S.button()}>
          {health.loading ? '확인 중...' : '헬스체크'}
        </button>
        <button type="button" onClick={handleReset} style={S.button()}>
          기본값
        </button>
      </div>

      <div style={S.meta}>현재 주소: {activeBaseUrl || '(비어 있음)'}</div>
      <div style={S.meta}>헬스 URL: {healthUrl}</div>
      {ENV_DEFAULT && <div style={S.meta}>환경변수 기본값: {ENV_DEFAULT}</div>}
      {health.message && (
        <div style={{ ...S.meta, marginTop: 6, color: health.ok ? '#166534' : '#92400e' }}>
          {health.message}
        </div>
      )}
    </section>
  )
}
