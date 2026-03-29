<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'

import Card from 'primevue/card'
import InputText from 'primevue/inputtext'
import DatePicker from 'primevue/datepicker'
import Select from 'primevue/select'
import Button from 'primevue/button'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'

interface FetchedFuelRecord {
  id: string
  departmentId: string
  refuelDate: string | null
  documentNumber: string
  vehiclePlate: string
  vehicleProvince: string
  fuelTypeName: string
  liters: number | null
  totalAmount: number | null
  gasStationCompany: string
  recordedBy: string
  createdAt: string
}

interface Department { id: string; name: string }

const authStore = useAuthStore()
const currentUserRole = computed(() => authStore.userProfile?.role || 'user')
const currentUserDepartment = computed(() => authStore.userProfile?.departmentId || '')

const historyRecords = ref<FetchedFuelRecord[]>([])
const departments = ref<Department[]>([])
const isLoading = ref(true)

onMounted(async () => {
  try {
    const params: Record<string, string | number> = { skip: 0, take: 500 }
    if (currentUserRole.value !== 'superadmin') {
      params.departmentId = currentUserDepartment.value
    }

    const [recordsRes, deptsRes] = await Promise.all([
      api.get('/FuelRecord', { params }),
      api.get('/Department'),
    ])
    historyRecords.value = recordsRes.data
    departments.value = deptsRes.data
  } finally {
    isLoading.value = false
  }
})

// Filters
const printDateFrom = ref<Date | null>(null)
const printDateTo = ref<Date | null>(null)
const printDeptId = ref<string>('')
const signerName = ref<string>('')
const signerPosition = ref<string>('เจ้าพนักงานธุรการชำนาญงาน')

const printRecords = computed(() =>
  historyRecords.value.filter((r) => {
    if (printDateFrom.value && r.refuelDate) {
      const from = new Date(printDateFrom.value); from.setHours(0, 0, 0, 0)
      if (new Date(r.refuelDate) < from) return false
    }
    if (printDateTo.value && r.refuelDate) {
      const to = new Date(printDateTo.value); to.setHours(23, 59, 59, 999)
      if (new Date(r.refuelDate) > to) return false
    }
    if (currentUserRole.value === 'superadmin' && printDeptId.value) {
      if (r.departmentId !== printDeptId.value) return false
    }
    return true
  })
)

const printTotalAmount = computed(() => printRecords.value.reduce((s, r) => s + (r.totalAmount ?? 0), 0))
const printTotalLiters = computed(() => printRecords.value.reduce((s, r) => s + (r.liters ?? 0), 0))

const formatThaiDate = (dateStr: string | null | undefined): string =>
  dateStr ? new Date(dateStr).toLocaleDateString('th-TH', { year: 'numeric', month: 'short', day: 'numeric' }) : '-'

const formatCurrency = (val: number | null | undefined): string =>
  val != null ? new Intl.NumberFormat('th-TH', { style: 'currency', currency: 'THB' }).format(val) : '-'

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

