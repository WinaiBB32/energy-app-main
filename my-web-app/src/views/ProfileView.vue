<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import api from '@/services/api'

import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Message from 'primevue/message'
import Tag from 'primevue/tag'

const router = useRouter()
const authStore = useAuthStore()

// ─── Display helpers ──────────────────────────────────────────────────────────
const roleLabel = computed(() => {
  const map: Record<string, string> = { superadmin: 'Super Admin', admin: 'Admin', user: 'ผู้ใช้งาน' }
  return map[authStore.userProfile?.role || 'user'] || '-'
})
const roleSeverity = computed(() => {
  const map: Record<string, string> = { superadmin: 'danger', admin: 'warn', user: 'info' }
  return map[authStore.userProfile?.role || 'user'] || 'secondary'
})
const statusLabel = computed(() => {
  const map: Record<string, string> = { active: 'ใช้งาน', pending: 'รออนุมัติ', suspended: 'ระงับ' }
  return map[authStore.userProfile?.status || ''] || '-'
})
const statusSeverity = computed(() => {
  const map: Record<string, string> = { active: 'success', pending: 'warn', suspended: 'danger' }
  return map[authStore.userProfile?.status || ''] || 'secondary'
})

// ─── Edit display name ────────────────────────────────────────────────────────
const editName = ref(authStore.user?.displayName || `${authStore.user?.firstName || ''} ${authStore.user?.lastName || ''}`.trim() || '')
const isSavingName = ref(false)
const nameSuccess = ref('')
const nameError = ref('')

const saveName = async () => {
  const newName = editName.value.trim()
  if (!newName) { nameError.value = 'กรุณากรอกชื่อ'; return }
  if (!authStore.user?.id) return
  isSavingName.value = true
  nameError.value = ''
  nameSuccess.value = ''
  try {
    await api.put(`/User/${authStore.user.id}`, {
      displayName: newName,
      departmentId: authStore.userProfile?.departmentId || null,
      role: authStore.user.role,
      status: authStore.userProfile?.status || 'active',
      accessibleSystems: authStore.userProfile?.accessibleSystems || [],
      adminSystems: authStore.userProfile?.adminSystems || [],
    })
    // Update local store
    if (authStore.user) {
      const parts = newName.split(' ', 2)
      authStore.user.firstName = parts[0] ?? authStore.user.firstName
      authStore.user.lastName = parts[1] ?? authStore.user.lastName
      authStore.user.displayName = newName
      localStorage.setItem('user_data', JSON.stringify(authStore.user))
    }
    nameSuccess.value = 'บันทึกชื่อเรียบร้อยแล้ว'
  } catch {
    nameError.value = 'เกิดข้อผิดพลาด กรุณาลองใหม่'
  } finally {
    isSavingName.value = false
  }
}

// ─── Change password ───────────────────────────────────────────────────────────
const currentPwd = ref('')
const newPwd = ref('')
const confirmPwd = ref('')
const isChangingPwd = ref(false)
const changePwdSuccess = ref('')
const changePwdError = ref('')

const changePassword = async () => {
  changePwdError.value = ''
  changePwdSuccess.value = ''
  if (!currentPwd.value) { changePwdError.value = 'กรุณากรอกรหัสผ่านปัจจุบัน'; return }
  if (newPwd.value.length < 6) { changePwdError.value = 'รหัสผ่านใหม่ต้องมีอย่างน้อย 6 ตัวอักษร'; return }
  if (newPwd.value !== confirmPwd.value) { changePwdError.value = 'รหัสผ่านใหม่ไม่ตรงกัน'; return }
  isChangingPwd.value = true
  try {
    await api.post('/Auth/change-password', {
      currentPassword: currentPwd.value,
      newPassword: newPwd.value,
    })
    changePwdSuccess.value = 'เปลี่ยนรหัสผ่านเรียบร้อยแล้ว'
    currentPwd.value = ''
    newPwd.value = ''
    confirmPwd.value = ''
  } catch {
    changePwdError.value = 'รหัสผ่านปัจจุบันไม่ถูกต้อง หรือเกิดข้อผิดพลาด'
  } finally {
    isChangingPwd.value = false
  }
}
</script>

