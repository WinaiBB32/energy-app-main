<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import {
  collection, query, onSnapshot, doc, addDoc, updateDoc, deleteDoc, orderBy, serverTimestamp, Timestamp
} from 'firebase/firestore'
// Firebase Removed
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
import Textarea from 'primevue/textarea'
import Message from 'primevue/message'
import Tag from 'primevue/tag'

interface Building {
  id: string
  name: string
  description: string
  createdAt?: Timestamp
}

const buildings = ref<Building[]>([])
const isLoading = ref(true)
let unsubscribe: () => void

const dialogVisible = ref(false)
const isSaving = ref(false)
const isEditMode = ref(false)
const successMessage = ref('')
const errorMessage = ref('')

const currentBuilding = ref<{ id: string; name: string; description: string }>({
  id: '', name: '', description: ''
})

onMounted(() => {
  const q = query(collection(db, 'buildings'), orderBy('createdAt', 'asc'))
  unsubscribe = onSnapshot(q, (snapshot) => {
    const fetched: Building[] = []
    snapshot.forEach((d) => fetched.push({ id: d.id, ...d.data() } as Building))
    buildings.value = fetched
    isLoading.value = false
  }, (error: unknown) => {
    toast.fromError(error, 'ไม่สามารถโหลดข้อมูลอาคารได้')
    isLoading.value = false
  })
})

onUnmounted(() => {
  if (unsubscribe) unsubscribe()
})

const openNewDialog = () => {
  successMessage.value = ''
  errorMessage.value = ''
  isEditMode.value = false
  currentBuilding.value = { id: '', name: '', description: '' }
  dialogVisible.value = true
}

const openEditDialog = (building: Building) => {
  successMessage.value = ''
  errorMessage.value = ''
  isEditMode.value = true
  currentBuilding.value = { id: building.id, name: building.name, description: building.description || '' }
  dialogVisible.value = true
}

const saveBuilding = async () => {
  if (!currentBuilding.value.name.trim()) {
    errorMessage.value = 'กรุณากรอกชื่ออาคาร/มิเตอร์'
    return
  }

  try {
    isSaving.value = true
    errorMessage.value = ''

    const actor = { uid: authStore.user?.uid ?? '', displayName: authStore.userProfile?.displayName ?? authStore.user?.email ?? '', email: authStore.user?.email ?? '', role: authStore.userProfile?.role ?? 'user' }
    if (isEditMode.value) {
      await updateDoc(doc(db, 'buildings', currentBuilding.value.id), {
        name: currentBuilding.value.name.trim(),
        description: currentBuilding.value.description.trim(),
      })
      logAudit(actor, 'UPDATE', 'BuildingManagement', `แก้ไขอาคาร: ${currentBuilding.value.name.trim()}`)
      successMessage.value = 'แก้ไขข้อมูลสำเร็จ'
    } else {
      await addDoc(collection(db, 'buildings'), {
        name: currentBuilding.value.name.trim(),
        description: currentBuilding.value.description.trim(),
        createdAt: serverTimestamp(),
      })
      logAudit(actor, 'CREATE', 'BuildingManagement', `เพิ่มอาคาร: ${currentBuilding.value.name.trim()}`)
      successMessage.value = 'เพิ่มอาคาร/มิเตอร์ใหม่สำเร็จ'
    }

    setTimeout(() => { dialogVisible.value = false }, 900)
  } catch (error: unknown) {
    errorMessage.value = error instanceof Error ? `Error: ${error.message}` : 'เกิดข้อผิดพลาด'
  } finally {
    isSaving.value = false
  }
}

const deleteBuilding = async (id: string, name: string) => {
  if (confirm(`ยืนยันการลบ "${name}" ?\n(ข้อมูลบิลที่บันทึกไว้จะยังคงอยู่ แต่จะไม่แสดงชื่ออาคารอีกต่อไป)`)) {
    try {
      await deleteDoc(doc(db, 'buildings', id))
      logAudit(
        { uid: authStore.user?.uid ?? '', displayName: authStore.userProfile?.displayName ?? authStore.user?.email ?? '', email: authStore.user?.email ?? '', role: authStore.userProfile?.role ?? 'user' },
        'DELETE', 'BuildingManagement', `ลบอาคาร: ${name}`,
      )
    } catch (error) {
      toast.fromError(error, 'ไม่สามารถลบข้อมูลอาคารได้')
      alert('เกิดข้อผิดพลาดในการลบข้อมูล')
    }
  }
}
</script>

