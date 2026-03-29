<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  collection,
  query,
  orderBy,
  where,
  getDocs,
  limit,
  Timestamp,
} from 'firebase/firestore'
// Firebase Removed
import { useAuthStore } from '@/stores/auth'

import Card from 'primevue/card'
import DatePicker from 'primevue/datepicker'
import Select from 'primevue/select'
import Button from 'primevue/button'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import InputText from 'primevue/inputtext'

const declarerName = ref('')
const declarerPosition = ref('')
const declarerDept = ref('')

import type { ReceiptEntry, FetchedReceipt, Department } from '@/types'

const authStore = useAuthStore()
const currentUserRole = computed(() => authStore.userProfile?.role || 'user')
const currentUserDepartment = computed(() => authStore.userProfile?.departmentId || '')

const allRecords = ref<FetchedReceipt[]>([])
const departments = ref<Department[]>([])
const isLoading = ref(true)

// ─── Filters ───────────────────────────────────────────────────────────────
const filterDateFrom = ref<Date | null>(null)
const filterDateTo = ref<Date | null>(null)
const filterDeptId = ref('')

const filteredRecords = computed(() =>
  allRecords.value.filter((r) => {
    if (filterDateFrom.value) {
      const from = new Date(filterDateFrom.value); from.setHours(0, 0, 0, 0)
      if (r.createdAt?.toDate() < from) return false
    }
    if (filterDateTo.value) {
      const to = new Date(filterDateTo.value); to.setHours(23, 59, 59, 999)
      if (r.createdAt?.toDate() > to) return false
    }
    if (filterDeptId.value && r.departmentId !== filterDeptId.value) return false
    return true
  })
)

const totalAmount = computed(() => filteredRecords.value.reduce((s, r) => s + (r.totalAmount ?? 0), 0))

// ─── Firestore ─────────────────────────────────────────────────────────────
onMounted(async () => {
  try {
    const colRef = collection(db, 'fuel_receipts')
    const q = currentUserRole.value === 'superadmin'
      ? query(colRef, orderBy('createdAt', 'desc'), limit(500))
      : query(colRef, where('departmentId', '==', currentUserDepartment.value), orderBy('createdAt', 'desc'), limit(500))

    const [recordsSnap, deptsSnap] = await Promise.all([
      getDocs(q),
      getDocs(query(collection(db, 'departments'), orderBy('name'))),
    ])
    allRecords.value = recordsSnap.docs.map((d) => ({ id: d.id, ...d.data() } as FetchedReceipt))
    departments.value = deptsSnap.docs.map((d) => ({ id: d.id, name: d.data().name as string }))
  } finally {
    isLoading.value = false
  }
})


