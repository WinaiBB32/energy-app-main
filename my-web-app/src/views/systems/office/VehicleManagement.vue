<template>
  <div class="p-4 space-y-4">
    <!-- Header -->
    <div class="flex flex-wrap justify-between items-center gap-2">
      <div>
        <h2 class="text-xl font-bold text-gray-800">ระบบบันทึกข้อมูลรถยนต์สำนักงานฯ</h2>
        <p class="text-sm text-gray-500 mt-0.5">1 ท่าน สามารถลงทะเบียนรถยนต์ได้สูงสุด 5 คัน</p>
      </div>
      <div v-if="isOfficer" class="flex gap-2">
        <Button label="นำเข้า CSV" icon="pi pi-upload" severity="secondary" @click="csvDialogVisible = true" />
        <Button label="เพิ่มรายการ" icon="pi pi-plus" @click="openAdd" />
      </div>
    </div>

    <!-- Search -->
    <InputText v-model="search" placeholder="ค้นหา ชื่อ / รหัสสแกน / ทะเบียน / หน่วยงาน..." class="w-full md:w-96" />

    <!-- Table -->
    <DataTable
      :value="filtered"
      :loading="store.isLoading"
      paginator
      :rows="15"
      dataKey="id"
      class="p-datatable-sm"
      responsiveLayout="scroll"
    >
      <template #empty>ไม่พบข้อมูล</template>

      <Column field="id" header="รหัส" style="width: 70px" />
      <Column header="รหัสสแกนหน้า" field="faceScanId" sortable style="width: 130px">
        <template #body="{ data }">
          <span class="font-mono text-sm font-semibold text-indigo-700 bg-indigo-50 px-2 py-0.5 rounded">{{ data.faceScanId }}</span>
        </template>
      </Column>
      <Column header="ชื่อ - นามสกุล" field="fullName" sortable>
        <template #body="{ data }">
          <div class="font-medium">{{ data.fullName }}</div>
        </template>
      </Column>
      <Column header="ตำแหน่ง / หน่วยงาน">
        <template #body="{ data }">
          <div>{{ data.position }}</div>
          <div class="text-xs text-gray-400">{{ data.department }}</div>
        </template>
      </Column>
      <Column field="phoneNumber" header="เบอร์ติดต่อ" />
      <Column header="ทะเบียนรถ">
        <template #body="{ data }">
          <span class="font-mono font-semibold text-blue-700">{{ data.licensePlate }}</span>
          <span class="text-xs text-gray-400 ml-1">({{ data.province }})</span>
        </template>
      </Column>
      <Column header="ยี่ห้อ / รุ่น">
        <template #body="{ data }">{{ data.brand }} {{ data.model }}</template>
      </Column>
      <Column header="รถของท่าน">
        <template #body="{ data }">
          <span
            class="text-xs font-semibold px-2 py-0.5 rounded-full"
            :class="vehicleCountOf(data.faceScanId) >= 5 ? 'bg-red-100 text-red-700' : 'bg-green-100 text-green-700'"
          >
            {{ vehicleCountOf(data.faceScanId) }} / 5 คัน
          </span>
        </template>
      </Column>
      <Column header="" :style="isOfficer ? 'width: 130px' : 'width: 60px'">
        <template #body="{ data }">
          <div class="flex gap-1">
            <Button icon="pi pi-eye" text rounded size="small" severity="secondary" v-tooltip.top="'ดูรายละเอียด'" @click="router.push(`/vehicle/person/${data.faceScanId}`)" />
            <template v-if="isOfficer">
              <Button icon="pi pi-pencil" text rounded size="small" severity="info" @click="openEdit(data)" />
              <Button icon="pi pi-trash" text rounded size="small" severity="danger" @click="confirmDelete(data)" />
            </template>
          </div>
        </template>
      </Column>
    </DataTable>

    <!-- Add / Edit Dialog -->
    <Dialog
      v-model:visible="dialogVisible"
      :header="editTarget ? 'แก้ไขข้อมูลรถยนต์' : 'เพิ่มข้อมูลรถยนต์'"
      :style="{ width: '560px' }"
      modal
    >
      <div class="grid grid-cols-2 gap-4 pt-2">
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium">รหัสสแกนหน้า</label>
          <InputText v-model="form.faceScanId" placeholder="เช่น EMP001 (ว่างได้สำหรับบุคคลภายนอก)" />
        </div>
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium">ชื่อ - นามสกุล *</label>
          <InputText v-model="form.fullName" />
        </div>
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium">ตำแหน่ง</label>
          <InputText v-model="form.position" />
        </div>
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium">หน่วยงาน</label>
          <Select
            v-model="form.department"
            :options="masterStore.departments"
            optionLabel="name"
            optionValue="name"
            placeholder="เลือกหน่วยงาน"
            class="w-full"
            filter
            filterPlaceholder="ค้นหา..."
            showClear
          />
        </div>
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium">เบอร์ติดต่อ</label>
          <InputText v-model="form.phoneNumber" />
        </div>
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium">ทะเบียนรถ *</label>
          <InputText v-model="form.licensePlate" placeholder="เช่น กข 1234" />
        </div>
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium">ยี่ห้อ</label>
          <InputText v-model="form.brand" placeholder="เช่น Toyota" />
        </div>
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium">รุ่น</label>
          <InputText v-model="form.model" placeholder="เช่น Fortuner" />
        </div>
        <div class="flex flex-col gap-1 col-span-2">
          <label class="text-sm font-medium">จังหวัด</label>
          <Select
            v-model="form.province"
            :options="masterStore.provinces"
            optionLabel="name"
            optionValue="name"
            placeholder="เลือกจังหวัด"
            class="w-full"
            filter
            filterPlaceholder="ค้นหา..."
            showClear
          />
        </div>
      </div>
      <p v-if="store.error" class="mt-3 text-sm text-red-600">{{ store.error }}</p>
      <template #footer>
        <Button label="ยกเลิก" text @click="dialogVisible = false" />
        <Button
          :label="editTarget ? 'บันทึกการแก้ไข' : 'เพิ่มรายการ'"
          icon="pi pi-check"
          :loading="store.isLoading"
          @click="submitForm"
        />
      </template>
    </Dialog>

    <!-- CSV Import Dialog -->
    <Dialog v-model:visible="csvDialogVisible" header="นำเข้าข้อมูลจากไฟล์ CSV" :style="{ width: '520px' }" modal>
      <div class="space-y-4 pt-2">
        <div class="bg-blue-50 border border-blue-200 rounded-lg p-3 text-sm text-blue-800">
          <div class="flex justify-between items-start gap-2">
            <div>
              <p class="font-semibold mb-1">รูปแบบไฟล์ CSV (แถวแรกเป็น Header)</p>
              <p class="font-mono text-xs break-all">FaceScanId,FullName,Position,Department,PhoneNumber,LicensePlate,Brand,Model,Province</p>
            </div>
            <Button label="Template" icon="pi pi-download" size="small" severity="secondary" outlined @click="downloadTemplate" />
          </div>
        </div>
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium">เลือกไฟล์ CSV</label>
          <input
            ref="csvFileInput"
            type="file"
            accept=".csv"
            class="block w-full text-sm text-gray-500 file:mr-3 file:py-1.5 file:px-3 file:rounded file:border-0 file:text-sm file:font-medium file:bg-indigo-50 file:text-indigo-700 hover:file:bg-indigo-100"
            @change="onCsvFileChange"
          />
        </div>
        <div v-if="csvResult" class="text-sm space-y-1">
          <p class="text-green-700 font-semibold">นำเข้าสำเร็จ {{ csvResult.imported }} รายการ</p>
          <p v-if="csvResult.skipped > 0" class="text-orange-600">ข้าม {{ csvResult.skipped }} รายการ</p>
          <ul v-if="csvResult.errors?.length" class="text-red-600 list-disc list-inside max-h-32 overflow-y-auto">
            <li v-for="(e, i) in csvResult.errors" :key="i">{{ e }}</li>
          </ul>
        </div>
      </div>
      <template #footer>
        <Button label="ปิด" text @click="closeCsvDialog" />
        <Button label="นำเข้า" icon="pi pi-upload" :loading="csvLoading" :disabled="!csvFile" @click="submitCsv" />
      </template>
    </Dialog>

    <!-- Confirm Delete -->
    <ConfirmDialog />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useConfirm } from 'primevue/useconfirm'
