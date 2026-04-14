// =============================================================================
// useRecentHistory.js
// 최근 조회 이력 관리 훅
//
// localStorage key : 'ark_recent_history'
// 최대 보관 건수  : 10건
// 중복 처리       : 같은 dedupKey → 기존 savedAt 유지, 최상단으로 이동, 내용 갱신
// =============================================================================

import { useState, useCallback } from 'react'

export const HISTORY_STORAGE_KEY = 'ark_recent_history'
const MAX_ENTRIES = 10

// ─────────────────────────────────────────────────────────────────────────────
// 중복 판별 키
//   - 주소 모드 : addr:<정규화된 레이블>
//   - 좌표 모드 : coord:<lon 4자리>:<lat 4자리>  (~11m 해상도)
// ─────────────────────────────────────────────────────────────────────────────
export function makeDedupKey(mode, label, coordinate) {
  if (mode === 'address') {
    return `addr:${(label ?? '').trim().toLowerCase()}`
  }
  const lon = coordinate?.lon ?? coordinate?.longitude ?? 0
  const lat = coordinate?.lat ?? coordinate?.latitude ?? 0
  return `coord:${lon.toFixed(4)}:${lat.toFixed(4)}`
}

// ─────────────────────────────────────────────────────────────────────────────
// localStorage 헬퍼
// ─────────────────────────────────────────────────────────────────────────────
function loadFromStorage() {
  try {
    const raw = localStorage.getItem(HISTORY_STORAGE_KEY)
    return raw ? JSON.parse(raw) : []
  } catch {
    return []
  }
}

function persistToStorage(entries) {
  try {
    localStorage.setItem(HISTORY_STORAGE_KEY, JSON.stringify(entries))
  } catch {
    // localStorage 용량 초과 또는 비활성 환경
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// result 요약 추출 (항목 표시용 — 전체 result는 별도 저장)
// ─────────────────────────────────────────────────────────────────────────────
function summarizeDevRestriction(result) {
  const dr = result?.extraLayers?.developmentRestriction
  if (!dr) return result?.overlayFlags?.isDevelopmentRestricted ? '개발제한구역' : '해당 없음'
  if (dr.status === 'unavailable') return '판정 불가'
  return dr.isInside ? '개발제한구역' : '해당 없음'
}

function summarizeDistrictUnitPlan(result) {
  const dup = result?.extraLayers?.districtUnitPlan
  if (!dup) return result?.overlayFlags?.isInDistrictUnitPlan ? '지구단위계획구역' : '해당 없음'
  if (dup.status === 'unavailable') return '판정 불가'
  return dup.isInside ? '지구단위계획구역' : '해당 없음'
}

// ─────────────────────────────────────────────────────────────────────────────
// 훅
// ─────────────────────────────────────────────────────────────────────────────

/**
 * 최근 조회 이력 CRUD 훅
 *
 * @returns {{
 *   history: Array,
 *   saveEntry: Function,
 *   patchEntry: Function,
 *   clearHistory: Function,
 * }}
 */
export function useRecentHistory() {
  const [history, setHistory] = useState(loadFromStorage)

  /**
   * 이력 항목 저장 (신규 or 덮어쓰기)
   *
   * @param {{
   *   mode        : 'address'|'coordinate',
   *   label       : string,
   *   coordinate  : {lon: number, lat: number},
   *   result      : object,
   *   lookupInfo  : {mode: string, label: string},
   *   selectedUse : string|null,
   * }} params
   */
  const saveEntry = useCallback(({
    mode, label, coordinate, result, lookupInfo, selectedUse = null,
  }) => {
    const dedupKey = makeDedupKey(mode, label, coordinate)

    setHistory(prev => {
      const existing = prev.find(it => it.dedupKey === dedupKey)

      const entry = {
        dedupKey,
        // 최초 저장 시각 유지 (중복 덮어쓰기 시에도 savedAt은 그대로)
        savedAt: existing?.savedAt ?? new Date().toISOString(),
        mode,
        label,
        coordinate,
        lookupInfo,
        selectedUse,
        // 표시용 요약
        zoneName:               result?.zoning?.zoneName ?? null,
        developmentRestriction: summarizeDevRestriction(result),
        districtUnitPlan:       summarizeDistrictUnitPlan(result),
        // 복원용 전체 데이터
        result,
      }

      const filtered = prev.filter(it => it.dedupKey !== dedupKey)
      const next = [entry, ...filtered].slice(0, MAX_ENTRIES)
      persistToStorage(next)
      return next
    })
  }, [])

  /**
   * 기존 항목의 일부 필드만 업데이트 (selectedUse 갱신 등)
   * savedAt, dedupKey 변경 없음 — 목록 순서도 유지
   *
   * @param {string} dedupKey
   * @param {Partial<object>} patch
   */
  const patchEntry = useCallback((dedupKey, patch) => {
    if (!dedupKey) return
    setHistory(prev => {
      const idx = prev.findIndex(it => it.dedupKey === dedupKey)
      if (idx === -1) return prev
      const next = [...prev]
      next[idx] = { ...next[idx], ...patch }
      persistToStorage(next)
      return next
    })
  }, [])

  /** 전체 이력 삭제 */
  const clearHistory = useCallback(() => {
    setHistory([])
    persistToStorage([])
  }, [])

  return { history, saveEntry, patchEntry, clearHistory }
}
