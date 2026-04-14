<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import { usePermissions } from '@/composables/usePermissions'
import { useAppToast } from '@/composables/useAppToast'

import Card from 'primevue/card'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import DatePicker from 'primevue/datepicker'
import Select from 'primevue/select'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Textarea from 'primevue/textarea'

defineOptions({ name: 'FuelView' })

interface FuelRecord {
  departmentId: string
  refuelDate: Date | null
  documentType: string
  documentNumber: string
  vehiclePlate: string
  vehicleProvince: string
  purchaserName: string
  fuelType: string
  liters: number | null
  totalAmount: number | null
  gasStationCompany: string
  note: string
}

interface Department { id: string; name: string }
interface FuelType { id: string; name: string; severity: string }

const authStore = useAuthStore()
const toast = useAppToast()
const { isSuperAdmin } = usePermissions()
const currentUserDepartment = computed(() => authStore.userProfile?.departmentId || '')

const departments = ref<Department[]>([])
const fuelTypes = ref<FuelType[]>([])
const documentTypes = ref(['ใบสั่งซื้อ', 'ใบเสร็จรับเงิน', 'ใบกำกับภาษี'])
const provinces = ref([
  'กรุงเทพมหานคร', 'นนทบุรี', 'ปทุมธานี', 'สมุทรปราการ',
  'เชียงใหม่', 'ขอนแก่น', 'นครราชสีมา', 'อุดรธานี',
])

const formData = ref<FuelRecord>({
  departmentId: isSuperAdmin.value ? '' : currentUserDepartment.value,
  refuelDate: null,
  documentType: 'ใบเสร็จรับเงิน',
  documentNumber: '',
  vehiclePlate: '',
  vehicleProvince: 'นนทบุรี',
  purchaserName: '',
  fuelType: '',
  liters: null,
  totalAmount: null,
  gasStationCompany: '',
  note: '',
})

const isSubmitting = ref(false)
const successMessage = ref('')
const errorMessage = ref('')

const computedPricePerLiter = computed<number>(() => {
  if (formData.value.liters && formData.value.totalAmount && formData.value.liters > 0) {
    return formData.value.totalAmount / formData.value.liters
  }
  return 0
})

const getDeptName = (id: string) => departments.value.find((x) => x.id === id)?.name || id

onMounted(async () => {
  try {
    const [deptsRes, fuelTypesRes] = await Promise.all([
      api.get('/Department'),
      api.get('/FuelType'),
    ])
    departments.value = deptsRes.data
    fuelTypes.value = fuelTypesRes.data
  } catch (error: unknown) {
    toast.fromError(error, 'ไม่สามารถโหลดข้อมูลหน่วยงาน/ประเภทน้ำมันได้')
  }
})

const resetForm = () => {
  formData.value = {
    departmentId: isSuperAdmin.value ? '' : currentUserDepartment.value,
    refuelDate: null,
    documentType: 'ใบเสร็จรับเงิน',
    documentNumber: '',
    vehiclePlate: '',
    vehicleProvince: 'นนทบุรี',
    purchaserName: '',
    fuelType: '',
    liters: null,
    totalAmount: null,
    gasStationCompany: '',
    note: '',
  }
}

