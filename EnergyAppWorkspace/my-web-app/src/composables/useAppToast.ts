import { useToast } from 'primevue/usetoast'

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
    if (err instanceof Error) {
      error(err.message || fallback)
    } else {
      error(fallback)
    }
  }

  return { error, success, warn, fromError }
}
