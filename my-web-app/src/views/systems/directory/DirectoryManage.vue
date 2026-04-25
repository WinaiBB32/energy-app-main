<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/services/api'
import { useAppToast } from '@/composables/useAppToast'
import { usePermissions } from '@/composables/usePermissions'

import Card from 'primevue/card'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import Textarea from 'primevue/textarea'
import Button from 'primevue/button'
import Message from 'primevue/message'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Dialog from 'primevue/dialog'
import Tag from 'primevue/tag'
import ToggleSwitch from 'primevue/toggleswitch'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'

interface Department {
  id: string
  name: string
}

interface DirectoryEntry {
  ownerName: string
  location: string
  ipPhoneNumber: string
  analogNumber: string
  deviceCode: string
  departmentId: string | null
  workgroup: string
  description: string
  keywords: string
  isPublished: boolean
}

interface FetchedDirectoryEntry extends DirectoryEntry {
  id: string
  createdAt?: string
}

const toast = useAppToast()
const { isSystemAdmin } = usePermissions()
const isAdmin = isSystemAdmin('directory')

const items = ref<FetchedDirectoryEntry[]>([])
const departments = ref<Department[]>([])
const isLoading = ref(false)
const dialogVisible = ref(false)
const isEditMode = ref(false)
const isSaving = ref(false)
const editId = ref<string>('')
const searchQuery = ref('')
const filterDeptId = ref<string | null>(null)
const errorMsg = ref('')
const successMsg = ref('')

const emptyForm = (): DirectoryEntry => ({
  ownerName: '',
  location: '',
  ipPhoneNumber: '',
  analogNumber: '',
  deviceCode: '',
  departmentId: null,
  workgroup: '',
  description: '',
  keywords: '',
  isPublished: true,
})

const form = ref<DirectoryEntry>(emptyForm())

const getDeptName = (id: string | null) =>
  !id ? '-' : departments.value.find((d) => d.id === id)?.name || id

const fetchAll = async () => {
  isLoading.value = true
  try {
    const [dirRes, deptRes] = await Promise.all([
      api.get('/Directory', { params: { take: 9999, skip: 0 } }),
      api.get('/Department'),
    ])
    items.value = dirRes.data.items || []
    departments.value = deptRes.data || []
  } catch (e) {
    toast.fromError(e, 'โหลดข้อมูลไม่สำเร็จ')
  } finally {
    isLoading.value = false
  }
}
onMounted(fetchAll)

const filteredItems = computed(() => {
  let list = items.value
  if (filterDeptId.value) {
    list = list.filter((i) => i.departmentId === filterDeptId.value)
  }
  if (!searchQuery.value) return list
  const q = searchQuery.value.toLowerCase()
  return list.filter(
    (i) =>
      (i.ownerName || '').toLowerCase().includes(q) ||
      (i.ipPhoneNumber || '').toLowerCase().includes(q) ||
      (i.workgroup || '').toLowerCase().includes(q) ||
      (i.location || '').toLowerCase().includes(q) ||
      getDeptName(i.departmentId).toLowerCase().includes(q),
  )
})

const openNewDialog = () => {
  isEditMode.value = false
  editId.value = ''
  form.value = emptyForm()
  errorMsg.value = ''
  successMsg.value = ''
  dialogVisible.value = true
}

const openEditDialog = (item: FetchedDirectoryEntry) => {
  isEditMode.value = true
  editId.value = item.id
  form.value = {
    ownerName: item.ownerName,
    location: item.location,
    ipPhoneNumber: item.ipPhoneNumber,
    analogNumber: item.analogNumber,
    deviceCode: item.deviceCode,
    departmentId: item.departmentId,
    workgroup: item.workgroup,
    description: item.description,
    keywords: item.keywords,
    isPublished: item.isPublished ?? true,
  }
  errorMsg.value = ''
  successMsg.value = ''
  dialogVisible.value = true
}

const save = async () => {
  if (!form.value.ownerName || !form.value.ipPhoneNumber || !form.value.departmentId) {
    errorMsg.value = 'กรุณากรอกข้อมูลบังคับ (*) ให้ครบถ้วน'
    return
  }
  isSaving.value = true
  try {
    if (isEditMode.value && editId.value) {
      await api.put(`/Directory/${editId.value}`, { ...form.value })
      successMsg.value = 'แก้ไขข้อมูลสำเร็จ'
    } else {
      await api.post('/Directory', { ...form.value })
      successMsg.value = 'เพิ่มข้อมูลสำเร็จ'
    }
    await fetchAll()
    setTimeout(() => {
      dialogVisible.value = false
    }, 800)
  } catch (e) {
    toast.fromError(e, 'บันทึกข้อมูลไม่สำเร็จ')
    errorMsg.value = e instanceof Error ? e.message : 'เกิดข้อผิดพลาด'
  } finally {
    isSaving.value = false
  }
}

