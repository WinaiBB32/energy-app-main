<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { ApiError } from '@/services/api'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import { logAudit } from '@/utils/auditLogger'
import { useAppToast } from '@/composables/useAppToast'

const authStore = useAuthStore()
const toast = useAppToast()

import Card from 'primevue/card'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Tag from 'primevue/tag'

interface FuelType {
  id: string
  name: string
  severity: string
}

const severityOptions = [
  { value: 'danger', label: 'แดง (ดีเซล)', color: 'bg-red-100 text-red-600 border-red-200' },
  {
    value: 'warn',
    label: 'เหลือง (แก๊สโซฮอล์)',
    color: 'bg-amber-100 text-amber-600 border-amber-200',
  },
  { value: 'info', label: 'ฟ้า (พรีเมียม)', color: 'bg-blue-100 text-blue-600 border-blue-200' },
  {
    value: 'success',
    label: 'เขียว (E20/E85)',
    color: 'bg-green-100 text-green-600 border-green-200',
  },
  { value: 'secondary', label: 'เทา (อื่นๆ)', color: 'bg-gray-100 text-gray-600 border-gray-200' },
]

const fuelTypes = ref<FuelType[]>([])
const isLoading = ref(true)

const dialogVisible = ref(false)
const isSaving = ref(false)
const isEditMode = ref(false)
const successMessage = ref('')
const errorMessage = ref('')

const currentFuelType = ref<{ id: string; name: string; severity: string }>({
  id: '',
  name: '',
  severity: 'secondary',
})

const fetchFuelTypes = async () => {
  isLoading.value = true
  try {
    const res = await api.get('/FuelType')
    // ป้องกัน error is not iterable โดยเช็คว่าเป็น Array หรือไม่ก่อนนำไปใช้งาน
    fuelTypes.value = Array.isArray(res.data) ? res.data : (res.data?.data || [])
  } catch (err) {
    toast.fromError(err, 'ไม่สามารถโหลดข้อมูลประเภทน้ำมันได้')
  } finally {
    isLoading.value = false
  }
}

onMounted(() => {
  fetchFuelTypes()
})

const openNewDialog = () => {
  successMessage.value = ''
  errorMessage.value = ''
  isEditMode.value = false
  currentFuelType.value = { id: '', name: '', severity: 'secondary' }
  dialogVisible.value = true
}

const openEditDialog = (ft: FuelType) => {
  successMessage.value = ''
  errorMessage.value = ''
  isEditMode.value = true
  currentFuelType.value = { id: ft.id, name: ft.name, severity: ft.severity || 'secondary' }
  dialogVisible.value = true
}

const saveFuelType = async () => {
  if (!currentFuelType.value.name.trim()) {
    errorMessage.value = 'กรุณากรอกชื่อประเภทน้ำมัน'
    return
  }

  isSaving.value = true
  errorMessage.value = ''
  try {
    const actor = {
      uid: authStore.user?.id ?? '',
      displayName: authStore.user?.firstName ?? authStore.user?.email ?? '',
      email: authStore.user?.email ?? '',
      role: authStore.user?.role ?? 'User',
    }

    if (isEditMode.value) {
      await api.put(`/FuelType/${currentFuelType.value.id}`, {
        name: currentFuelType.value.name.trim(),
        severity: currentFuelType.value.severity,
      })
      logAudit(
        actor,
        'UPDATE',
        'FuelTypeManagement',
        `แก้ไขประเภทน้ำมัน: ${currentFuelType.value.name.trim()}`,
      )
      successMessage.value = 'แก้ไขข้อมูลสำเร็จ'
    } else {
      await api.post('/FuelType', {
        name: currentFuelType.value.name.trim(),
        severity: currentFuelType.value.severity,
      })
      logAudit(
        actor,
        'CREATE',
        'FuelTypeManagement',
        `เพิ่มประเภทน้ำมัน: ${currentFuelType.value.name.trim()}`,
      )
      successMessage.value = 'เพิ่มประเภทน้ำมันใหม่สำเร็จ'
    }

    await fetchFuelTypes()
    setTimeout(() => {
      dialogVisible.value = false
    }, 900)
  } catch (error: unknown) {
    if (error instanceof ApiError) {
      errorMessage.value = error.response?.data?.message || 'เกิดข้อผิดพลาดในการเชื่อมต่อ'
    } else if (error instanceof Error) {
      errorMessage.value = error.message
    } else {
      errorMessage.value = 'เกิดข้อผิดพลาดในการเชื่อมต่อ'
    }
  } finally {
    isSaving.value = false
  }
}

const deleteFuelType = async (id: string, name: string) => {
  if (
    confirm(
      `ยืนยันการลบ "${name}" ?\n(ข้อมูลบันทึกที่ใช้ประเภทนี้จะยังคงอยู่ แต่จะไม่แสดงชื่อประเภทอีกต่อไป)`,
    )
  ) {
    try {
      await api.delete(`/FuelType/${id}`)
      logAudit(
        {
          uid: authStore.user?.id ?? '',
          displayName: authStore.user?.firstName ?? authStore.user?.email ?? '',
          email: authStore.user?.email ?? '',
          role: authStore.user?.role ?? 'User',
        },
        'DELETE',
        'FuelTypeManagement',
        `ลบประเภทน้ำมัน: ${name}`,
      )
      await fetchFuelTypes()
      toast.success('ลบข้อมูลสำเร็จ')
    } catch (error) {
      toast.fromError(error, 'ไม่สามารถลบประเภทน้ำมันได้')
    }
  }
}

