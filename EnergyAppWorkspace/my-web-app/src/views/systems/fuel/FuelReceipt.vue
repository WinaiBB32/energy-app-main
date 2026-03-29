<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { collection, query, orderBy, onSnapshot, doc, writeBatch, serverTimestamp, type QuerySnapshot, type QueryDocumentSnapshot } from 'firebase/firestore'
import { db } from '@/firebase/config'
import { useAuthStore } from '@/stores/auth'
import { toMonthKey, batchUpdateSummary } from '@/utils/monthlySummary'
import { usePermissions } from '@/composables/usePermissions'

import Card from 'primevue/card'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import DatePicker from 'primevue/datepicker'
import Button from 'primevue/button'
import Message from 'primevue/message'
import type { Department } from '@/types'

const authStore = useAuthStore()
const { isSuperAdmin } = usePermissions()
const currentUserDepartment = computed(() => authStore.userProfile?.departmentId || '')

const thaiMonths = [
  'มกราคม', 'กุมภาพันธ์', 'มีนาคม', 'เมษายน', 'พฤษภาคม', 'มิถุนายน',
  'กรกฎาคม', 'สิงหาคม', 'กันยายน', 'ตุลาคม', 'พฤศจิกายน', 'ธันวาคม',
]

const departments = ref<Department[]>([])
let unsubscribeDepts: () => void

onMounted(() => {
  unsubscribeDepts = onSnapshot(
    query(collection(db, 'departments'), orderBy('name')),
    (snap: QuerySnapshot) => { departments.value = snap.docs.map((d: QueryDocumentSnapshot) => ({ id: d.id, name: d.data().name as string })) },
  )
})
onUnmounted(() => { if (unsubscribeDepts) unsubscribeDepts() })

// ─── Form state ────────────────────────────────────────────────────────────
const blankEntry = () => ({
  detail: 'ค่าผ่านทางพิเศษ',
  receiptNo: '',
  bookNo: '',
  amount: null as number | null,
  driverName: '',
})

const entryDate = ref<Date | null>(new Date())
const entry = ref(blankEntry())
const isSubmitting = ref(false)
const successMessage = ref('')
const errorMessage = ref('')

const totalAmount = computed(() => entry.value.amount ?? 0)

// ─── Save ──────────────────────────────────────────────────────────────────
const submitForm = async () => {
  successMessage.value = ''
  errorMessage.value = ''
  if (!entryDate.value || !entry.value.amount || !entry.value.detail) {
    errorMessage.value = 'กรุณากรอกข้อมูลที่จำเป็น (*) ให้ครบถ้วน'
    return
  }

  const d = entryDate.value
  const singleEntry = {
    day: d.getDate(),
    month: thaiMonths[d.getMonth()],
    year: d.getFullYear() + 543,
    detail: entry.value.detail,
    receiptNo: entry.value.receiptNo,
    bookNo: entry.value.bookNo,
    amount: entry.value.amount,
    driverName: entry.value.driverName
  }

  try {
    isSubmitting.value = true
    const batch = writeBatch(db)
    const newDocRef = doc(collection(db, 'fuel_receipts'))

    // Operation 1: Create the new receipt document
    batch.set(newDocRef, {
      departmentId: isSuperAdmin.value ? '' : currentUserDepartment.value,
      entries: [singleEntry],
      receiptDate: entryDate.value, // Add timestamp for querying
      totalAmount: totalAmount.value,
      recordedByName: authStore.user?.displayName || authStore.user?.email?.split('@')[0] || 'ไม่ระบุชื่อ',
      recordedByUid: authStore.user?.uid || 'unknown',
      createdAt: serverTimestamp(),
    })

    // Operation 2: Update the monthly summary
    const monthKey = toMonthKey(entryDate.value)
    if (monthKey) {
      batchUpdateSummary(batch, monthKey, 'fuel_receipt', {
        totalAmount: totalAmount.value,
        count: 1,
      })
    }

    await batch.commit()

    successMessage.value = 'บันทึกใบรับรองแทนใบเสร็จรับเงินสำเร็จ'

    // Reset amount but keep other values for quick entry
    entry.value = { ...entry.value, amount: null, receiptNo: '', bookNo: '' }
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? `เกิดข้อผิดพลาด: ${e.message}` : 'เกิดข้อผิดพลาด'
  } finally {
    isSubmitting.value = false
  }
}

