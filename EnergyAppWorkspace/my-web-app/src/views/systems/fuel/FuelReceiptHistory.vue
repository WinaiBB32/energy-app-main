<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'

import Card from 'primevue/card'
import DatePicker from 'primevue/datepicker'
import InputText from 'primevue/inputtext'
import InputNumber from 'primevue/inputnumber'
import Textarea from 'primevue/textarea'
import Select from 'primevue/select'
import Button from 'primevue/button'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Dialog from 'primevue/dialog'

import type { FetchedReceipt, Department } from '@/types'

const authStore = useAuthStore()
const currentUserRole = computed(() => authStore.userProfile?.role || 'user')
const currentUserDepartment = computed(() => authStore.userProfile?.departmentId || '')

const historyRecords = ref<FetchedReceipt[]>([])
const departments = ref<Department[]>([])
const isLoading = ref(true)

// ─── Filters ───────────────────────────────────────────────────────────────
const filterDateFrom = ref<Date | null>(null)
const filterDateTo = ref<Date | null>(null)
const filterDeptId = ref('')

const filteredRecords = computed(() =>
  historyRecords.value.filter((r) => {
    if (filterDateFrom.value) {
      const from = new Date(filterDateFrom.value); from.setHours(0, 0, 0, 0)
      if (new Date(r.createdAt) < from) return false
    }
    if (filterDateTo.value) {
      const to = new Date(filterDateTo.value); to.setHours(23, 59, 59, 999)
      if (new Date(r.createdAt) > to) return false
    }
    if (filterDeptId.value && r.departmentId !== filterDeptId.value) return false
    return true
  })
)

const totalAmount = computed(() => filteredRecords.value.reduce((s, r) => s + (r.totalAmount ?? 0), 0))

const clearFilters = () => { filterDateFrom.value = null; filterDateTo.value = null; filterDeptId.value = '' }

// ─── Fetch ─────────────────────────────────────────────────────────────────
onMounted(async () => {
  try {
    const params: Record<string, string> = { take: '200' }
    if (currentUserRole.value !== 'superadmin') params.departmentId = currentUserDepartment.value

    const [receiptsRes, deptsRes] = await Promise.all([
      api.get('/FuelReceipt', { params }),
      api.get('/Department'),
    ])
    historyRecords.value = (receiptsRes.data.items || []).map((r: FetchedReceipt & { entriesJson?: string }) => ({
      ...r,
      entries: r.entries ?? (r.entriesJson ? JSON.parse(r.entriesJson) : []),
    }))
    departments.value = deptsRes.data
  } catch (error: unknown) {
    console.error(error)
  } finally {
    isLoading.value = false
  }
})

