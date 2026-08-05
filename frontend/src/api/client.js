const DEFAULT_API_BASE_URL = import.meta.env.DEV
  ? 'http://localhost:5018/api'
  : '/api'
const API_BASE_URL = (
  import.meta.env.VITE_API_BASE_URL || DEFAULT_API_BASE_URL
).replace(/\/$/, '')

export class ApiError extends Error {
  constructor(message, status, details) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.details = details
  }
}

function getValidationMessage(payload) {
  if (!payload?.errors) return null
  const firstError = Object.values(payload.errors).flat()[0]
  return firstError || null
}

export async function apiRequest(path, options = {}) {
  const { token, body, headers: customHeaders, ...requestOptions } = options
  const headers = new Headers(customHeaders)

  if (body !== undefined) {
    headers.set('Content-Type', 'application/json')
  }

  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  let response
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      ...requestOptions,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch (error) {
    if (error.name === 'AbortError') throw error
    throw new ApiError(
      'تعذّر الاتصال بالخادم. تأكد من تشغيل الـ Backend وإعداد رابط API.',
      0,
      null,
    )
  }

  const contentType = response.headers.get('content-type') || ''
  const payload = contentType.includes('application/json')
    ? await response.json()
    : null

  if (!response.ok) {
    throw new ApiError(
      payload?.message ||
        getValidationMessage(payload) ||
        payload?.title ||
        'حدث خطأ غير متوقع.',
      response.status,
      payload,
    )
  }

  return payload
}

export function buildQuery(parameters) {
  const query = new URLSearchParams()
  Object.entries(parameters).forEach(([key, value]) => {
    if (value !== '' && value !== null && value !== undefined) {
      query.set(key, value)
    }
  })
  return query.toString()
}