</script>

<template>
  <div class="max-w-4xl mx-auto pb-10">
    <div class="mb-6 flex justify-between items-end">
      <div>
        <h2 class="text-2xl font-bold text-gray-800">
          <i class="pi pi-file-edit text-red-500 mr-2"></i>บันทึกใบรับรองแทนใบเสร็จรับเงิน
        </h2>
        <p class="text-gray-500 mt-1">กรอกข้อมูลสำหรับใบเสร็จขนาดเล็ก หรือ ค่าผ่านทาง</p>
      </div>
    </div>

    <Card class="shadow-sm border-none">
      <template #content>
        <form @submit.prevent="submitForm" class="flex flex-col gap-8">
          <Message v-if="successMessage" severity="success" :closable="true">{{ successMessage }}</Message>
          <Message v-if="errorMessage" severity="error" :closable="true">{{ errorMessage }}</Message>

          <!-- ข้อมูลใบรับรอง -->
          <div class="bg-red-50/30 p-4 rounded-lg border border-red-100 mt-2">
            <h3 class="font-bold text-red-800 border-b border-red-200 pb-2 mb-4 text-lg">
              <i class="pi pi-receipt mr-2"></i>ข้อมูลรายจ่าย
            </h3>
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 items-end">
              <div class="flex flex-col gap-2">
                <label class="font-semibold text-sm text-gray-700">วันที่ <span class="text-red-500">*</span></label>
                <DatePicker v-model="entryDate" dateFormat="dd/mm/yy" class="w-full" showIcon />
              </div>
              <div class="flex flex-col gap-2 lg:col-span-2">
                <label class="font-semibold text-sm text-gray-700">รายละเอียดรายจ่าย <span
                    class="text-red-500">*</span></label>
                <InputText v-model="entry.detail" placeholder="ค่าผ่านทางพิเศษ" class="w-full" />
              </div>
              <div class="flex flex-col gap-2">
                <label class="font-semibold text-sm text-gray-700">พขร. / ผู้จ่ายเงิน</label>
                <InputText v-model="entry.driverName" placeholder="ชื่อคนขับ" class="w-full" />
              </div>

              <div class="flex flex-col gap-2">
                <label class="font-semibold text-sm text-gray-700">เลขที่</label>
                <InputText v-model="entry.receiptNo" placeholder="เลขที่อ้างอิง" class="w-full" />
              </div>
              <div class="flex flex-col gap-2">
                <label class="font-semibold text-sm text-gray-700">เล่มที่</label>
                <InputText v-model="entry.bookNo" placeholder="เล่มที่อ้างอิง" class="w-full" />
              </div>
              <div class="flex flex-col gap-2 lg:col-span-2">
                <label class="font-semibold text-sm text-gray-700">จำนวนเงิน (บาท) <span
                    class="text-red-500">*</span></label>
                <InputNumber v-model="entry.amount" :minFractionDigits="2" :maxFractionDigits="2" placeholder="0.00"
                  class="w-full" :inputProps="{ class: 'font-bold text-lg text-blue-700' }" />
              </div>
            </div>
          </div>

          <!-- Actions -->
          <div class="flex justify-end gap-3 mt-2 pt-4 border-t border-gray-100">
            <Button type="submit" label="บันทึกข้อมูล" icon="pi pi-save" severity="danger" class="px-8 py-3 text-lg"
              :loading="isSubmitting" :disabled="!entry.amount" />
          </div>
        </form>
      </template>
    </Card>
  </div>
</template>

<style scoped>
:deep(.p-inputnumber-input) {
  width: 100%;
}
</style>