const remove = async (id: string, name: string) => {
  if (!confirm(`ยืนยันการลบ "${name}" ?`)) return
  try {
    await api.delete(`/Directory/${id}`)
    await fetchAll()
  } catch (e) {
    toast.fromError(e, 'ลบข้อมูลไม่สำเร็จ')
  }
}
</script>

<template>
  <div class="max-w-7xl mx-auto pb-10">
    <div class="mb-6">
      <h2 class="text-3xl font-bold text-gray-800">
        <i class="pi pi-cog text-teal-600 mr-2"></i>จัดการข้อมูลหมายเลขโทรศัพท์
      </h2>
      <p class="text-gray-500 mt-1">เพิ่ม แก้ไข หรือลบรายชื่อในสมุดโทรศัพท์องค์กร</p>
    </div>

    <Card class="shadow-sm border-none">
      <template #content>
        <!-- Filter bar -->
        <div class="flex flex-col sm:flex-row gap-2 mb-4">
          <IconField class="flex-1">
            <InputIcon class="pi pi-search" />
            <InputText v-model="searchQuery" placeholder="ค้นหาชื่อ, เบอร์, กลุ่มงาน..." class="w-full" />
          </IconField>
          <Select
            v-model="filterDeptId"
            :options="[{ id: null, name: 'ทุกหน่วยงาน' }, ...departments]"
            optionLabel="name"
            optionValue="id"
            placeholder="ทุกหน่วยงาน"
            class="w-full sm:w-56"
          />
          <Button
            v-if="isAdmin"
            label="เพิ่มหมายเลขใหม่"
            icon="pi pi-plus"
            severity="help"
            @click="openNewDialog"
          />
        </div>

        <DataTable
          :value="filteredItems"
          :loading="isLoading"
          paginator
          :rows="15"
          stripedRows
          responsiveLayout="scroll"
          emptyMessage="ไม่พบข้อมูล"
        >
          <Column header="ผู้ครอบครอง" style="min-width: 160px">
            <template #body="{ data }">
              <div class="font-semibold text-gray-800">{{ data.ownerName }}</div>
              <div class="text-xs text-gray-400">{{ data.location || '-' }}</div>
            </template>
          </Column>
          <Column header="เบอร์ IP-Phone" style="min-width: 120px">
            <template #body="{ data }">
              <span class="font-bold text-teal-600 text-base tracking-wider">{{ data.ipPhoneNumber }}</span>
              <div v-if="data.analogNumber" class="text-xs text-gray-400">Analog: {{ data.analogNumber }}</div>
            </template>
          </Column>
          <Column header="หน่วยงาน / กลุ่มงาน" style="min-width: 160px">
            <template #body="{ data }">
              <div class="text-sm font-semibold text-gray-700">{{ getDeptName(data.departmentId) }}</div>
              <div class="text-xs text-gray-400">{{ data.workgroup || '-' }}</div>
            </template>
          </Column>
          <Column field="keywords" header="Keywords" style="min-width: 120px">
            <template #body="{ data }">
              <Tag v-if="data.keywords" :value="data.keywords" severity="secondary" rounded class="text-xs" />
              <span v-else class="text-gray-400 text-xs">-</span>
            </template>
          </Column>
          <Column header="สถานะ" style="width: 120px">
            <template #body="{ data }">
              <Tag
                :value="data.isPublished !== false ? 'เผยแพร่ได้' : 'ห้ามเผยแพร่'"
                :severity="data.isPublished !== false ? 'success' : 'danger'"
                rounded
              />
            </template>
          </Column>
          <Column header="จัดการ" style="width: 100px" v-if="isAdmin">
            <template #body="{ data }">
              <div class="flex gap-1">
                <Button icon="pi pi-pencil" text rounded severity="secondary" v-tooltip.top="'แก้ไข'" @click="openEditDialog(data)" />
                <Button icon="pi pi-trash" text rounded severity="danger" v-tooltip.top="'ลบ'" @click="remove(data.id, data.ownerName)" />
              </div>
            </template>
          </Column>
        </DataTable>
      </template>
    </Card>

    <!-- Edit / Add Dialog -->
    <Dialog
      v-model:visible="dialogVisible"
      modal
      :header="isEditMode ? 'แก้ไขข้อมูลหมายเลข' : 'เพิ่มหมายเลขใหม่'"
      :style="{ width: '680px' }"
      :draggable="false"
    >
      <Message v-if="successMsg" severity="success" :closable="false" class="mb-3">{{ successMsg }}</Message>
      <Message v-if="errorMsg" severity="error" :closable="false" class="mb-3">{{ errorMsg }}</Message>

      <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mt-2">
        <div class="flex flex-col gap-2">
          <label class="text-sm font-semibold text-gray-700">ชื่อผู้ครอบครอง / ประจำจุด <span class="text-red-500">*</span></label>
          <InputText v-model="form.ownerName" placeholder="ระบุชื่อเจ้าหน้าที่หรือตำแหน่ง" class="w-full" />
        </div>
        <div class="flex flex-col gap-2">
          <label class="text-sm font-semibold text-gray-700">เบอร์โทร IP-Phone <span class="text-red-500">*</span></label>
          <InputText v-model="form.ipPhoneNumber" placeholder="เช่น 70101" class="w-full font-bold text-teal-600" />
        </div>
        <div class="flex flex-col gap-2">
          <label class="text-sm font-semibold text-gray-700">หน่วยงาน <span class="text-red-500">*</span></label>
          <Select
            v-model="form.departmentId"
            :options="departments"
            optionLabel="name"
            optionValue="id"
            placeholder="เลือกหน่วยงาน"
            class="w-full"
          />
        </div>
        <div class="flex flex-col gap-2">
          <label class="text-sm font-semibold text-gray-700">กลุ่มงาน / แผนก</label>
          <InputText v-model="form.workgroup" placeholder="เช่น IT Support" class="w-full" />
        </div>
        <div class="flex flex-col gap-2">
          <label class="text-sm font-semibold text-gray-700">สถานที่ติดตั้ง</label>
          <InputText v-model="form.location" placeholder="เช่น อาคาร A ชั้น 2" class="w-full" />
        </div>
        <div class="flex flex-col gap-2">
          <label class="text-sm font-semibold text-gray-700">เบอร์โทร Analog (ถ้ามี)</label>
          <InputText v-model="form.analogNumber" placeholder="เช่น 02-123-4567 ต่อ 11" class="w-full" />
        </div>
        <div class="flex flex-col gap-2">
          <label class="text-sm font-semibold text-gray-700">รหัสประจำเครื่อง (MAC/Serial)</label>
          <InputText v-model="form.deviceCode" placeholder="ระบุรหัสหลังเครื่อง" class="w-full" />
        </div>
        <div class="flex flex-col gap-2">
          <label class="text-sm font-semibold text-gray-700">Keywords สำหรับค้นหา</label>
          <InputText v-model="form.keywords" placeholder="เช่น แจ้งซ่อม, ด่วน, รปภ" class="w-full" />
        </div>
        <div class="flex flex-col gap-2 md:col-span-2">
          <label class="text-sm font-semibold text-gray-700">สถานะการเผยแพร่</label>
          <div class="flex items-center gap-3 p-3 bg-gray-50 rounded-lg border border-gray-200">
            <ToggleSwitch v-model="form.isPublished" />
            <span :class="form.isPublished ? 'text-green-600 font-semibold' : 'text-gray-400'">
              <i :class="form.isPublished ? 'pi pi-eye' : 'pi pi-eye-slash'" class="mr-1"></i>
              {{ form.isPublished ? 'เผยแพร่ — ผู้ใช้ทั่วไปมองเห็น' : 'ซ่อน — เฉพาะ Admin เท่านั้น' }}
            </span>
          </div>
        </div>
        <div class="flex flex-col gap-2 md:col-span-2">
          <label class="text-sm font-semibold text-gray-700">รายละเอียดเพิ่มเติม</label>
          <Textarea v-model="form.description" rows="2" class="w-full" />
        </div>
      </div>

      <template #footer>
        <Button label="ยกเลิก" severity="secondary" text @click="dialogVisible = false" />
        <Button
          :label="isEditMode ? 'บันทึกการแก้ไข' : 'เพิ่มหมายเลข'"
          icon="pi pi-check"
          severity="help"
          :loading="isSaving"
          @click="save"
        />
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
