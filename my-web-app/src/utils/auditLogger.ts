import { collection, addDoc, serverTimestamp } from 'firebase/firestore'
import { db } from '@/firebase/config'

export type AuditAction =
  | 'LOGIN'
  | 'LOGOUT'
  | 'CREATE'
  | 'UPDATE'
  | 'DELETE'
  | 'UPLOAD'
  | 'APPROVE_USER'
  | 'REJECT_USER'
  | 'SUSPEND_USER'
  | 'VIEW_ADMIN'

export interface AuditUser {
  uid: string
  displayName: string
  email: string
  role: string
}

let _cachedIp = ''

async function fetchClientIp(): Promise<string> {
  if (_cachedIp) return _cachedIp
  try {
    const controller = new AbortController()
    const timer = setTimeout(() => controller.abort(), 3000)
    const res = await fetch('https://api.ipify.org?format=json', { signal: controller.signal })
    clearTimeout(timer)
    const data = (await res.json()) as { ip: string }
    _cachedIp = data.ip
    return _cachedIp
  } catch {
    return 'N/A'
  }
}

function parseBrowser(ua: string): string {
  if (ua.includes('Edg/')) return 'Edge'
  if (ua.includes('OPR/') || ua.includes('Opera')) return 'Opera'
  if (ua.includes('Chrome')) return 'Chrome'
  if (ua.includes('Firefox')) return 'Firefox'
  if (ua.includes('Safari')) return 'Safari'
  return 'Other'
}

export async function logAudit(
  user: AuditUser,
  action: AuditAction,
  module: string,
  detail: string = '',
): Promise<void> {
  try {
    const ip = await fetchClientIp()
    await addDoc(collection(db, 'audit_logs'), {
      uid: user.uid,
      displayName: user.displayName,
      email: user.email,
      role: user.role,
      action,
      module,
      detail,
      ipAddress: ip,
      browser: parseBrowser(navigator.userAgent),
      userAgent: navigator.userAgent.substring(0, 300),
      createdAt: serverTimestamp(),
    })
  } catch (e) {
    console.warn('[AuditLogger] failed:', e)
  }
}