// ─── Helpers ───────────────────────────────────────────────────────────────
const getDeptName = (id: string) => departments.value.find((x) => x.id === id)?.name || id
const formatCurrency = (val: number | null | undefined) =>
  val != null ? new Intl.NumberFormat('th-TH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(val) : '-'
const formatDate = (ts: Timestamp | undefined) =>
  ts ? ts.toDate().toLocaleDateString('th-TH', { year: 'numeric', month: 'short', day: 'numeric' }) : '-'

const numberToThaiBaht = (amount: number): string => {
  if (amount === 0) return 'ศูนย์บาทถ้วน'
  const ones = ['', 'หนึ่ง', 'สอง', 'สาม', 'สี่', 'ห้า', 'หก', 'เจ็ด', 'แปด', 'เก้า']
  const convert6 = (n: number): string => {
    if (n === 0) return ''
    const แสน = Math.floor(n / 100000), หมื่น = Math.floor((n % 100000) / 10000)
    const พัน = Math.floor((n % 10000) / 1000), ร้อย = Math.floor((n % 1000) / 100)
    const สิบ = Math.floor((n % 100) / 10), หน่วย = n % 10
    let r = ''
    if (แสน) r += ones[แสน] + 'แสน'
    if (หมื่น) r += ones[หมื่น] + 'หมื่น'
    if (พัน) r += ones[พัน] + 'พัน'
    if (ร้อย) r += ones[ร้อย] + 'ร้อย'
    if (สิบ === 1) r += 'สิบ'
    else if (สิบ === 2) r += 'ยี่สิบ'
    else if (สิบ > 2) r += ones[สิบ] + 'สิบ'
    if (หน่วย === 1 && สิบ > 0) r += 'เอ็ด'
    else if (หน่วย > 0) r += ones[หน่วย]
    return r
  }
  const convert = (n: number): string =>
    n === 0 ? '' : (Math.floor(n / 1000000) > 0 ? convert6(Math.floor(n / 1000000)) + 'ล้าน' : '') + convert6(n % 1000000)
  const baht = Math.floor(amount)
  const satang = Math.round((amount - baht) * 100)
  return convert(baht) + 'บาท' + (satang > 0 ? convert(satang) + 'สตางค์' : 'ถ้วน')
}

// ─── Print single ───────────────────────────────────────────────────────────
const printBatch = () => {
  if (filteredRecords.value.length === 0) { alert('ไม่มีข้อมูลที่กรองอยู่'); return }
  
  let allEntries: ReceiptEntry[] = []
  filteredRecords.value.forEach(r => {
    allEntries = allEntries.concat(r.entries)
  })
  
  const total = allEntries.reduce((s, e) => s + (e.amount || 0), 0)
  const firstRecord = filteredRecords.value[0]!
  
  const fmtAmt = (v: number) => v.toLocaleString('th-TH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
  const rows = allEntries.map((e) => `<tr>
    <td class="c">${e.day || ''}</td><td class="c">${e.month || ''}</td><td class="c">${e.year || ''}</td>
    <td>${e.detail || ''}${e.receiptNo ? ` เลขที่ ${e.receiptNo}` : ''}${e.bookNo ? ` เล่มที่ ${e.bookNo}` : ''}</td>
    <td class="r">${e.amount != null ? fmtAmt(e.amount) : ''}</td>
    <td class="c">${e.driverName || ''}</td>
  </tr>`).join('')
  
  const deptDisplay = declarerDept.value || getDeptName(firstRecord.departmentId)
  const css = `
    @import url('https://fonts.googleapis.com/css2?family=Sarabun:wght@400;700&display=swap');
    *{box-sizing:border-box;margin:0;padding:0}
    body{font-family:'TH Sarabun New','Sarabun',Tahoma,sans-serif;font-size:16px}
    .page{min-height:297mm;padding:15mm 20mm}
    h2{text-align:center;font-size:20px;font-weight:bold;margin-bottom:16px}
    table{width:100%;border-collapse:collapse;margin-top:10px}
    th,td{border:1px solid #000;padding:5px 8px;font-size:15px;vertical-align:middle;line-height:1.4}
    th{text-align:center;font-weight:bold}
    td.c{text-align:center}td.r{text-align:right}
    @media print{body{margin:0}.page{padding:10mm 15mm}}
    @page{size:A4 portrait;margin:0}
  `
  const html = `<!DOCTYPE html><html lang="th"><head><meta charset="UTF-8">
  <title>รวมใบรับรองแทนใบเสร็จรับเงิน</title><style>${css}</style></head><body>
  <div class="page">
    <h2>ใบรับรองแทนใบเสร็จรับเงิน</h2>
    <table>
      <thead>
        <tr><th colspan="3">วัน เดือน ปี</th><th>รายละเอียดรายจ่าย</th><th>จำนวนเงิน</th><th>พขร.</th></tr>
        <tr><th style="width:6%">วัน</th><th style="width:14%">เดือน</th><th style="width:8%">ปี</th><th></th><th style="width:14%"></th><th style="width:16%"></th></tr>
      </thead>
      <tbody>
        ${rows}
        <tr><td colspan="4" class="c"><b>รวมทั้งสิ้น (ตัวอักษร)</b> -${numberToThaiBaht(total)}-</td><td class="r"><b>${fmtAmt(total)}</b></td><td></td></tr>
      </tbody>
    </table>
    <div style="margin-top:20px;line-height:2;font-size:16px">
      ข้าพเจ้า ${declarerName.value || '........................................'}
      ตำแหน่ง ${declarerPosition.value || '........................................'}
      สังกัด ${deptDisplay}
      ขอรับรองว่า รายจ่ายข้างต้นนี้ไม่อาจเรียกใบเสร็จรับเงินจากผู้รับได้ เนื่องจากเป็นค่าใช้จ่ายเล็กน้อยตามที่จ่ายจริง
    </div>
    <div style="display:flex;justify-content:flex-end;margin-top:40px">
      <div style="text-align:center;min-width:280px;line-height:2">
        <div>ลงชื่อ.............................................................................</div>
        <div>(${declarerName.value || '......................................................'})</div>
        <div>${declarerPosition.value || ''}</div>
      </div>
    </div>
  </div></body></html>`
  const iframe = document.createElement('iframe')
  iframe.style.cssText = 'position:fixed;top:-9999px;left:-9999px;width:0;height:0;border:none'
  iframe.srcdoc = html
  iframe.onload = () => { iframe.contentWindow!.focus(); iframe.contentWindow!.print(); setTimeout(() => document.body.removeChild(iframe), 1000) }
  document.body.appendChild(iframe)
}
</script>

<template>
  <div class="max-w-5xl mx-auto pb-10">
    <div class="mb-6">
      <h2 class="text-2xl font-bold text-gray-800">
        <i class="pi pi-print text-red-500 mr-2"></i>พิมพ์ใบรับรองแทนใบเสร็จรับเงิน
      </h2>
      <p class="text-gray-500 mt-1">เลือกกรองและพิมพ์ใบรับรองแต่ละฉบับ</p>
    </div>

    <Card class="shadow-sm border-none">
      <template #content>
        <!-- Filters -->
        <div class="mb-4">
          <h3 class="font-bold text-gray-700 border-b pb-2 mb-4">
            <i class="pi pi-filter mr-2 text-red-500"></i>ตัวกรองข้อมูล
          </h3>
          <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 items-end">
            <div class="flex flex-col gap-2">
              <label class="text-sm font-semibold text-gray-700">วันที่เริ่มต้น</label>
              <DatePicker v-model="filterDateFrom" dateFormat="dd/mm/yy" showIcon class="w-full" />
            </div>
            <div class="flex flex-col gap-2">
              <label class="text-sm font-semibold text-gray-700">วันที่สิ้นสุด</label>
              <DatePicker v-model="filterDateTo" dateFormat="dd/mm/yy" showIcon class="w-full" />
            </div>
            <div v-if="currentUserRole === 'superadmin'" class="flex flex-col gap-2">
              <label class="text-sm font-semibold text-gray-700">หน่วยงาน</label>
              <Select v-model="filterDeptId"
                :options="[{ id: '', name: 'ทั้งหมด' }, ...departments]"
                optionLabel="name" optionValue="id" class="w-full" />
            </div>
          </div>
        </div>

        <!-- ผู้รับรองที่จะพิมพ์ลงเอกสาร -->
        <div class="mb-4 bg-amber-50 p-4 rounded-lg border border-amber-100">
          <h3 class="font-bold text-amber-800 border-b border-amber-200 pb-2 mb-4">
            <i class="pi pi-user-edit mr-2"></i>ข้อมูลผู้รับรอง (พิมพ์ก่อนปริ้นจริง จะไม่ถูกบันทึก)
          </h3>
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div class="flex flex-col gap-2">
              <label class="text-sm font-semibold text-gray-700">ชื่อผู้รับรอง</label>
              <InputText v-model="declarerName" placeholder="ชื่อ-นามสกุล" class="w-full" />
            </div>
            <div class="flex flex-col gap-2">
              <label class="text-sm font-semibold text-gray-700">ตำแหน่ง</label>
              <InputText v-model="declarerPosition" placeholder="ตำแหน่ง" class="w-full" />
            </div>
            <div class="flex flex-col gap-2">
              <label class="text-sm font-semibold text-gray-700">สังกัด</label>
              <InputText v-model="declarerDept" :placeholder="getDeptName(currentUserDepartment)" class="w-full" />
            </div>
          </div>
        </div>

        <!-- Summary -->
        <div class="bg-red-50 border border-red-100 rounded-xl p-4 flex flex-wrap gap-6 items-center justify-between mb-4">
          <div class="flex gap-6">
            <div>
              <p class="text-xs text-gray-500">จำนวนฉบับ</p>
              <p class="font-bold text-xl text-gray-800">{{ filteredRecords.length }} บิล</p>
            </div>
            <div>
              <p class="text-xs text-gray-500">รวมยอดเงิน</p>
              <p class="font-bold text-xl text-red-600">{{ formatCurrency(totalAmount) }} บาท</p>
            </div>
          </div>
          <Button label="พิมพ์ใบรับรองรวมตามตัวกรอง" icon="pi pi-print" severity="danger" 
            :disabled="filteredRecords.length === 0" @click="printBatch" />
        </div>

        <!-- Table -->
        <DataTable
          :value="filteredRecords"
          :loading="isLoading"
          paginator :rows="15"
          stripedRows
          emptyMessage="ไม่พบข้อมูลในช่วงเวลาที่เลือก"
          class="text-sm"
        >
          <Column header="วันที่บันทึก">
            <template #body="sp">{{ formatDate(sp.data.createdAt) }}</template>
          </Column>
          <Column v-if="currentUserRole === 'superadmin'" header="หน่วยงาน">
            <template #body="sp">{{ getDeptName(sp.data.departmentId) }}</template>
          </Column>
          <Column header="จำนวนรายการ">
            <template #body="sp">{{ sp.data.entries?.length || 0 }} รายการ</template>
          </Column>
          <Column header="รวมยอดเงิน">
            <template #body="sp">
              <span class="font-bold text-red-600">{{ formatCurrency(sp.data.totalAmount) }}</span>
            </template>
          </Column>
          <Column header="ผู้รับรอง">
            <template #body="sp">{{ sp.data.declarerName || '-' }}</template>
          </Column>
        </DataTable>
      </template>
    </Card>
  </div>
</template>

<style scoped>
:deep(.p-datatable-header-cell) {
  background-color: #f8fafc !important;
  color: #475569 !important;
  font-weight: 700 !important;
}
</style>