const submitForm = async () => {
  successMessage.value = ''
  errorMessage.value = ''
  if (
    !formData.value.refuelDate ||
    !formData.value.vehiclePlate ||
    !formData.value.fuelType ||
    formData.value.liters === null ||
    formData.value.totalAmount === null
  ) {
    errorMessage.value = 'กรุณากรอกข้อมูลที่จำเป็น (*) ให้ครบถ้วน'
    return
  }
  try {
    isSubmitting.value = true
    const saveDepartmentId = isSuperAdmin.value
      ? formData.value.departmentId
      : currentUserDepartment.value

    await api.post('/FuelRecord', {
      departmentId: saveDepartmentId,
      refuelDate: formData.value.refuelDate ? new Date(formData.value.refuelDate).toISOString() : null,
      documentType: formData.value.documentType,
      documentNumber: formData.value.documentNumber,
      vehiclePlate: formData.value.vehiclePlate,
      vehicleProvince: formData.value.vehicleProvince,
      purchaserName: formData.value.purchaserName,
      fuelTypeName: fuelTypes.value.find(f => f.id === formData.value.fuelType)?.name ?? formData.value.fuelType,
      liters: formData.value.liters,
      totalAmount: formData.value.totalAmount,
      gasStationCompany: formData.value.gasStationCompany,
      note: formData.value.note,
      recordedBy: authStore.user?.uid || '',
    })

    successMessage.value = 'บันทึกข้อมูลการเติมน้ำมันสำเร็จ'
    resetForm()
  } catch (error: unknown) {
    errorMessage.value = error instanceof Error ? `เกิดข้อผิดพลาด: ${error.message}` : 'เกิดข้อผิดพลาด'
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
          <i class="pi pi-file-edit text-red-500 mr-2"></i>บันทึกน้ำมันเชื้อเพลิง
        </h2>
        <p class="text-gray-500 mt-1">กรอกข้อมูลการเติมน้ำมันแต่ละครั้ง</p>
      </div>
    </div>

    <Card class="shadow-sm border-none">
      <template #content>
        <form @submit.prevent="submitForm" class="flex flex-col gap-8">
          <Message v-if="successMessage" severity="success" :closable="true">{{ successMessage }}</Message>
          <Message v-if="errorMessage" severity="error" :closable="true">{{ errorMessage }}</Message>

          <!-- เอกสารอ้างอิง -->
          <div>
            <h3 class="font-bold text-gray-700 border-b pb-2 mb-4 text-lg">
              <i class="pi pi-file mr-2 text-red-500"></i>ข้อมูลเอกสารอ้างอิง
            </h3>
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
              <div class="flex flex-col gap-2">
                <label class="font-semibold text-sm text-gray-700">หน่วยงาน <span class="text-red-500">*</span></label>
                <Select v-if="isSuperAdmin" v-model="formData.departmentId" :options="departments" optionLabel="name"
                  optionValue="id" placeholder="-- เลือกหน่วยงาน --" class="w-full" />
                <InputText v-else :value="getDeptName(currentUserDepartment)" disabled
                  class="w-full bg-gray-100 font-bold" />
              </div>
              <div class="flex flex-col gap-2">
                <label class="font-semibold text-sm text-gray-700">วันที่ <span class="text-red-500">*</span></label>
                <DatePicker v-model="formData.refuelDate" dateFormat="dd/mm/yy" class="w-full" showIcon />
              </div>
              <div class="flex flex-col gap-2">
                <label class="font-semibold text-sm text-gray-700">ประเภทเอกสาร <span
                    class="text-red-500">*</span></label>
                <Select v-model="formData.documentType" :options="documentTypes" class="w-full" />
              </div>
              <div class="flex flex-col gap-2">
                <label class="font-semibold text-sm text-gray-700">เลขที่เอกสาร</label>
                <InputText v-model="formData.documentNumber" placeholder="เช่น เล่มที่ 1 เลขที่ 10" class="w-full" />
              </div>
            </div>
          </div>

          <!-- ข้อมูลรถ -->
          <div>
            <h3 class="font-bold text-gray-700 border-b pb-2 mb-4 text-lg">
              <i class="pi pi-car mr-2 text-red-500"></i>ข้อมูลรถยนต์และบุคลากร
            </h3>
            <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div class="flex flex-col gap-2">
                <label class="font-semibold text-sm text-gray-700">เลขทะเบียนรถ <span
                    class="text-red-500">*</span></label>
                <InputText v-model="formData.vehiclePlate" placeholder="เช่น 1กข 1234" class="w-full" />
              </div>
              <div class="flex flex-col gap-2">
                <label class="font-semibold text-sm text-gray-700">จังหวัด</label>
                <Select v-model="formData.vehicleProvince" :options="provinces" filter class="w-full" />
              </div>
              <div class="flex flex-col gap-2">
                <label class="font-semibold text-sm text-gray-700">ผู้จัดซื้อน้ำมันเชื้อเพลิง</label>
                <InputText v-model="formData.purchaserName" placeholder="ชื่อ-นามสกุล" class="w-full" />
              </div>
            </div>
          </div>

          <!-- รายละเอียดการเติมน้ำมัน -->
          <div class="bg-red-50/30 p-4 rounded-lg border border-red-100">
            <h3 class="font-bold text-red-800 border-b border-red-200 pb-2 mb-4 text-lg">
              <i class="pi pi-gauge mr-2"></i>รายละเอียดการเติมน้ำมัน
            </h3>
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 items-end">
              <div class="flex flex-col gap-2">
                <label class="font-semibold text-sm text-gray-700">ประเภทน้ำมัน <span
                    class="text-red-500">*</span></label>
                <Select v-model="formData.fuelType" :options="fuelTypes" optionLabel="name" optionValue="id"
                  placeholder="-- เลือกประเภท --" class="w-full" />
              </div>
              <div class="flex flex-col gap-2">
                <label class="font-semibold text-sm text-gray-700">บริษัทจำหน่ายน้ำมัน</label>
                <InputText v-model="formData.gasStationCompany" placeholder="เช่น ปตท., บางจาก" class="w-full" />
              </div>
              <div class="flex flex-col gap-2">
                <label class="font-semibold text-sm text-gray-700">ปริมาณ (ลิตร) <span
                    class="text-red-500">*</span></label>
                <InputNumber v-model="formData.liters" :minFractionDigits="0" :maxFractionDigits="2" placeholder="0.00"
                  suffix=" ลิตร" class="w-full" />
              </div>
              <div class="flex flex-col gap-2">
                <label class="font-semibold text-sm text-gray-700">วงเงินรวม (บาท) <span
                    class="text-red-500">*</span></label>
                <InputNumber v-model="formData.totalAmount" mode="currency" currency="THB" locale="th-TH"
                  placeholder="฿ 0.00" class="w-full" />
              </div>
              <div class="lg:col-span-4 flex justify-end">
                <div class="text-sm bg-white border border-gray-200 px-4 py-2 rounded-md shadow-sm">
                  <span class="text-gray-500 mr-2">ราคาเฉลี่ยต่อลิตร:</span>
                  <span class="font-bold text-red-600">
                    {{ computedPricePerLiter > 0 ? computedPricePerLiter.toFixed(2) : '0.00' }} บาท/ลิตร
                  </span>
                </div>
              </div>
            </div>
          </div>

          <!-- หมายเหตุ -->
          <div class="flex flex-col gap-2">
            <label class="font-semibold text-sm text-gray-700">หมายเหตุ</label>
            <Textarea v-model="formData.note" rows="2" placeholder="รายละเอียดเพิ่มเติม (ถ้ามี)..." class="w-full" />
          </div>

          <div class="flex justify-end mt-2 pt-4 border-t border-gray-100">
            <Button type="submit" label="บันทึกข้อมูลน้ำมัน" icon="pi pi-save" severity="danger" :loading="isSubmitting"
              class="px-8 py-3 text-lg" />
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

:deep(.p-datatable-header-cell) {
  background-color: #f8fafc !important;
  color: #475569 !important;
  font-weight: 700 !important;
}
</style>