import { useVehicleStore } from '@/stores/vehicleStore'
import { useVehicleMasterStore } from '@/stores/vehicleMasterStore'
import { usePermissions } from '@/composables/usePermissions'
import { useAppToast } from '@/composables/useAppToast'
import type { VehicleRecord, VehicleRecordCreatePayload } from '@/types/vehicle'
import api from '@/services/api'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import ConfirmDialog from 'primevue/confirmdialog'

const router = useRouter()
const store = useVehicleStore()
const masterStore = useVehicleMasterStore()
const confirm = useConfirm()
const { success: showSuccess, error: showError } = useAppToast()
const { isSystemAdmin } = usePermissions()

const isOfficer = computed(() => isSystemAdmin('system11'))

const search = ref('')
const dialogVisible = ref(false)
const editTarget = ref<VehicleRecord | null>(null)

const emptyForm = (): VehicleRecordCreatePayload => ({
  faceScanId: '', fullName: '', position: '', department: '',
  phoneNumber: '', licensePlate: '', brand: '', model: '', province: '',
})
const form = ref(emptyForm())

const filtered = computed(() => {
  const q = search.value.toLowerCase()
  if (!q) return store.records
  return store.records.filter(
    (v) =>
      v.fullName.toLowerCase().includes(q) ||
      v.faceScanId.toLowerCase().includes(q) ||
      v.licensePlate.toLowerCase().includes(q) ||
      v.department.toLowerCase().includes(q),
  )
})

