<template>
  <div class="p-4 space-y-4">
    <!-- Back -->
    <Button icon="pi pi-arrow-left" label="กลับ" text @click="router.back()" />

    <!-- Person card -->
    <div v-if="person" class="bg-white rounded-xl border border-gray-100 shadow-sm p-5">
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div class="flex items-center gap-4">
          <div class="w-14 h-14 rounded-full bg-indigo-100 flex items-center justify-center shrink-0">
            <i class="pi pi-user text-indigo-600 text-2xl"></i>
          </div>
          <div>
            <h2 class="text-xl font-bold text-gray-800">{{ person.fullName }}</h2>
            <p class="text-sm text-gray-500">{{ person.position }}</p>
            <p class="text-sm text-gray-500">{{ person.department }}</p>
          </div>
        </div>
        <div class="flex flex-col gap-1 text-sm text-gray-600">
          <div class="flex items-center gap-2">
            <i class="pi pi-id-card text-gray-400"></i>
            <span class="font-mono">{{ person.faceScanId }}</span>
          </div>
          <div class="flex items-center gap-2">
            <i class="pi pi-phone text-gray-400"></i>
            <span>{{ person.phoneNumber || '-' }}</span>
          </div>
        </div>
        <span
          class="text-sm font-semibold px-3 py-1 rounded-full"
          :class="vehicles.length >= 5 ? 'bg-red-100 text-red-700' : 'bg-green-100 text-green-700'"
        >
          {{ vehicles.length }} / 5 คัน
        </span>
      </div>
    </div>

    <!-- Not found -->
    <div v-else-if="!store.isLoading" class="text-center py-12 text-gray-400">
      <i class="pi pi-exclamation-circle text-4xl mb-3 block"></i>
      ไม่พบข้อมูลบุคคลนี้
    </div>

    <!-- Vehicles -->
    <div v-if="vehicles.length" class="space-y-3">
      <h3 class="text-base font-semibold text-gray-700">รายการรถยนต์ที่ลงทะเบียน</h3>
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        <div
          v-for="v in vehicles"
          :key="v.id"
          class="bg-white rounded-xl border border-gray-100 shadow-sm p-4 space-y-2"
        >
          <div class="flex items-center justify-between">
            <span class="font-mono text-lg font-bold text-blue-700">{{ v.licensePlate }}</span>
            <span class="text-xs text-gray-400"># {{ v.id }}</span>
          </div>
          <p class="text-sm text-gray-700 font-medium">{{ v.brand }} {{ v.model }}</p>
          <div class="flex items-center gap-1 text-xs text-gray-500">
            <i class="pi pi-map-marker text-gray-300"></i>
            {{ v.province || '-' }}
          </div>
          <div class="pt-1 border-t border-gray-50 text-xs text-gray-400">
            ลงทะเบียน {{ formatDate(v.createdAtUtc) }}
          </div>
          <div v-if="isOfficer" class="flex gap-1 pt-1">
            <Button icon="pi pi-pencil" text rounded size="small" severity="info" label="แก้ไข" @click="openEdit(v)" />
            <Button icon="pi pi-trash" text rounded size="small" severity="danger" label="ลบ" @click="confirmDelete(v)" />
          </div>
        </div>
      </div>
    </div>

    <!-- Add button -->
    <div v-if="isOfficer && person">
      <Button
        icon="pi pi-plus"
        label="เพิ่มรถยนต์ให้บุคคลนี้"
        :disabled="vehicles.length >= 5"
        @click="openAdd"
      />
      <span v-if="vehicles.length >= 5" class="ml-3 text-sm text-red-500">ครบ 5 คันแล้ว</span>
    </div>

    <!-- Add / Edit Dialog -->
    <Dialog
      v-model:visible="dialogVisible"
      :header="editTarget ? 'แก้ไขข้อมูลรถยนต์' : 'เพิ่มรถยนต์'"
      :style="{ width: '560px' }"
      modal
    >
      <div class="grid grid-cols-2 gap-4 pt-2">
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
        <div class="flex flex-col gap-1">
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
      <p v-if="vehicleStore.error" class="mt-3 text-sm text-red-600">{{ vehicleStore.error }}</p>
      <template #footer>
        <Button label="ยกเลิก" text @click="dialogVisible = false" />
        <Button
          :label="editTarget ? 'บันทึกการแก้ไข' : 'เพิ่ม'"
          icon="pi pi-check"
          :loading="vehicleStore.isLoading"
          @click="submitForm"
        />
      </template>
    </Dialog>

    <ConfirmDialog />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useConfirm } from 'primevue/useconfirm'
