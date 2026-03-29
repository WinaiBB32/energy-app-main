<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import {
  updateProfile,
  sendPasswordResetEmail,
  EmailAuthProvider,
  linkWithCredential,
  reauthenticateWithCredential,
  updatePassword,
} from 'firebase/auth'
import { doc, updateDoc, collection, onSnapshot } from 'firebase/firestore'
import { auth, db } from '@/firebase/config'
import { useAuthStore } from '@/stores/auth'
import { FirebaseError } from 'firebase/app'

import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Message from 'primevue/message'
import Tag from 'primevue/tag'

const router = useRouter()
const authStore = useAuthStore()

// ─── Departments ──────────────────────────────────────────────────────────────
interface Department { id: string; name: string }
const departments = ref<Department[]>([])
let unsubDepts: (() => void) | null = null
onMounted(() => {
  unsubDepts = onSnapshot(collection(db, 'departments'), (snap) => {
    departments.value = snap.docs.map((d) => ({ id: d.id, name: d.data().name as string }))
  })
})
onUnmounted(() => { if (unsubDepts) unsubDepts() })

const departmentName = computed(() => {
  const id = authStore.userProfile?.departmentId
  return departments.value.find((d) => d.id === id)?.name || id || '-'
})

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

// ─── Provider checks ──────────────────────────────────────────────────────────
const isGoogleLinked = computed(() =>
  auth.currentUser?.providerData.some((p) => p.providerId === 'google.com') ?? false
)
const isPasswordLinked = computed(() =>
  auth.currentUser?.providerData.some((p) => p.providerId === 'password') ?? false
)

// ─── Edit display name ────────────────────────────────────────────────────────
const editName = ref(authStore.user?.displayName || authStore.userProfile?.displayName || '')
const isSavingName = ref(false)
const nameSuccess = ref('')
const nameError = ref('')

const saveName = async () => {
  const newName = editName.value.trim()
  if (!newName) { nameError.value = 'กรุณากรอกชื่อ'; return }
  if (!auth.currentUser) return
  isSavingName.value = true
  nameError.value = ''
  nameSuccess.value = ''
  try {
    await updateProfile(auth.currentUser, { displayName: newName })
    await updateDoc(doc(db, 'users', auth.currentUser.uid), { displayName: newName })
    nameSuccess.value = 'บันทึกชื่อเรียบร้อยแล้ว'
  } catch {
    nameError.value = 'เกิดข้อผิดพลาด กรุณาลองใหม่'
  } finally {
    isSavingName.value = false
  }
}

// ─── Link email/password (Google-only users) ──────────────────────────────────
const linkEmail = ref(auth.currentUser?.email || '')
const linkPassword = ref('')
const linkConfirm = ref('')
const isLinking = ref(false)
const linkSuccess = ref('')
const linkError = ref('')

const linkEmailPassword = async () => {
  linkError.value = ''
  linkSuccess.value = ''
  if (!linkEmail.value.trim()) { linkError.value = 'กรุณากรอกอีเมล'; return }
  if (linkPassword.value.length < 6) { linkError.value = 'รหัสผ่านต้องมีอย่างน้อย 6 ตัวอักษร'; return }
  if (linkPassword.value !== linkConfirm.value) { linkError.value = 'รหัสผ่านไม่ตรงกัน'; return }
  if (!auth.currentUser) return
  isLinking.value = true
  try {
    const credential = EmailAuthProvider.credential(linkEmail.value.trim(), linkPassword.value)
    await linkWithCredential(auth.currentUser, credential)
    linkSuccess.value = 'เชื่อมต่ออีเมล/รหัสผ่านเรียบร้อยแล้ว สามารถเข้าสู่ระบบด้วยอีเมลได้แล้ว'
    linkPassword.value = ''
    linkConfirm.value = ''
  } catch (e: unknown) {
    if (e instanceof FirebaseError) {
      if (e.code === 'auth/email-already-in-use') linkError.value = 'อีเมลนี้ถูกใช้งานแล้ว'
      else if (e.code === 'auth/invalid-email') linkError.value = 'รูปแบบอีเมลไม่ถูกต้อง'
      else linkError.value = `เกิดข้อผิดพลาด: ${e.message}`
    } else {
      linkError.value = 'เกิดข้อผิดพลาด กรุณาลองใหม่'
    }
  } finally {
    isLinking.value = false
  }
}

// ─── Change password (email/password users) ───────────────────────────────────
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
  if (!auth.currentUser?.email) return
  isChangingPwd.value = true
  try {
    const credential = EmailAuthProvider.credential(auth.currentUser.email, currentPwd.value)
    await reauthenticateWithCredential(auth.currentUser, credential)
    await updatePassword(auth.currentUser, newPwd.value)
    changePwdSuccess.value = 'เปลี่ยนรหัสผ่านเรียบร้อยแล้ว'
    currentPwd.value = ''
    newPwd.value = ''
    confirmPwd.value = ''
  } catch (e: unknown) {
    if (e instanceof FirebaseError) {
      if (e.code === 'auth/wrong-password' || e.code === 'auth/invalid-credential')
        changePwdError.value = 'รหัสผ่านปัจจุบันไม่ถูกต้อง'
      else if (e.code === 'auth/requires-recent-login')
        changePwdError.value = 'กรุณาออกจากระบบแล้วเข้าใหม่ก่อนเปลี่ยนรหัสผ่าน'
      else changePwdError.value = `เกิดข้อผิดพลาด: ${e.message}`
    } else {
      changePwdError.value = 'เกิดข้อผิดพลาด กรุณาลองใหม่'
    }
  } finally {
    isChangingPwd.value = false
  }
}

