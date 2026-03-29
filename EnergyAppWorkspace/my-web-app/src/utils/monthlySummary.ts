/**
 * monthlySummary.ts
 * ─────────────────────────────────────────────────────────────────────────────
 * Utility สำหรับอัปเดต Document สรุปยอดรายเดือน (monthly_summaries/{yyyy-MM})
 * ใช้ writeBatch + increment เพื่อให้ Dashboard อ่านแค่ 1 Document แทนการ
 * ดึงบิลทั้งหมดมาบวกเลขฝั่ง Client
 *
 * โครงสร้าง monthly_summaries/{yyyy-MM}:
 * {
 *   fuel:      { totalAmount, totalLiters, count }
 *   water:     { totalAmount, count }
 *   telephone: { totalAmount, count }
 *   electricity: { totalAmount, count }
 *   saraban:   { received, count }
 *   updatedAt: serverTimestamp
 * }
 */

export type SystemKey =
  | 'fuel'
  | 'water'
  | 'telephone'
  | 'electricity'
  | 'saraban'
  | 'fuel_receipt'
  | 'postal'

export interface SummaryDelta {
  totalAmount?: number
  totalLiters?: number
  received?: number
  // Postal fields
  normalMail?: number
  registeredMail?: number
  emsMail?: number
  count: number // +1 สำหรับสร้าง, -1 สำหรับลบ
}

/** แปลง Date หรือ Timestamp เป็น 'yyyy-MM' */
export function toMonthKey(date: Date | string | null | undefined): string | null {
  if (!date) return null
  const d = date instanceof Date ? date : new Date(date)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`
}

/**
 * อัปเดต monthly_summaries (จำลองเพื่อรอเชื่อม API)
 * @param monthKey - 'yyyy-MM' เช่น '2026-03'
 * @param system   - ชื่อระบบ เช่น 'fuel', 'water'
 * @param delta    - ค่าที่จะ increment (บวกหรือลบ)
 */
export function updateSummary(monthKey: string, system: SystemKey, delta: SummaryDelta): void {
  // TODO: Implement API call to update monthly summary
  console.log(`Update summary for ${monthKey} in ${system}`, delta)
}

/**
 * Mock version for batch updates
 */
export function batchUpdateSummary(batch: any, monthKey: string, system: SystemKey, delta: SummaryDelta): void {
  // Mock implementation for UI stability
  console.log(`[Mock] Batch update summary for ${monthKey} in ${system}`, delta)
}
