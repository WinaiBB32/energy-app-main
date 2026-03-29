import { onCall, HttpsError } from 'firebase-functions/v2/https'
import { onDocumentWritten } from 'firebase-functions/v2/firestore'
import * as admin from 'firebase-admin'

admin.initializeApp()
const db = admin.firestore()

// ─── Helpers ──────────────────────────────────────────────────────────────────

function parseBrowser(ua: string): string {
  if (ua.includes('Edg/')) return 'Edge'
  if (ua.includes('OPR/') || ua.includes('Opera')) return 'Opera'
  if (ua.includes('Chrome')) return 'Chrome'
  if (ua.includes('Firefox')) return 'Firefox'
  if (ua.includes('Safari')) return 'Safari'
  return 'Other'
}

function getMonthKey(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`
}

// ─── 1. Callable: บันทึกค่าโทรศัพท์ (คำนวณ VAT ฝั่ง Backend) ─────────────────
// Client ส่งมาแค่ตัวเลขดิบ — Server คำนวณ VAT 7% และ totalAmount เองทั้งหมด
export const addTelephoneRecord = onCall(
  { region: 'asia-southeast1' },
  async (request) => {
    if (!request.auth) {
      throw new HttpsError('unauthenticated', 'ต้องเข้าสู่ระบบก่อน')
    }

    const {
      departmentId, docReceiveNumber, docNumber,
      billingCycleMs, customerId,
      adslCost, sipTrunkCost, otherCost,
      note, recordedByName,
    } = request.data as Record<string, unknown>

    if (!billingCycleMs || !customerId) {
      throw new HttpsError('invalid-argument', 'กรุณากรอกข้อมูลที่จำเป็นให้ครบถ้วน')
    }

    const adsl = Number(adslCost) || 0
    const sip = Number(sipTrunkCost) || 0
    const other = Number(otherCost) || 0
    const subTotal = adsl + sip + other
    const vatAmount = Math.round(subTotal * 0.07 * 100) / 100
    const totalAmount = Math.round((subTotal + vatAmount) * 100) / 100

    const ref = await db.collection('telephone_records').add({
      departmentId: departmentId || '',
      docReceiveNumber: docReceiveNumber || '',
      docNumber: docNumber || '',
      billingCycle: admin.firestore.Timestamp.fromMillis(Number(billingCycleMs)),
      customerId,
      adslCost: adsl,
      sipTrunkCost: sip,
      otherCost: other,
      vatAmount,
      totalAmount,
      note: note || '',
      recordedByName: recordedByName || '',
      recordedByUid: request.auth.uid,
      createdAt: admin.firestore.FieldValue.serverTimestamp(),
    })

    return { id: ref.id, vatAmount, totalAmount }
  },
)

// ─── 2. Callable: แก้ไขค่าโทรศัพท์ (คำนวณ VAT ฝั่ง Backend) ─────────────────
export const updateTelephoneRecord = onCall(
  { region: 'asia-southeast1' },
  async (request) => {
    if (!request.auth) {
      throw new HttpsError('unauthenticated', 'ต้องเข้าสู่ระบบก่อน')
    }

    const {
      docId, departmentId, docReceiveNumber, docNumber,
      billingCycleMs, customerId,
      adslCost, sipTrunkCost, otherCost, note,
    } = request.data as Record<string, unknown>

    if (!docId || typeof docId !== 'string') {
      throw new HttpsError('invalid-argument', 'ไม่พบรหัสเอกสาร')
    }

    const adsl = Number(adslCost) || 0
    const sip = Number(sipTrunkCost) || 0
    const other = Number(otherCost) || 0
    const subTotal = adsl + sip + other
    const vatAmount = Math.round(subTotal * 0.07 * 100) / 100
    const totalAmount = Math.round((subTotal + vatAmount) * 100) / 100

    await db.collection('telephone_records').doc(docId).update({
      departmentId: departmentId || '',
      docReceiveNumber: docReceiveNumber || '',
      docNumber: docNumber || '',
      billingCycle: admin.firestore.Timestamp.fromMillis(Number(billingCycleMs)),
      customerId,
      adslCost: adsl,
      sipTrunkCost: sip,
      otherCost: other,
      vatAmount,
      totalAmount,
      note: note || '',
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    })

    return { vatAmount, totalAmount }
  },
)

// ─── 3. Callable: บันทึก Audit Log (ดึง IP จาก Server — ไม่ต้องพึ่ง ipify) ──
export const logAuditEvent = onCall(
  { region: 'asia-southeast1' },
  async (request) => {
    if (!request.auth) return { success: false }

    // IP มาจาก header ของ request โดยอัตโนมัติ ไม่ต้องให้ client ส่งมา
    const forwarded = request.rawRequest.headers['x-forwarded-for'] as string | undefined
    const ip = forwarded?.split(',')[0]?.trim() ?? request.rawRequest.ip ?? 'unknown'

    const { action, module, detail, displayName, email, role, userAgent } =
      request.data as Record<string, unknown>

    await db.collection('audit_logs').add({
      uid: request.auth.uid,
      displayName: displayName || '',
      email: email || '',
      role: role || 'user',
      action,
      module,
      detail: detail || '',
      ipAddress: ip,
      browser: parseBrowser(String(userAgent || '')),
      userAgent: String(userAgent || '').slice(0, 300),
      createdAt: admin.firestore.FieldValue.serverTimestamp(),
    })

    return { success: true }
  },
)

// ─── 4. Trigger: สะสมยอดรายเดือนสำหรับ fuel_records ─────────────────────────
// เมื่อมีการ create/update/delete fuel_records → อัปเดต monthly_summaries/{yyyy-MM}
export const onFuelRecordWrite = onDocumentWritten(
  { document: 'fuel_records/{docId}', region: 'asia-southeast1' },
  async (event) => {
    const before = event.data?.before.exists ? event.data.before.data() : null
    const after = event.data?.after.exists ? event.data.after.data() : null

    const toMonthKey = (d: admin.firestore.DocumentData | null | undefined): string | null => {
      if (!d?.refuelDate) return null
      return getMonthKey((d.refuelDate as admin.firestore.Timestamp).toDate())
    }

    const keyBefore = toMonthKey(before)
    const keyAfter = toMonthKey(after)
    if (!keyBefore && !keyAfter) return

    const batch = db.batch()
    const inc = admin.firestore.FieldValue.increment
    const ts = admin.firestore.FieldValue.serverTimestamp()

    if (keyBefore === keyAfter && keyBefore) {
      // เดือนเดิม: อัปเดตเฉพาะยอด net delta (ไม่เปลี่ยน count)
      const ref = db.doc(`monthly_summaries/${keyBefore}`)
      batch.set(ref, {
        fuel: {
          totalAmount: inc((after?.totalAmount || 0) - (before?.totalAmount || 0)),
          totalLiters: inc((after?.liters || 0) - (before?.liters || 0)),
        },
        updatedAt: ts,
      }, { merge: true })
    } else {
      if (keyBefore) {
        batch.set(db.doc(`monthly_summaries/${keyBefore}`), {
          fuel: {
            totalAmount: inc(-(before!.totalAmount || 0)),
            totalLiters: inc(-(before!.liters || 0)),
            count: inc(-1),
          },
          updatedAt: ts,
        }, { merge: true })
      }
      if (keyAfter) {
        batch.set(db.doc(`monthly_summaries/${keyAfter}`), {
          fuel: {
            totalAmount: inc(after!.totalAmount || 0),
            totalLiters: inc(after!.liters || 0),
            count: inc(1),
          },
          updatedAt: ts,
        }, { merge: true })
      }
    }

    await batch.commit()
  },
)

// ─── 5. Trigger: สะสมยอดรายเดือนสำหรับ telephone_records ────────────────────
// เมื่อมีการ create/update/delete telephone_records → อัปเดต monthly_summaries/{yyyy-MM}
export const onTelephoneRecordWrite = onDocumentWritten(
  { document: 'telephone_records/{docId}', region: 'asia-southeast1' },
  async (event) => {
    const before = event.data?.before.exists ? event.data.before.data() : null
    const after = event.data?.after.exists ? event.data.after.data() : null

    const toMonthKey = (d: admin.firestore.DocumentData | null | undefined): string | null => {
      if (!d?.createdAt) return null
      return getMonthKey((d.createdAt as admin.firestore.Timestamp).toDate())
    }

    const keyBefore = toMonthKey(before)
    const keyAfter = toMonthKey(after)
    if (!keyBefore && !keyAfter) return

    const batch = db.batch()
    const inc = admin.firestore.FieldValue.increment
    const ts = admin.firestore.FieldValue.serverTimestamp()

    if (keyBefore === keyAfter && keyBefore) {
      // update ในเดือนเดิม: เปลี่ยนเฉพาะยอด
      batch.set(db.doc(`monthly_summaries/${keyBefore}`), {
        telephone: {
          totalAmount: inc((after?.totalAmount || 0) - (before?.totalAmount || 0)),
        },
        updatedAt: ts,
      }, { merge: true })
    } else {
      if (keyBefore) {
        batch.set(db.doc(`monthly_summaries/${keyBefore}`), {
          telephone: {
            totalAmount: inc(-(before!.totalAmount || 0)),
            count: inc(-1),
          },
          updatedAt: ts,
        }, { merge: true })
      }
      if (keyAfter) {
        batch.set(db.doc(`monthly_summaries/${keyAfter}`), {
          telephone: {
            totalAmount: inc(after!.totalAmount || 0),
            count: inc(1),
          },
          updatedAt: ts,
        }, { merge: true })
      }
    }

    await batch.commit()
  },
)
