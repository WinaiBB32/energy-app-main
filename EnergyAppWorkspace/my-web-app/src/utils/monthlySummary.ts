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

import { doc, writeBatch, increment, serverTimestamp } from 'firebase/firestore'
import { db } from '@/firebase/config'
import type { Timestamp } from 'firebase/firestore'

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
export function toMonthKey(date: Date | Timestamp | null | undefined): string | null {
  if (!date) return null
  const d = date instanceof Date ? date : date.toDate()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`
}

/**
 * อัปเดต monthly_summaries ผ่าน batch
 * @param batch  - writeBatch ที่กำลังสร้างอยู่ (เพื่อ merge กับ operation อื่น)
 * @param monthKey - 'yyyy-MM' เช่น '2026-03'
 * @param system   - ชื่อระบบ เช่น 'fuel', 'water'
 * @param delta    - ค่าที่จะ increment (บวกหรือลบ)
 */
export function batchUpdateSummary(
  batch: ReturnType<typeof writeBatch>,
  monthKey: string,
  system: SystemKey,
  delta: SummaryDelta,
): void {
  const ref = doc(db, 'monthly_summaries', monthKey)
  const fields: Record<string, ReturnType<typeof increment> | ReturnType<typeof serverTimestamp>> =
    {
      [`${system}.count`]: increment(delta.count),
      updatedAt: serverTimestamp(),
    }
  if (delta.totalAmount !== undefined)
    fields[`${system}.totalAmount`] = increment(delta.totalAmount)
  if (delta.totalLiters !== undefined)
    fields[`${system}.totalLiters`] = increment(delta.totalLiters)
  if (delta.received !== undefined) fields[`${system}.received`] = increment(delta.received)
  if (delta.normalMail !== undefined) fields[`${system}.normalMail`] = increment(delta.normalMail)
  if (delta.registeredMail !== undefined)
    fields[`${system}.registeredMail`] = increment(delta.registeredMail)
  if (delta.emsMail !== undefined) fields[`${system}.emsMail`] = increment(delta.emsMail)
  batch.set(ref, fields, { merge: true })
}