// ─── Helpers ───────────────────────────────────────────────────────────────
const thaiMonths = [
  'มกราคม', 'กุมภาพันธ์', 'มีนาคม', 'เมษายน', 'พฤษภาคม', 'มิถุนายน',
  'กรกฎาคม', 'สิงหาคม', 'กันยายน', 'ตุลาคม', 'พฤศจิกายน', 'ธันวาคม',
]
const getDeptName = (id: string) => departments.value.find((x) => x.id === id)?.name || id
const formatCurrency = (val: number | null | undefined) =>
  val != null ? new Intl.NumberFormat('th-TH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(val) : '-'
const formatDate = (dateStr: string | null | undefined) =>
  dateStr ? new Date(dateStr).toLocaleDateString('th-TH', { year: 'numeric', month: 'short', day: 'numeric' }) : '-'

// ─── Detail / Delete ────────────────────────────────────────────────────────
const selectedRecord = ref<FetchedReceipt | null>(null)
const detailVisible = ref(false)
const isEditing = ref(false)
const isSaving = ref(false)

const openDetail = (event: { data: FetchedReceipt }) => {
  selectedRecord.value = JSON.parse(JSON.stringify(event.data)) as FetchedReceipt
  selectedRecord.value.entries.forEach(e => {
    let mIndex = thaiMonths.indexOf(e.month)
    if (mIndex === -1) mIndex = 0
    let y = e.year || new Date().getFullYear() + 543
    if (y > 2500) y -= 543
    e._editDate = new Date(y, mIndex, e.day || 1)
  })
  isEditing.value = false
  detailVisible.value = true
}

const saveEdit = async () => {
  if (!selectedRecord.value) return
  try {
    isSaving.value = true
    selectedRecord.value.entries.forEach(e => {
      if (e._editDate) {
        e.day = e._editDate.getDate()
        e.month = thaiMonths[e._editDate.getMonth()] || ''
        e.year = e._editDate.getFullYear() + 543
      }
      delete e._editDate
    })
    const entryTotal = selectedRecord.value.entries.reduce((s, e) => s + (e.amount || 0), 0)
    selectedRecord.value.totalAmount = entryTotal
    await api.put(`/FuelReceipt/${selectedRecord.value.id}`, {
      entriesJson: JSON.stringify(selectedRecord.value.entries),
      totalAmount: entryTotal,
      declarerName: selectedRecord.value.declarerName || '',
      declarerPosition: selectedRecord.value.declarerPosition || '',
      declarerDept: selectedRecord.value.declarerDept || '',
      note: selectedRecord.value.note || '',
    })
    const index = historyRecords.value.findIndex(r => r.id === selectedRecord.value!.id)
    if (index !== -1) historyRecords.value[index] = { ...historyRecords.value[index]!, ...selectedRecord.value }
    isEditing.value = false
  } catch (err: unknown) {
    alert('เกิดข้อผิดพลาดในการบันทึก: ' + (err instanceof Error ? err.message : String(err)))
  } finally {
    isSaving.value = false
  }
}

const deleteRecord = async () => {
  if (!selectedRecord.value) return
  if (!confirm('ยืนยันการลบข้อมูลนี้?')) return
  await api.delete(`/FuelReceipt/${selectedRecord.value.id}`)
  historyRecords.value = historyRecords.value.filter(r => r.id !== selectedRecord.value!.id)
  detailVisible.value = false
  selectedRecord.value = null
}
</script>

<template>
  <div class="max-w-6xl mx-auto pb-10">
    <div class="mb-6">
      <h2 class="text-2xl font-bold text-gray-800">
        <i class="pi pi-history text-red-500 mr-2"></i>ประวัติใบรับรองแทนใบเสร็จรับเงิน
      </h2>
      <p class="text-gray-500 mt-1">ค้นหาและจัดการใบรับรองแทนใบเสร็จรับเงินทั้งหมด</p>
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
              <label class="text-sm font-semibold text-gray-700">วันที่เริ่มต้น (วันที่บันทึก)</label>
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
            <div class="flex items-end">
              <Button label="ล้างตัวกรอง" icon="pi pi-times" severity="secondary" text @click="clearFilters" />
            </div>
          </div>
        </div>

        <!-- Summary -->
        <div class="bg-red-50 border border-red-100 rounded-xl p-4 flex flex-wrap gap-6 items-center mb-4">
          <div>
            <p class="text-xs text-gray-500">รายการที่แสดง</p>
            <p class="font-bold text-xl text-gray-800">{{ filteredRecords.length }} บิล</p>
          </div>
          <div>
            <p class="text-xs text-gray-500">รวมยอดเงินทั้งหมด</p>
            <p class="font-bold text-xl text-red-600">{{ formatCurrency(totalAmount) }} บาท</p>
          </div>
        </div>

        <!-- Table -->
        <DataTable
          :value="filteredRecords"
          :loading="isLoading"
          paginator :rows="15"
          stripedRows
          emptyMessage="ไม่พบข้อมูล"
          selectionMode="single"
          @row-click="openDetail"
          class="cursor-pointer"
        >
          <Column header="วันที่บันทึก / ผู้บันทึก">
            <template #body="sp">
              <div class="font-semibold text-gray-800">{{ formatDate(sp.data.createdAt) }}</div>
              <div class="text-xs text-gray-500"><i class="pi pi-user mr-1"></i>{{ sp.data.recordedByName }}</div>
            </template>
          </Column>
          <Column v-if="currentUserRole === 'superadmin'" header="หน่วยงาน">
            <template #body="sp">{{ getDeptName(sp.data.departmentId) }}</template>
          </Column>
          <Column header="จำนวนรายการ">
            <template #body="sp">
              <span class="font-semibold">{{ sp.data.entries?.length || 0 }}</span> รายการ
            </template>
          </Column>
          <Column header="รวมยอดเงิน">
            <template #body="sp">
              <span class="font-bold text-red-600 text-lg">{{ formatCurrency(sp.data.totalAmount) }}</span>
            </template>
          </Column>
          <Column header="" style="width:3rem">
            <template #body><i class="pi pi-chevron-right text-gray-400"></i></template>
          </Column>
        </DataTable>
      </template>
    </Card>

    <!-- Detail Dialog -->
    <Dialog v-model:visible="detailVisible" modal header="รายละเอียดบันทึกรายจ่าย"
      :style="{ width: '800px' }" :draggable="false">
      <div v-if="selectedRecord" class="flex flex-col gap-4">
        <div class="grid grid-cols-2 gap-3 text-sm">
          <div class="bg-gray-50 rounded-lg p-3">
            <p class="text-gray-500 text-xs mb-1">วันที่บันทึก</p>
            <p class="font-semibold">{{ formatDate(selectedRecord.createdAt) }}</p>
          </div>
          <div class="bg-gray-50 rounded-lg p-3">
            <p class="text-gray-500 text-xs mb-1">ผู้บันทึก</p>
            <p class="font-semibold">{{ selectedRecord.recordedByName }}</p>
          </div>
        </div>

        <!-- Viewing Mode -->
        <table v-if="!isEditing" class="w-full text-sm border-collapse">
          <thead>
            <tr class="bg-gray-50">
              <th class="border px-2 py-1.5 text-center">วัน</th>
              <th class="border px-2 py-1.5 text-center">เดือน</th>
              <th class="border px-2 py-1.5 text-center">ปี</th>
              <th class="border px-2 py-1.5">รายละเอียด</th>
              <th class="border px-2 py-1.5 text-right">จำนวนเงิน</th>
              <th class="border px-2 py-1.5 text-center">พขร.</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(e, i) in selectedRecord.entries" :key="i">
              <td class="border px-2 py-1.5 text-center">{{ e.day }}</td>
              <td class="border px-2 py-1.5 text-center">{{ e.month }}</td>
              <td class="border px-2 py-1.5 text-center">{{ e.year }}</td>
              <td class="border px-2 py-1.5">
                {{ e.detail }}
                <span v-if="e.receiptNo" class="text-gray-500 text-xs text-nowrap"> เลขที่ {{ e.receiptNo }}</span>
                <span v-if="e.bookNo" class="text-gray-500 text-xs text-nowrap"> เล่มที่ {{ e.bookNo }}</span>
              </td>
              <td class="border px-2 py-1.5 text-right font-semibold">{{ formatCurrency(e.amount) }}</td>
              <td class="border px-2 py-1.5 text-center">{{ e.driverName }}</td>
            </tr>
          </tbody>
          <tfoot>
            <tr class="bg-red-50">
              <td colspan="4" class="border px-2 py-1.5 text-center font-bold">รวมทั้งสิ้น</td>
              <td class="border px-2 py-1.5 text-right font-bold text-red-700">{{ formatCurrency(selectedRecord.totalAmount) }}</td>
              <td class="border px-2 py-1.5"></td>
            </tr>
          </tfoot>
        </table>

        <!-- Editing Mode -->
        <div v-else class="flex flex-col gap-4">
          <div v-for="(e, i) in selectedRecord.entries" :key="i" class="p-4 rounded-xl border border-blue-100 bg-blue-50/30 flex flex-col gap-4">
            <h4 class="font-bold text-blue-800 border-b border-blue-100 pb-2">
              <i class="pi pi-receipt mr-2"></i>แก้ไขรายการจ่าย <span v-if="selectedRecord.entries.length > 1">#{{ i+1 }}</span>
            </h4>
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 items-end">
              <div class="flex flex-col gap-2">
                <label class="font-semibold text-sm text-gray-700">วันที่ <span class="text-red-500">*</span></label>
                <DatePicker v-model="e._editDate" dateFormat="dd/mm/yy" class="w-full" showIcon />
              </div>
              <div class="flex flex-col gap-2 lg:col-span-2">
                <label class="font-semibold text-sm text-gray-700">รายละเอียดรายจ่าย <span class="text-red-500">*</span></label>
                <InputText v-model="e.detail" placeholder="ค่าผ่านทางพิเศษ" class="w-full" />
              </div>
              <div class="flex flex-col gap-2">
                <label class="font-semibold text-sm text-gray-700">พขร. / ผู้จ่ายเงิน</label>
                <InputText v-model="e.driverName" placeholder="ชื่อคนขับ" class="w-full" />
              </div>
              <div class="flex flex-col gap-2">
                <label class="font-semibold text-sm text-gray-700">เลขที่</label>
                <InputText v-model="e.receiptNo" placeholder="เลขที่อ้างอิง" class="w-full" />
              </div>
              <div class="flex flex-col gap-2">
                <label class="font-semibold text-sm text-gray-700">เล่มที่</label>
                <InputText v-model="e.bookNo" placeholder="เล่มที่อ้างอิง" class="w-full" />
              </div>
              <div class="flex flex-col gap-2 lg:col-span-2">
                <label class="font-semibold text-sm text-gray-700">จำนวนเงิน (บาท) <span class="text-red-500">*</span></label>
                <InputNumber v-model="e.amount" :minFractionDigits="2" :maxFractionDigits="2" placeholder="0.00" class="w-full" :inputProps="{ class: 'font-bold text-lg text-blue-700' }" />
              </div>
            </div>
          </div>
          <div class="flex items-center justify-between p-3 rounded-xl bg-red-50 border border-red-100">
            <span class="font-bold text-gray-700">ยอดรวมสุทธิ</span>
            <span class="font-bold text-red-700 text-lg">{{ formatCurrency(selectedRecord.entries.reduce((a,c) => a + (c.amount || 0), 0)) }} บาท</span>
          </div>
        </div>

        <div v-if="selectedRecord.note || isEditing" class="bg-amber-50 rounded-lg p-3 border border-amber-100 text-sm">
          <p class="text-gray-500 text-xs mb-1">หมายเหตุ</p>
          <p v-if="!isEditing">{{ selectedRecord.note }}</p>
          <Textarea v-else v-model="selectedRecord.note" rows="2" class="w-full text-xs p-2" />
        </div>
      </div>
      <template #footer>
        <template v-if="!isEditing">
          <Button label="แก้ไข" icon="pi pi-file-edit" severity="info" text @click="isEditing = true" />
          <Button label="ลบ" icon="pi pi-trash" severity="danger" text @click="deleteRecord" />
          <Button label="ปิด" severity="secondary" text @click="detailVisible = false" />
        </template>
        <template v-else>
          <Button label="บันทึก" icon="pi pi-save" severity="success" :loading="isSaving" @click="saveEdit" />
          <Button label="ยกเลิก" severity="secondary" text @click="isEditing = false" />
        </template>
      </template>
    </Dialog>
  </div>
</template>

<style scoped>
:deep(.p-datatable-header-cell) {
  background-color: #f8fafc !important;
  color: #475569 !important;
  font-weight: 700 !important;
}
</style>