<template>
  <div class="max-w-5xl mx-auto pb-10">
    <div class="mb-6 flex flex-col md:flex-row md:items-end justify-between gap-4">
      <div>
        <h2 class="text-3xl font-bold text-gray-800">
          <i class="pi pi-building text-yellow-500 mr-2"></i>จัดการอาคาร / มิเตอร์
        </h2>
        <p class="text-gray-500 mt-1">เพิ่ม แก้ไข รายชื่ออาคารและจุดติดตั้งสำหรับระบบค่าไฟฟ้าและ Solar</p>
      </div>
      <Button label="เพิ่มอาคาร / มิเตอร์" icon="pi pi-plus" @click="openNewDialog" />
    </div>

    <!-- Stat Card -->
    <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-6">
      <div class="bg-white rounded-2xl border border-gray-100 shadow-sm p-5 flex items-center gap-4">
        <div class="w-12 h-12 rounded-xl bg-linear-to-br from-yellow-400 to-orange-500 flex items-center justify-center text-white shadow-sm">
          <i class="pi pi-building text-xl"></i>
        </div>
        <div>
          <p class="text-sm text-gray-500">อาคาร / มิเตอร์ทั้งหมด</p>
          <p class="text-3xl font-bold text-gray-800">{{ buildings.length }}</p>
        </div>
      </div>
    </div>

    <Card class="shadow-sm border-none overflow-hidden">
      <template #content>
        <DataTable
          :value="buildings"
          :loading="isLoading"
          paginator
          :rows="10"
          stripedRows
          responsiveLayout="scroll"
          emptyMessage="ยังไม่มีข้อมูลอาคาร — กดปุ่ม 'เพิ่มอาคาร / มิเตอร์' เพื่อเริ่มต้น"
        >
          <Column header="#" style="width: 3.5rem">
            <template #body="sp">
              <span class="text-gray-400 text-sm font-mono">{{ sp.index + 1 }}</span>
            </template>
          </Column>

          <Column header="ชื่ออาคาร / จุดติดตั้ง">
            <template #body="sp">
              <div class="flex items-center gap-3">
                <div class="w-9 h-9 rounded-lg bg-yellow-50 border border-yellow-100 flex items-center justify-center shrink-0">
                  <i class="pi pi-building text-yellow-500"></i>
                </div>
                <div>
                  <p class="font-bold text-gray-800">{{ sp.data.name }}</p>
                  <p v-if="sp.data.description" class="text-xs text-gray-400 mt-0.5">{{ sp.data.description }}</p>
                </div>
              </div>
            </template>
          </Column>

          <Column header="ระบบที่ใช้">
            <template #body>
              <div class="flex gap-2">
                <Tag value="ค่าไฟฟ้า" severity="warn" rounded class="text-xs" />
                <Tag value="Solar" severity="success" rounded class="text-xs" />
              </div>
            </template>
          </Column>

          <Column header="รหัสอ้างอิง (ID)" style="width: 14rem">
            <template #body="sp">
              <span class="text-xs text-gray-400 font-mono">{{ sp.data.id }}</span>
            </template>
          </Column>

          <Column header="จัดการ" style="width: 8rem">
            <template #body="sp">
              <div class="flex gap-1">
                <Button icon="pi pi-pencil" severity="secondary" text rounded @click="openEditDialog(sp.data)" />
                <Button icon="pi pi-trash" severity="danger" text rounded @click="deleteBuilding(sp.data.id, sp.data.name)" />
              </div>
            </template>
          </Column>
        </DataTable>
      </template>
    </Card>

    <!-- Add / Edit Dialog -->
    <Dialog
      v-model:visible="dialogVisible"
      modal
      :header="isEditMode ? 'แก้ไขอาคาร / มิเตอร์' : 'เพิ่มอาคาร / มิเตอร์ใหม่'"
      :style="{ width: '440px' }"
      :draggable="false"
    >
      <Message v-if="successMessage" severity="success" :closable="false" class="mb-4">{{ successMessage }}</Message>
      <Message v-if="errorMessage" severity="error" :closable="false" class="mb-4">{{ errorMessage }}</Message>

      <div class="flex flex-col gap-4 mt-2">
        <div class="flex flex-col gap-2">
          <label class="font-semibold text-sm text-gray-700">
            ชื่ออาคาร / จุดติดตั้ง <span class="text-red-500">*</span>
          </label>
          <InputText
            v-model="currentBuilding.name"
            placeholder="เช่น อาคารสำนักงานใหญ่, โรงงานผลิต"
            autofocus
            @keyup.enter="saveBuilding"
            class="w-full"
          />
        </div>
        <div class="flex flex-col gap-2">
          <label class="font-semibold text-sm text-gray-700">คำอธิบายเพิ่มเติม</label>
          <Textarea
            v-model="currentBuilding.description"
            placeholder="เช่น มิเตอร์หมายเลข 3-025, ชั้น 1-3 เท่านั้น"
            rows="2"
            class="w-full"
          />
        </div>
      </div>

      <template #footer>
        <Button label="ยกเลิก" severity="secondary" text @click="dialogVisible = false" />
        <Button
          :label="isEditMode ? 'บันทึกการแก้ไข' : 'เพิ่มอาคาร'"
          icon="pi pi-check"
          :loading="isSaving"
          @click="saveBuilding"
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
