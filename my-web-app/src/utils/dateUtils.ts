/**
 * dateUtils.ts — Thailand Timezone Safe Date Utilities
 *
 * ปัญหา: new Date(y, m, d).toISOString() แปลง local midnight เป็น UTC
 *   ตัวอย่าง: Jan 1 00:00 UTC+7 → "2025-12-31T17:00:00Z" (ผิด!)
 *
 * วิธีแก้: ดึงแค่ year/month/day จาก Date object แล้วสร้าง UTC string โดยตรง
 *   ตัวอย่าง: Jan 1 → "2026-01-01T00:00:00.000Z" (ถูก!)
 */

/** แปลง Date (local) → UTC midnight ISO string (ส่งไป API) */
export const toUtcDateOnly = (d: Date): string => {
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}T00:00:00.000Z`
}

/** แปลง Date (local) → UTC end-of-day ISO string (สำหรับ toDate filter) */
export const toUtcEndOfDay = (d: Date): string => {
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}T23:59:59.999Z`
}

/** สร้าง UTC midnight string จาก year/month/day (สำหรับ CSV import) */
export const toUtcDateFromParts = (yyyy: number, mm: number, dd: number): string => {
  const m = String(mm).padStart(2, '0')
  const d = String(dd).padStart(2, '0')
  return `${yyyy}-${m}-${d}T00:00:00.000Z`
}

/** วันที่ 1 ของเดือน เป็น UTC (สำหรับ monthly filter) */
export const toUtcMonthStart = (d: Date): string => {
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  return `${y}-${m}-01T00:00:00.000Z`
}

/** วันสุดท้ายของเดือน เป็น UTC end-of-day */
export const toUtcMonthEnd = (d: Date): string => {
  const lastDay = new Date(d.getFullYear(), d.getMonth() + 1, 0)
  return toUtcEndOfDay(lastDay)
}
