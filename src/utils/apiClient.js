const STORAGE_KEY = 'automation-check.api-base-url'
const CHANGE_EVENT = 'automation-check:api-base-url-changed'

function normalizeBaseUrl(value) {
  if (!value) return ''
  return value.trim().replace(/\/+$/, '')
}

export function getApiBaseUrl() {
  if (typeof window !== 'undefined') {
    const stored = normalizeBaseUrl(window.localStorage.getItem(STORAGE_KEY))
    if (stored) return stored
  }

  return normalizeBaseUrl(import.meta.env.VITE_API_BASE_URL ?? '')
}

export function setApiBaseUrl(value) {
  if (typeof window === 'undefined') return

  const normalized = normalizeBaseUrl(value)
  if (normalized) {
    window.localStorage.setItem(STORAGE_KEY, normalized)
  } else {
    window.localStorage.removeItem(STORAGE_KEY)
  }

  window.dispatchEvent(new CustomEvent(CHANGE_EVENT, { detail: normalized }))
}

export function clearApiBaseUrl() {
  setApiBaseUrl('')
}

export function onApiBaseUrlChange(callback) {
  if (typeof window === 'undefined') return () => {}

  const handler = (event) => callback(event.detail ?? getApiBaseUrl())
  window.addEventListener(CHANGE_EVENT, handler)
  window.addEventListener('storage', handler)

  return () => {
    window.removeEventListener(CHANGE_EVENT, handler)
    window.removeEventListener('storage', handler)
  }
}

export function buildApiUrl(path) {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`
  return `${getApiBaseUrl()}${normalizedPath}`
}

export async function fetchApi(path, init = {}) {
  const nextInit = { ...init }
  const headers = new Headers(init.headers ?? {})

  if (!headers.has('ngrok-skip-browser-warning')) {
    headers.set('ngrok-skip-browser-warning', '1')
  }

  nextInit.headers = headers
  return fetch(buildApiUrl(path), nextInit)
}