import { useVehicleStore } from '@/stores/vehicleStore'
import { useVehicleMasterStore } from '@/stores/vehicleMasterStore'
import { usePermissions } from '@/composables/usePermissions'
import { useAppToast } from '@/composables/useAppToast'
import type { VehicleRecord, VehicleRecordCreatePayload } from '@/types/vehicle'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import ConfirmDialog from 'primevue/confirmdialog'

const route = useRoute()
const router = useRouter()
const confirm = useConfirm()
const vehicleStore = useVehicleStore()
const masterStore = useVehicleMasterStore()
const { success: showSuccess, error: showError } = useAppToast()
const { isSystemAdmin } = usePermissions()

const isOfficer = computed(() => isSystemAdmin('system11'))
const faceScanId = computed(() => route.params.faceScanId as string)

const vehicles = computed(() =>
  vehicleStore.records.filter((v) => v.faceScanId === faceScanId.value),
)
const person = computed(() => vehicles.value[0] ?? null)
const store = vehicleStore

// ─── Dialog ──────────────────────────────────────────────────
const dialogVisible = ref(false)
const editTarget = ref<VehicleRecord | null>(null)

const emptyForm = (): VehicleRecordCreatePayload => ({
  faceScanId: faceScanId.value,
  fullName: person.value?.fullName ?? '',
  position: person.value?.position ?? '',
  department: person.value?.department ?? '',
  phoneNumber: person.value?.phoneNumber ?? '',
  licensePlate: '',
  brand: '',
  model: '',
  province: '',
})

const form = ref<VehicleRecordCreatePayload>(emptyForm())

function openAdd() {
  editTarget.value = null
  form.value = emptyForm()
  vehicleStore.error = null
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
  vehicleStore.error = null
  dialogVisible.value = true
}

async function submitForm() {
  if (!form.value.licensePlate) {
    vehicleStore.error = 'กรุณากรอกทะเบียนรถ'
    return
  }
  const ok = editTarget.value
    ? await vehicleStore.updateRecord(editTarget.value.id, form.value)
    : await vehicleStore.createRecord(form.value)

  if (ok) {
    showSuccess(editTarget.value ? 'แก้ไขข้อมูลสำเร็จ' : 'เพิ่มรถยนต์สำเร็จ')
    dialogVisible.value = false
  } else {
    showError(vehicleStore.error ?? 'เกิดข้อผิดพลาด')
  }
}

function confirmDelete(record: VehicleRecord) {
  confirm.require({
    message: `ยืนยันลบรถ ${record.licensePlate}?`,
    header: 'ยืนยันการลบ',
    icon: 'pi pi-exclamation-triangle',
    acceptClass: 'p-button-danger',
    acceptLabel: 'ลบ',
    rejectLabel: 'ยกเลิก',
    accept: async () => {
      const ok = await vehicleStore.deleteRecord(record.id)
      if (ok) showSuccess('ลบสำเร็จ')
      else showError(vehicleStore.error ?? 'เกิดข้อผิดพลาด')
    },
  })
}

function formatDate(utcStr: string): string {
  const d = new Date(utcStr.endsWith('Z') ? utcStr : utcStr + 'Z')
  return d.toLocaleDateString('th-TH', { year: 'numeric', month: 'short', day: 'numeric' })
}

onMounted(() => {
  vehicleStore.fetchRecords()
  masterStore.fetchProvinces()
})
</script>