const getSeverityLabel = (val: string) => severityOptions.find((o) => o.value === val)?.label || val
</script>

<template>
  <div class="max-w-4xl mx-auto pb-10">
    <div class="mb-6 flex flex-col md:flex-row md:items-end justify-between gap-4">
      <div>
        <h2 class="text-3xl font-bold text-gray-800">
          <i class="pi pi-gauge text-red-500 mr-2"></i>จัดการประเภทน้ำมันเชื้อเพลิง
        </h2>
        <p class="text-gray-500 mt-1">
          เพิ่ม แก้ไข ลบ ประเภทน้ำมันที่ใช้ในระบบบันทึกน้ำมันเชื้อเพลิง
        </p>
      </div>
      <Button label="เพิ่มประเภทน้ำมัน" icon="pi pi-plus" severity="danger" @click="openNewDialog" />
    </div>

    <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-6">
      <div class="bg-white rounded-2xl border border-gray-100 shadow-sm p-5 flex items-center gap-4">
        <div
          class="w-12 h-12 rounded-xl bg-linear-to-br from-red-400 to-rose-500 flex items-center justify-center text-white shadow-sm">
          <i class="pi pi-gauge text-xl"></i>
        </div>
        <div>
          <p class="text-sm text-gray-500">ประเภทน้ำมันทั้งหมด</p>
          <p class="text-3xl font-bold text-gray-800">{{ fuelTypes.length }}</p>
        </div>
      </div>
    </div>

    <Card class="shadow-sm border-none overflow-hidden">
      <template #content>
        <DataTable :value="fuelTypes" :loading="isLoading" paginator :rows="10" stripedRows responsiveLayout="scroll"
          emptyMessage="ยังไม่มีประเภทน้ำมัน — กดปุ่ม 'เพิ่มประเภทน้ำมัน' เพื่อเริ่มต้น">
          <Column header="#" style="width: 3.5rem">
            <template #body="sp">
              <span class="text-gray-400 text-sm font-mono">{{ sp.index + 1 }}</span>
            </template>
          </Column>

          <Column header="ชื่อประเภทน้ำมัน">
            <template #body="sp">
              <div class="flex items-center gap-3">
                <Tag :value="sp.data.name" :severity="sp.data.severity || 'secondary'" rounded
                  class="text-sm font-bold" />
              </div>
            </template>
          </Column>

          <Column header="สีแสดงผล (Tag)">
            <template #body="sp">
              <span class="text-sm text-gray-500">{{ getSeverityLabel(sp.data.severity) }}</span>
            </template>
          </Column>

          <Column header="จัดการ" style="width: 8rem">
            <template #body="sp">
              <div class="flex gap-1">
                <Button icon="pi pi-pencil" severity="secondary" text rounded @click="openEditDialog(sp.data)" />
                <Button icon="pi pi-trash" severity="danger" text rounded
                  @click="deleteFuelType(sp.data.id, sp.data.name)" />
              </div>
            </template>
          </Column>
        </DataTable>
      </template>
    </Card>

    <Dialog v-model:visible="dialogVisible" modal :header="isEditMode ? 'แก้ไขประเภทน้ำมัน' : 'เพิ่มประเภทน้ำมันใหม่'"
      :style="{ width: '440px' }" :draggable="false">
      <Message v-if="successMessage" severity="success" :closable="false" class="mb-4">{{
        successMessage
      }}</Message>
      <Message v-if="errorMessage" severity="error" :closable="false" class="mb-4">{{
        errorMessage
      }}</Message>

      <div class="flex flex-col gap-5 mt-2">
        <div class="flex flex-col gap-2">
          <label class="font-semibold text-sm text-gray-700">
            ชื่อประเภทน้ำมัน <span class="text-red-500">*</span>
          </label>
          <InputText v-model="currentFuelType.name" placeholder="เช่น ดีเซล B7, แก๊สโซฮอล์ 91" autofocus
            @keyup.enter="saveFuelType" class="w-full" />
        </div>

        <div class="flex flex-col gap-2">
          <label class="font-semibold text-sm text-gray-700">สีแสดงผล (Tag) <span class="text-red-500">*</span></label>
          <div class="flex flex-wrap gap-2">
            <button v-for="opt in severityOptions" :key="opt.value" type="button"
              @click="currentFuelType.severity = opt.value" :class="[
                'px-3 py-2 rounded-lg border text-sm font-semibold transition-all',
                opt.color,
                currentFuelType.severity === opt.value
                  ? 'ring-2 ring-offset-1 ring-gray-400 shadow-sm scale-105'
                  : 'opacity-60 hover:opacity-100',
              ]">
              {{ opt.label }}
            </button>
          </div>
          <div class="mt-1 flex items-center gap-2 text-sm text-gray-500">
            ตัวอย่าง:
            <Tag :value="currentFuelType.name || 'ตัวอย่าง'" :severity="currentFuelType.severity" rounded />
          </div>
        </div>
      </div>

      <template #footer>
        <Button label="ยกเลิก" severity="secondary" text @click="dialogVisible = false" />
        <Button :label="isEditMode ? 'บันทึกการแก้ไข' : 'เพิ่มประเภทน้ำมัน'" icon="pi pi-check" severity="danger"
          :loading="isSaving" @click="saveFuelType" />
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