<template>
  <div class="min-h-screen bg-gray-50 p-6 font-sans">
    <div class="max-w-xl mx-auto">

      <!-- Back button -->
      <Button label="กลับหน้าหลัก" icon="pi pi-arrow-left" severity="secondary" text
        class="mb-6 hover:bg-gray-100" @click="router.push('/')" />

      <!-- Header card -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 p-6 mb-4 flex items-center gap-4">
        <div
          class="w-16 h-16 rounded-full bg-blue-100 text-blue-600 flex items-center justify-center text-2xl font-bold shrink-0">
          {{ (authStore.user?.displayName || authStore.user?.email || '?').charAt(0).toUpperCase() }}
        </div>
        <div class="min-w-0">
          <h1 class="text-xl font-bold text-gray-800 truncate">
            {{ authStore.user?.displayName || `${authStore.user?.firstName || ''} ${authStore.user?.lastName || ''}`.trim() || 'ไม่มีชื่อ' }}
          </h1>
          <p class="text-sm text-gray-400 truncate">{{ authStore.user?.email }}</p>
          <div class="flex items-center gap-2 mt-1 flex-wrap">
            <Tag :value="roleLabel" :severity="roleSeverity as any" />
            <Tag :value="statusLabel" :severity="statusSeverity as any" />
          </div>
        </div>
      </div>

      <!-- Read-only info -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 p-6 mb-4">
        <h2 class="text-base font-bold text-gray-700 mb-4 flex items-center gap-2">
          <i class="pi pi-id-card text-blue-500"></i> ข้อมูลบัญชี
        </h2>
        <div class="grid grid-cols-2 gap-x-6 gap-y-4 text-sm">
          <div>
            <p class="text-gray-400 text-xs mb-0.5">อีเมล</p>
            <p class="font-medium text-gray-700">{{ authStore.user?.email || '-' }}</p>
          </div>
          <div>
            <p class="text-gray-400 text-xs mb-0.5">ชื่อ-นามสกุล</p>
            <p class="font-medium text-gray-700">{{ authStore.userProfile?.displayName || '-' }}</p>
          </div>
          <div>
            <p class="text-gray-400 text-xs mb-0.5">บทบาท</p>
            <Tag :value="roleLabel" :severity="roleSeverity as any" />
          </div>
          <div>
            <p class="text-gray-400 text-xs mb-0.5">สถานะ</p>
            <Tag :value="statusLabel" :severity="statusSeverity as any" />
          </div>
        </div>
      </div>

      <!-- Edit display name -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 p-6 mb-4">
        <h2 class="text-base font-bold text-gray-700 mb-4 flex items-center gap-2">
          <i class="pi pi-user-edit text-blue-500"></i> แก้ไขชื่อที่แสดงผล
        </h2>
        <Message v-if="nameSuccess" severity="success" :closable="false" class="mb-3">{{ nameSuccess }}</Message>
        <Message v-if="nameError" severity="error" :closable="false" class="mb-3">{{ nameError }}</Message>
        <div class="flex gap-3">
          <InputText v-model="editName" placeholder="ชื่อ-นามสกุล" class="flex-1" />
          <Button label="บันทึก" icon="pi pi-check" :loading="isSavingName" @click="saveName" />
        </div>
      </div>

      <!-- Change password -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 p-6 mb-4">
        <h2 class="text-base font-bold text-gray-700 mb-1 flex items-center gap-2">
          <i class="pi pi-lock text-blue-500"></i> เปลี่ยนรหัสผ่าน
        </h2>
        <p class="text-sm text-gray-500 mb-4">กรอกรหัสผ่านปัจจุบันเพื่อยืนยันตัวตน</p>
        <Message v-if="changePwdSuccess" severity="success" :closable="false" class="mb-3">{{ changePwdSuccess }}</Message>
        <Message v-if="changePwdError" severity="error" :closable="false" class="mb-3">{{ changePwdError }}</Message>
        <div class="flex flex-col gap-3">
          <div class="flex flex-col gap-1">
            <label class="text-xs text-gray-500 font-medium">รหัสผ่านปัจจุบัน</label>
            <Password v-model="currentPwd" placeholder="รหัสผ่านปัจจุบัน" :feedback="false"
              toggleMask inputClass="w-full" class="w-full" />
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-xs text-gray-500 font-medium">รหัสผ่านใหม่</label>
            <Password v-model="newPwd" placeholder="อย่างน้อย 6 ตัวอักษร" :feedback="true"
              toggleMask inputClass="w-full" class="w-full" />
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-xs text-gray-500 font-medium">ยืนยันรหัสผ่านใหม่</label>
            <Password v-model="confirmPwd" placeholder="กรอกรหัสผ่านใหม่อีกครั้ง" :feedback="false"
              toggleMask inputClass="w-full" class="w-full" />
          </div>
          <Button label="บันทึกรหัสผ่านใหม่" icon="pi pi-check" :loading="isChangingPwd"
            @click="changePassword" class="w-full mt-1" />
        </div>
      </div>

    </div>
  </div>
</template>

<style scoped>
:deep(.p-password-input) {
  width: 100%;
}
</style>