function vehicleCountOf(faceScanId: string): number {
  return store.records.filter((v) => v.faceScanId === faceScanId).length
}

function openAdd() {
  editTarget.value = null
  form.value = emptyForm()
  store.error = null
  dialogVisible.value = true
}

function openEdit(record: VehicleRecord) {
  editTarget.value = record
  form.value = {
    faceScanId: record.faceScanId,
    fullName: record.fullName,
    position: record.position,
    department: record.department,
    phoneNumber: record.phoneNumber,
    licensePlate: record.licensePlate,
    brand: record.brand,
    model: record.model,
    province: record.province,
  }
  store.error = null
  dialogVisible.value = true
}

async function submitForm() {
  if (!form.value.fullName || !form.value.licensePlate) {
    store.error = 'กรุณากรอกข้อมูลที่จำเป็น: ชื่อ-นามสกุล, ทะเบียนรถ'
    return
  }
  const ok = editTarget.value
    ? await store.updateRecord(editTarget.value.id, form.value)
    : await store.createRecord(form.value)

  if (ok) {
    showSuccess(editTarget.value ? 'แก้ไขข้อมูลสำเร็จ' : 'เพิ่มข้อมูลสำเร็จ')
    dialogVisible.value = false
  } else {
    showError(store.error ?? 'เกิดข้อผิดพลาด')
  }
}

function confirmDelete(record: VehicleRecord) {
  confirm.require({
    message: `ยืนยันลบข้อมูลรถ ${record.licensePlate} ของ ${record.fullName}?`,
    header: 'ยืนยันการลบ',
    icon: 'pi pi-exclamation-triangle',
    acceptClass: 'p-button-danger',
    acceptLabel: 'ลบ',
    rejectLabel: 'ยกเลิก',
    accept: async () => {
      const ok = await store.deleteRecord(record.id)
      if (ok) showSuccess('ลบข้อมูลสำเร็จ')
      else showError(store.error ?? 'เกิดข้อผิดพลาด')
    },
  })
}

// CSV import
const csvDialogVisible = ref(false)
const csvFile = ref<File | null>(null)
const csvFileInput = ref<HTMLInputElement | null>(null)
const csvLoading = ref(false)
const csvResult = ref<{ imported: number; skipped: number; errors: string[] } | null>(null)

function downloadTemplate() {
  const header = 'FaceScanId,FullName,Position,Department,PhoneNumber,LicensePlate,Brand,Model,Province'
  const example = 'EMP001,สมชาย ใจดี,นักวิชาการ,กองพัสดุ,0812345678,กข 1234,Toyota,Fortuner,กรุงเทพมหานคร'
  const blob = new Blob(['\ufeff' + header + '\n' + example], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = 'vehicle_template.csv'
  a.click()
  URL.revokeObjectURL(url)
}

function onCsvFileChange(e: Event) {
  const target = e.target as HTMLInputElement
  csvFile.value = target.files?.[0] ?? null
  csvResult.value = null
}

function closeCsvDialog() {
  csvDialogVisible.value = false
  csvFile.value = null
  csvResult.value = null
  if (csvFileInput.value) csvFileInput.value.value = ''
}

async function submitCsv() {
  if (!csvFile.value) return
  csvLoading.value = true
  csvResult.value = null
  try {
    const fd = new FormData()
    fd.append('file', csvFile.value)
    const res = await api.post('/office/vehicles/import-csv', fd)
    csvResult.value = res.data
    if (res.data.imported > 0) {
      showSuccess(`นำเข้าสำเร็จ ${res.data.imported} รายการ`)
      await store.fetchRecords()
    }
  } catch (err: any) {
    showError(err?.response?.data?.message ?? 'นำเข้าไม่สำเร็จ')
  } finally {
    csvLoading.value = false
  }
}

onMounted(() => {
  store.fetchRecords()
  masterStore.fetchDepartments()
  masterStore.fetchProvinces()
})
</script>
