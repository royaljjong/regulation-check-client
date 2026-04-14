// =============================================================================
// RecentHistoryPanel.jsx
// 최근 조회 이력 패널
//
// - localStorage(ark_recent_history) 기반 최근 10건 표시
// - 클릭 → 지도 이동 + 결과 패널 복원 + selectedUse 복원
// - 전체 삭제 지원
// - 기본 접힘, 아이콘 클릭으로 펼치기/접기
// =============================================================================

import { useState } from 'react'

// ─────────────────────────────────────────────────────────────────────────────
// 헬퍼
// ─────────────────────────────────────────────────────────────────────────────
function formatSavedAt(isoString) {
  if (!isoString) return ''
  try {
    const d = new Date(isoString)
    const now = new Date()
    const diffMs = now - d
    const diffMin = Math.floor(diffMs / 60000)
    if (diffMin < 1)  return '방금 전'
    if (diffMin < 60) return `${diffMin}분 전`
    const diffH = Math.floor(diffMin / 60)
    if (diffH < 24) return `${diffH}시간 전`
    const diffD = Math.floor(diffH / 24)
    if (diffD < 7)  return `${diffD}일 전`
    return d.toLocaleDateString('ko-KR', { month: 'short', day: 'numeric' })
  } catch {
    return ''
  }
}

function truncate(str, max = 28) {
  if (!str) return ''
  return str.length > max ? str.slice(0, max - 1) + '…' : str
}

function ModeIcon({ mode }) {
  // 주소 모드: 검색 아이콘, 좌표 모드: 핀 아이콘
  return (
    <span style={{
      fontSize: 11, width: 18, height: 18,
      display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
      borderRadius: '50%', flexShrink: 0,
      background: mode === 'address' ? '#ede9fe' : '#e0f2fe',
      color:      mode === 'address' ? '#7c3aed'  : '#0284c7',
      fontWeight: 700,
    }}>
      {mode === 'address' ? 'A' : 'C'}
    </span>
  )
}

function RestrictionTag({ text }) {
  const isRestricted = text === '개발제한구역' || text === '지구단위계획구역'
  const isUnknown    = text === '판정 불가'
  return (
    <span style={{
      fontSize: 9, padding: '1px 5px', borderRadius: 3, fontWeight: 600, whiteSpace: 'nowrap',
      background: isRestricted ? '#fff1f2' : isUnknown ? '#f1f5f9' : '#f0fdf4',
      color:      isRestricted ? '#be123c' : isUnknown ? '#94a3b8' : '#15803d',
    }}>
      {text}
    </span>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// 컴포넌트
// ─────────────────────────────────────────────────────────────────────────────
export default function RecentHistoryPanel({ history, onRestore, onClear }) {
  const [open, setOpen] = useState(false)

  if (!history || history.length === 0) return null

  return (
    <div style={{
      marginBottom: 12, border: '1px solid #e2e8f0', borderRadius: 10,
      background: '#fff', overflow: 'hidden',
    }}>

      {/* ── 헤더 ── */}
      <button
        onClick={() => setOpen(v => !v)}
        style={{
          width: '100%', display: 'flex', alignItems: 'center', justifyContent: 'space-between',
          padding: '9px 12px', background: 'none', border: 'none', cursor: 'pointer',
          textAlign: 'left',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span style={{ fontSize: 12, fontWeight: 700, color: '#0f172a' }}>최근 조회</span>
          <span style={{
            fontSize: 10, padding: '1px 6px', borderRadius: 10, fontWeight: 700,
            background: '#f1f5f9', color: '#64748b',
          }}>
            {history.length}건
          </span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          {open && (
            <span
              role="button"
              tabIndex={0}
              onClick={e => { e.stopPropagation(); onClear() }}
              onKeyDown={e => { if (e.key === 'Enter') { e.stopPropagation(); onClear() } }}
              style={{ fontSize: 10, color: '#94a3b8', cursor: 'pointer', padding: '2px 4px' }}
            >
              전체 삭제
            </span>
          )}
          <span style={{
            fontSize: 10, color: '#94a3b8', lineHeight: 1,
            transform: open ? 'rotate(180deg)' : 'none',
            transition: 'transform 0.15s',
            display: 'inline-block',
          }}>
            ▼
          </span>
        </div>
      </button>

      {/* ── 이력 목록 ── */}
      {open && (
        <div style={{ borderTop: '1px solid #f1f5f9' }}>
          {history.map((entry, idx) => (
            <button
              key={entry.dedupKey}
              onClick={() => onRestore(entry)}
              style={{
                width: '100%', display: 'flex', flexDirection: 'column', gap: 3,
                padding: '9px 12px', background: 'none', border: 'none', cursor: 'pointer',
                textAlign: 'left',
                borderBottom: idx < history.length - 1 ? '1px solid #f8fafc' : 'none',
              }}
              onMouseEnter={e => { e.currentTarget.style.background = '#f8fafc' }}
              onMouseLeave={e => { e.currentTarget.style.background = 'none' }}
            >
              {/* 첫째 줄: 아이콘 + 레이블 + 시각 */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 6, width: '100%' }}>
                <ModeIcon mode={entry.mode} />
                <span style={{
                  flex: 1, fontSize: 12, fontWeight: 600, color: '#1e293b',
                  overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
                }}>
                  {truncate(entry.label)}
                </span>
                <span style={{ fontSize: 10, color: '#94a3b8', whiteSpace: 'nowrap', flexShrink: 0 }}>
                  {formatSavedAt(entry.savedAt)}
                </span>
              </div>

              {/* 둘째 줄: 용도지역 + 규제 태그 + selectedUse */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 4, paddingLeft: 24, flexWrap: 'wrap' }}>
                {entry.zoneName && (
                  <span style={{ fontSize: 10, color: '#475569', fontWeight: 500 }}>
                    {entry.zoneName}
                  </span>
                )}
                {entry.developmentRestriction && entry.developmentRestriction !== '해당 없음' && (
                  <RestrictionTag text={entry.developmentRestriction} />
                )}
                {entry.districtUnitPlan && entry.districtUnitPlan !== '해당 없음' && (
                  <RestrictionTag text={entry.districtUnitPlan} />
                )}
                {entry.selectedUse && (
                  <span style={{
                    fontSize: 9, padding: '1px 5px', borderRadius: 3, fontWeight: 600,
                    background: '#ede9fe', color: '#6d28d9', whiteSpace: 'nowrap',
                  }}>
                    {entry.selectedUse}
                  </span>
                )}
              </div>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
