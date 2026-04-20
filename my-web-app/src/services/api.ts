/* eslint-disable @typescript-eslint/no-explicit-any */
const BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5008/api/v1'
const TIMEOUT_MS = 10000

export interface ApiResponse<T = any> {
  data: T
  status: number
}

interface RequestConfig {
  headers?: HeadersInit
  params?: Record<string, any> | URLSearchParams
}

// Error ที่มี .response.status/.response.data เหมือน AxiosError
export class ApiError extends Error {
  response: { status: number; data: any }
  constructor(status: number, data: unknown) {
    super(`HTTP ${status}`)
    this.name = 'ApiError'
    this.response = { status, data }
  }
}

async function fetchWithTimeout(url: string, options: RequestInit): Promise<Response> {
  const controller = new AbortController()
  const id = setTimeout(() => controller.abort(), TIMEOUT_MS)
  try {
    return await fetch(url, { ...options, signal: controller.signal })
  } catch (err) {
    if ((err as Error).name === 'AbortError') throw new Error('Request timeout')
    throw err
  } finally {
    clearTimeout(id)
  }
}

function buildHeaders(url: string, extra?: HeadersInit): Headers {
  const headers = new Headers(extra)
  if (!headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }
  const isAuth = url.includes('/auth/login') || url.includes('/auth/register')
  const token = localStorage.getItem('jwt_token')
  if (token && !isAuth) {
    headers.set('Authorization', `Bearer ${token}`)
  }
  return headers
}

async function request<T = any>(
  method: string,
  url: string,
  body?: unknown,
  config?: RequestConfig,
): Promise<ApiResponse<T>> {
  let fullUrl = url.startsWith('http') ? url : `${BASE_URL}${url}`

  // รองรับ params เหมือน axios (รับทั้ง plain object และ URLSearchParams)
  if (config?.params) {
    const searchParams =
      config.params instanceof URLSearchParams
        ? config.params
        : (() => {
            const sp = new URLSearchParams()
            for (const [key, value] of Object.entries(config.params as Record<string, any>)) {
              if (value !== undefined && value !== null) {
                sp.append(key, String(value))
              }
            }
            return sp
          })()
    const qs = searchParams.toString()
    if (qs) fullUrl += `?${qs}`
  }

  const options: RequestInit = {
    method,
    headers: buildHeaders(url, config?.headers),
  }
  if (body !== undefined) {
    options.body = JSON.stringify(body)
  }

  const response = await fetchWithTimeout(fullUrl, options)

  const isAuth = url.includes('/auth/login') || url.includes('/auth/register')
  if (response.status === 401 && !isAuth) {
    console.warn('Token expired or unauthorized. Please login again.')
  }

  if (!response.ok) {
    let errorData: unknown
    try { errorData = await response.json() } catch { errorData = { message: response.statusText } }
    throw new ApiError(response.status, errorData)
  }

  if (response.status === 204) {
    return { data: {} as T, status: 204 }
  }

  const data = (await response.json()) as T
  return { data, status: response.status }
}

const api = {
  get: <T = any>(url: string, config?: RequestConfig) =>
    request<T>('GET', url, undefined, config),

  post: <T = any>(url: string, data?: unknown, config?: RequestConfig) =>
    request<T>('POST', url, data, config),

  put: <T = any>(url: string, data?: unknown, config?: RequestConfig) =>
    request<T>('PUT', url, data, config),

  patch: <T = any>(url: string, data?: unknown, config?: RequestConfig) =>
    request<T>('PATCH', url, data, config),

  delete: <T = any>(url: string, config?: RequestConfig) =>
    request<T>('DELETE', url, undefined, config),
}

export default api