// ─── Reset password via email ─────────────────────────────────────────────────
const isResettingPwd = ref(false)
const resetSuccess = ref('')
const resetError = ref('')

const sendResetEmail = async () => {
  const email = auth.currentUser?.email
  if (!email) return
  isResettingPwd.value = true
  resetSuccess.value = ''
  resetError.value = ''
  try {
    await sendPasswordResetEmail(auth, email)
    resetSuccess.value = `ส่งอีเมลรีเซ็ตรหัสผ่านไปที่ ${email} เรียบร้อยแล้ว`
  } catch {
    resetError.value = 'ส่งอีเมลไม่สำเร็จ กรุณาลองใหม่'
  } finally {
    isResettingPwd.value = false
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
            {{ authStore.user?.displayName || authStore.userProfile?.displayName || 'ไม่มีชื่อ' }}
          </h1>
          <p class="text-sm text-gray-400 truncate">{{ authStore.user?.email }}</p>
          <div class="flex items-center gap-2 mt-1 flex-wrap">
            <Tag :value="roleLabel" :severity="roleSeverity as any" />
            <Tag :value="statusLabel" :severity="statusSeverity as any" />
            <span v-if="isGoogleLinked"
              class="text-xs bg-red-50 text-red-400 border border-red-100 px-2 py-0.5 rounded-full flex items-center gap-1">
              <i class="pi pi-google" style="font-size:0.65rem"></i> Google
            </span>
            <span v-if="isPasswordLinked"
              class="text-xs bg-blue-50 text-blue-400 border border-blue-100 px-2 py-0.5 rounded-full flex items-center gap-1">
              <i class="pi pi-envelope" style="font-size:0.65rem"></i> Email
            </span>
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
            <p class="text-gray-400 text-xs mb-0.5">หน่วยงาน</p>
            <p class="font-medium text-gray-700">{{ departmentName }}</p>
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

      <!-- ══════════ SECURITY SECTION ══════════ -->

      <!-- Case 1: Google only → link email/password -->
      <div v-if="isGoogleLinked && !isPasswordLinked"
        class="bg-white rounded-2xl shadow-sm border border-blue-100 p-6 mb-4">
        <h2 class="text-base font-bold text-gray-700 mb-1 flex items-center gap-2">
          <i class="pi pi-link text-blue-500"></i> เพิ่มการเข้าสู่ระบบด้วยอีเมล & รหัสผ่าน
        </h2>
        <p class="text-sm text-gray-500 mb-4">
          เชื่อมต่ออีเมล/รหัสผ่านเข้ากับบัญชีนี้ เพื่อให้สามารถเข้าสู่ระบบได้ทั้ง 2 วิธี
        </p>
        <Message v-if="linkSuccess" severity="success" :closable="false" class="mb-3">{{ linkSuccess }}</Message>
        <Message v-if="linkError" severity="error" :closable="false" class="mb-3">{{ linkError }}</Message>
        <div class="flex flex-col gap-3">
          <div class="flex flex-col gap-1">
            <label class="text-xs text-gray-500 font-medium">อีเมลองค์กร / ผู้ใช้งาน</label>
            <InputText v-model="linkEmail" placeholder="example@domain.com" class="w-full" />
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-xs text-gray-500 font-medium">รหัสผ่าน</label>
            <Password v-model="linkPassword" placeholder="อย่างน้อย 6 ตัวอักษร" :feedback="true"
              toggleMask inputClass="w-full" class="w-full" />
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-xs text-gray-500 font-medium">ยืนยันรหัสผ่าน</label>
            <Password v-model="linkConfirm" placeholder="กรอกรหัสผ่านอีกครั้ง" :feedback="false"
              toggleMask inputClass="w-full" class="w-full" />
          </div>
          <Button label="เชื่อมต่ออีเมล & รหัสผ่าน" icon="pi pi-link" :loading="isLinking"
            @click="linkEmailPassword" class="w-full mt-1" />
        </div>
      </div>

      <!-- Case 2: Has email/password → change password -->
      <div v-if="isPasswordLinked" class="bg-white rounded-2xl shadow-sm border border-gray-100 p-6 mb-4">
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

        <!-- Fallback: send reset email -->
        <div class="mt-4 pt-4 border-t border-gray-100">
          <p class="text-xs text-gray-400 mb-2">หรือรีเซ็ตรหัสผ่านผ่านอีเมล</p>
          <Message v-if="resetSuccess" severity="success" :closable="false" class="mb-2">{{ resetSuccess }}</Message>
          <Message v-if="resetError" severity="error" :closable="false" class="mb-2">{{ resetError }}</Message>
          <Button label="ส่งอีเมลรีเซ็ตรหัสผ่าน" icon="pi pi-envelope" severity="secondary" outlined size="small"
            :loading="isResettingPwd" @click="sendResetEmail" />
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