const printReport = () => {
  const records = printRecords.value
  if (records.length === 0) { alert('ไม่มีข้อมูลสำหรับพิมพ์'); return }

  const total = printTotalAmount.value
  const totalLiters = printTotalLiters.value

  const fmtDate = (dateStr: string | null) =>
    dateStr ? new Date(dateStr).toLocaleDateString('th-TH', { year: 'numeric', month: 'short', day: 'numeric' }) : ''
  const fmtAmount = (v: number | null) =>
    v != null ? v.toLocaleString('th-TH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : ''

  const thead = `<thead>
    <tr>
      <th rowspan="2" style="width:4%">ลำดับ</th>
      <th rowspan="2" style="width:12%">วัน เดือน ปี</th>
      <th style="width:18%">เลขที่</th>
      <th colspan="2">ทะเบียนรถ</th>
      <th rowspan="2">บริษัทจำหน่ายน้ำมัน</th>
      <th style="width:12%">จำนวนเงิน</th>
      <th rowspan="2" style="width:9%">จำนวนลิตร</th>
    </tr>
    <tr>
      <th>ใบกำกับภาษี</th>
      <th style="width:10%">เลขทะเบียน</th>
      <th style="width:7%">จังหวัด</th>
      <th>บาท</th>
    </tr>
  </thead>`

  const ROWS_PER_PAGE = 20
  const totalPages = Math.ceil(records.length / ROWS_PER_PAGE)
  const pages: string[] = []

  for (let p = 0; p < totalPages; p++) {
    const isLast = p === totalPages - 1
    const pageRecords = records.slice(p * ROWS_PER_PAGE, (p + 1) * ROWS_PER_PAGE)
    const startIdx = p * ROWS_PER_PAGE

    const bodyRows = pageRecords.map((r, i) => `<tr>
      <td class="c">${startIdx + i + 1}</td>
      <td class="c">${fmtDate(r.refuelDate)}</td>
      <td>${r.documentNumber || ''}</td>
      <td class="c">${r.vehiclePlate}</td>
      <td class="c">${r.vehicleProvince}</td>
      <td>${r.gasStationCompany || ''}</td>
      <td class="r">${fmtAmount(r.totalAmount)}</td>
      <td class="r">${(r.liters ?? 0).toFixed(3)}</td>
    </tr>`).join('')

    const summaryRow = isLast ? `
      <tr>
        <td colspan="6" class="c" style="padding:6px">
          <b>รวมทั้งสิ้น (ตัวอักษร)</b> -${numberToThaiBaht(total)}-
        </td>
        <td class="r">${fmtAmount(total)}</td>
        <td class="r">${totalLiters.toFixed(3)}</td>
      </tr>` : ''

    const signatureBlock = isLast ? `
      <div style="display:flex;justify-content:flex-end;margin-top:40px">
        <div style="text-align:center;min-width:300px;line-height:2">
          <div>ลงชื่อ.............................................................................</div>
          <div>(${signerName.value || '......................................................'})</div>
          <div>${signerPosition.value}</div>
        </div>
      </div>` : ''

    pages.push(`<div class="page">
      <h2>หน้างบใบสำคัญประกอบฏีกา</h2>
      <h3>หมวดรายจ่ายค่าน้ำมันเชื้อเพลิงและหล่อลื่น</h3>
      <table>${thead}<tbody>${bodyRows}${summaryRow}</tbody></table>
      ${signatureBlock}
    </div>`)
  }

  const css = `
    @import url('https://fonts.googleapis.com/css2?family=Sarabun:wght@400;700&display=swap');
    * { box-sizing:border-box; margin:0; padding:0; }
    body { font-family:'TH Sarabun New','Sarabun',Tahoma,sans-serif; font-size:16px; }
    .page { min-height:297mm; padding:12mm 16mm; page-break-after:always; break-after:page; }
    .page:last-child { page-break-after:avoid; break-after:avoid; }
    h2 { text-align:center; font-size:18px; font-weight:bold; margin-bottom:4px; white-space:nowrap; }
    h3 { text-align:center; font-size:16px; font-weight:bold; margin-bottom:10px; white-space:nowrap; }
    table { width:100%; border-collapse:collapse; }
    th,td { border:1px solid #000; padding:4px 6px; font-size:15px; vertical-align:middle; line-height:1.4; }
    th { background:#fff; text-align:center; font-weight:bold; }
    tr { height:34px; }
    thead tr { height:auto; }
    td.c { text-align:center; }
    td.r { text-align:right; }
    @media print { body{margin:0} .page{padding:10mm 14mm} }
    @page { size:A4 portrait; margin:0; }
  `

  const html = `<!DOCTYPE html><html lang="th"><head><meta charset="UTF-8">
  <title>หน้างบใบสำคัญประกอบฏีกา</title>
  <style>${css}</style></head><body>
  ${pages.join('\n')}
  </body></html>`

  const iframe = document.createElement('iframe')
  iframe.style.cssText = 'position:fixed;top:-9999px;left:-9999px;width:0;height:0;border:none'
  iframe.srcdoc = html
  iframe.onload = () => {
    iframe.contentWindow!.focus()
    iframe.contentWindow!.print()
    setTimeout(() => document.body.removeChild(iframe), 1000)
  }
  document.body.appendChild(iframe)
}
</script>

<template>
  <div class="max-w-5xl mx-auto pb-10">
    <div class="mb-6">
      <h2 class="text-2xl font-bold text-gray-800">
        <i class="pi pi-print text-red-500 mr-2"></i>พิมพ์หน้างบใบสำคัญประกอบฏีกา
      </h2>
      <p class="text-gray-500 mt-1">หมวดรายจ่ายค่าน้ำมันเชื้อเพลิงและหล่อลื่น</p>
    </div>

    <Card class="shadow-sm border-none">
      <template #content>
        <div class="flex flex-col gap-6">
          <!-- Filter -->
          <div>
            <h3 class="font-bold text-gray-700 border-b pb-2 mb-4">
              <i class="pi pi-filter mr-2 text-red-500"></i>ตัวกรองข้อมูล
            </h3>
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
              <div class="flex flex-col gap-2">
                <label class="text-sm font-semibold text-gray-700">วันที่เริ่มต้น</label>
                <DatePicker v-model="printDateFrom" dateFormat="dd/mm/yy" showIcon class="w-full" />
              </div>
              <div class="flex flex-col gap-2">
                <label class="text-sm font-semibold text-gray-700">วันที่สิ้นสุด</label>
                <DatePicker v-model="printDateTo" dateFormat="dd/mm/yy" showIcon class="w-full" />
              </div>
              <div v-if="currentUserRole === 'superadmin'" class="flex flex-col gap-2">
                <label class="text-sm font-semibold text-gray-700">หน่วยงาน</label>
                <Select v-model="printDeptId" :options="[{ id: '', name: 'ทั้งหมด' }, ...departments]"
                  optionLabel="name" optionValue="id" class="w-full" />
              </div>
              <div class="flex flex-col gap-2">
                <label class="text-sm font-semibold text-gray-700">ชื่อผู้ลงนาม</label>
                <InputText v-model="signerName" placeholder="ชื่อ-นามสกุล" class="w-full" />
              </div>
              <div class="flex flex-col gap-2">
                <label class="text-sm font-semibold text-gray-700">ตำแหน่ง</label>
                <InputText v-model="signerPosition" class="w-full" />
              </div>
            </div>
          </div>

          <!-- Summary Bar -->
          <div class="bg-red-50 border border-red-100 rounded-xl p-4 flex flex-wrap gap-6 items-center">
            <div>
              <p class="text-xs text-gray-500">จำนวนรายการ</p>
              <p class="font-bold text-xl text-gray-800">{{ printRecords.length }} รายการ</p>
            </div>
            <div>
              <p class="text-xs text-gray-500">รวมวงเงิน</p>
              <p class="font-bold text-xl text-red-600">{{ formatCurrency(printTotalAmount) }}</p>
            </div>
            <div>
              <p class="text-xs text-gray-500">รวมลิตร</p>
              <p class="font-bold text-xl text-gray-800">{{ printTotalLiters.toFixed(3) }} ลิตร</p>
            </div>
            <div class="ml-auto">
              <Button label="พิมพ์" icon="pi pi-print" severity="danger"
                :disabled="printRecords.length === 0" @click="printReport" />
            </div>
          </div>

          <!-- Preview -->
          <div>
            <h3 class="font-bold text-gray-700 border-b pb-2 mb-3">
              <i class="pi pi-eye mr-2 text-red-500"></i>ตัวอย่างรายการ
            </h3>
            <DataTable :value="printRecords" :loading="isLoading" :rows="10" paginator stripedRows
              emptyMessage="ไม่มีข้อมูลในช่วงเวลาที่เลือก" class="text-sm">
              <Column header="ลำดับ" style="width:4rem">
                <template #body="{ index }">{{ index + 1 }}</template>
              </Column>
              <Column header="วัน เดือน ปี">
                <template #body="sp">{{ formatThaiDate(sp.data.refuelDate) }}</template>
              </Column>
              <Column header="เลขที่ใบกำกับภาษี" field="documentNumber" />
              <Column header="ทะเบียนรถ">
                <template #body="sp">{{ sp.data.vehiclePlate }} ({{ sp.data.vehicleProvince }})</template>
              </Column>
              <Column header="บริษัทจำหน่ายน้ำมัน" field="gasStationCompany" />
              <Column header="วงเงิน (บาท)">
                <template #body="sp">
                  <span class="font-bold text-red-600">{{ formatCurrency(sp.data.totalAmount) }}</span>
                </template>
              </Column>
              <Column header="ลิตร">
                <template #body="sp">{{ (sp.data.liters ?? 0).toFixed(3) }}</template>
              </Column>
            </DataTable>
          </div>
        </div>
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
