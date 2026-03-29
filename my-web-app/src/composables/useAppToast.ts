import { useToast } from 'primevue/usetoast'
import { FirebaseError } from 'firebase/app'

const FIREBASE_ERROR_MAP: Record<string, string> = {
  'permission-denied':    'ไม่มีสิทธิ์เข้าถึงข้อมูล กรุณาติดต่อผู้ดูแลระบบ',
  'not-found':            'ไม่พบข้อมูลที่ต้องการ',
  'already-exists':       'ข้อมูลนี้มีอยู่ในระบบแล้ว',
  'unavailable':          'ระบบชั่วคราวไม่พร้อมใช้งาน กรุณาลองใหม่อีกครั้ง',
  'deadline-exceeded':    'การเชื่อมต่อหมดเวลา กรุณาลองใหม่',
  'unauthenticated':      'กรุณาเข้าสู่ระบบก่อนใช้งาน',
  'resource-exhausted':   'คำขอมากเกินไป กรุณารอสักครู่แล้วลองใหม่',
  'cancelled':            'การดำเนินการถูกยกเลิก',
  'data-loss':            'เกิดข้อผิดพลาดของข้อมูล กรุณาติดต่อผู้ดูแลระบบ',
  'internal':             'เกิดข้อผิดพลาดภายในระบบ',
  'invalid-argument':     'ข้อมูลที่ส่งไปไม่ถูกต้อง',
}

export function useAppToast() {
  const toast = useToast()

  const error = (detail: string, summary = 'เกิดข้อผิดพลาด') => {
    toast.add({ severity: 'error', summary, detail, life: 6000 })
  }

  const success = (detail: string, summary = 'สำเร็จ') => {
    toast.add({ severity: 'success', summary, detail, life: 3000 })
  }

  const warn = (detail: string, summary = 'คำเตือน') => {
    toast.add({ severity: 'warn', summary, detail, life: 5000 })
  }

  const fromError = (err: unknown, fallback = 'เกิดข้อผิดพลาด กรุณาลองใหม่อีกครั้ง') => {
    if (err instanceof FirebaseError) {
      const code = err.code.replace('firestore/', '').replace('auth/', '').replace('storage/', '')
      const message = FIREBASE_ERROR_MAP[code] ?? fallback
      error(message)
    } else if (err instanceof Error) {
      error(err.message || fallback)
    } else {
      error(fallback)
    }
  }

  return { error, success, warn, fromError }
}
