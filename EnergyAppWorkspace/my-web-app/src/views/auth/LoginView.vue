<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import {
  signInWithEmailAndPassword,
  createUserWithEmailAndPassword,
  GoogleAuthProvider,
  signInWithPopup,
  type User
} from 'firebase/auth'
import { FirebaseError } from 'firebase/app'
import { doc, getDoc, setDoc, serverTimestamp } from 'firebase/firestore'
import { auth, db } from '@/firebase/config'
import { logAudit } from '@/utils/auditLogger'
import { useAppToast } from '@/composables/useAppToast'

import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Button from 'primevue/button'
import Message from 'primevue/message'

const router = useRouter()

const isLoginMode = ref<boolean>(true)
const displayName = ref<string>('')
const email = ref<string>('')
const password = ref<string>('')
const errorMessage = ref<string>('')
const successMessage = ref<string>('')
const isLoadingEmail = ref<boolean>(false)
const isLoadingGoogle = ref<boolean>(false)
const toast = useAppToast()

const syncUserToFirestore = async (user: User, nameFromForm: string = ''): Promise<void> => {
  const userRef = doc(db, 'users', user.uid)
  const userSnap = await getDoc(userRef)
  if (!userSnap.exists()) {
    try {
      await setDoc(userRef, {
        email: user.email,
        displayName: nameFromForm || user.displayName || user.email?.split('@')[0] || 'ไม่ระบุชื่อ',
        departmentId: 'DEP-01',
        role: 'user',
        status: 'pending',
        accessibleSystems: [],
        createdAt: serverTimestamp()
      })
    } catch (firestoreError) {
      console.error('[syncUserToFirestore] ไม่สามารถสร้าง profile ใน Firestore ได้:', firestoreError)
      toast.fromError(firestoreError, 'ไม่สามารถสร้างโปรไฟล์ผู้ใช้ได้ กรุณาติดต่อผู้ดูแลระบบ')
      throw firestoreError
    }
  }
}

const handleEmailAuth = async (): Promise<void> => {
  errorMessage.value = ''
  successMessage.value = ''
  if (!email.value || !password.value) { errorMessage.value = 'กรุณากรอกข้อมูลให้ครบถ้วน'; return }
  if (!isLoginMode.value && !displayName.value) { errorMessage.value = 'กรุณากรอกชื่อ-นามสกุล'; return }
  try {
    isLoadingEmail.value = true
    if (isLoginMode.value) {
      const cred = await signInWithEmailAndPassword(auth, email.value, password.value)
      await syncUserToFirestore(cred.user)
      const userSnap = await getDoc(doc(db, 'users', cred.user.uid))
      const realRole = userSnap.exists() ? (userSnap.data().role as string) : 'user'
      logAudit(
        { uid: cred.user.uid, displayName: cred.user.displayName ?? cred.user.email ?? '', email: cred.user.email ?? '', role: realRole },
        'LOGIN', 'Auth', 'Email login',
      )
      router.push('/')
    } else {
      const cred = await createUserWithEmailAndPassword(auth, email.value, password.value)
      await syncUserToFirestore(cred.user, displayName.value)
      successMessage.value = 'สมัครสมาชิกสำเร็จ! กรุณารอผู้ดูแลระบบอนุมัติสิทธิ์'
      setTimeout(() => { isLoginMode.value = true; password.value = '' }, 2000)
    }
  } catch (error: unknown) {
    if (error instanceof FirebaseError) {
      switch (error.code) {
        case 'auth/invalid-email': errorMessage.value = 'รูปแบบอีเมลไม่ถูกต้อง'; break
        case 'auth/user-not-found':
        case 'auth/wrong-password':
        case 'auth/invalid-credential': errorMessage.value = 'อีเมลหรือรหัสผ่านไม่ถูกต้อง'; break
        case 'auth/email-already-in-use': errorMessage.value = 'อีเมลนี้ถูกใช้งานแล้ว กรุณาเข้าสู่ระบบ'; break
        case 'auth/weak-password': errorMessage.value = 'รหัสผ่านต้องมีอย่างน้อย 6 ตัวอักษร'; break
        default: errorMessage.value = `เกิดข้อผิดพลาด: ${error.message}`
      }
    } else if (error instanceof Error) {
      errorMessage.value = `เกิดข้อผิดพลาด: ${error.message}`
    }
  } finally {
    isLoadingEmail.value = false
  }
}

const handleGoogleLogin = async (): Promise<void> => {
  errorMessage.value = ''
  try {
    isLoadingGoogle.value = true
    const provider = new GoogleAuthProvider()
    provider.setCustomParameters({ prompt: 'select_account' })
    const cred = await signInWithPopup(auth, provider)
    await syncUserToFirestore(cred.user)
    const userSnap = await getDoc(doc(db, 'users', cred.user.uid))
    const realRole = userSnap.exists() ? (userSnap.data().role as string) : 'user'
    logAudit(
      { uid: cred.user.uid, displayName: cred.user.displayName ?? cred.user.email ?? '', email: cred.user.email ?? '', role: realRole },
      'LOGIN', 'Auth', 'Google login',
    )
    router.push('/')
  } catch (error: unknown) {
    if (error instanceof FirebaseError) {
      if (error.code === 'auth/popup-closed-by-user') {
        errorMessage.value = 'ยกเลิกการเข้าสู่ระบบด้วย Google'
      } else {
        errorMessage.value = `ไม่สามารถเข้าสู่ระบบด้วย Google ได้: ${error.message}`
      }
    } else {
      errorMessage.value = 'ไม่สามารถเข้าสู่ระบบด้วย Google ได้ กรุณาลองใหม่'
    }
  } finally {
    isLoadingGoogle.value = false
  }
}
</script>

<template>
  <div class="min-h-screen flex">
    <!-- Left: Branding Panel -->
    <div class="hidden lg:flex flex-col w-5/12 bg-gradient-to-br from-slate-900 via-indigo-950 to-indigo-900 p-12 justify-between relative overflow-hidden">
      <!-- Background decoration -->
      <div class="absolute top-0 right-0 w-96 h-96 bg-indigo-600/10 rounded-full -translate-y-1/2 translate-x-1/2"></div>
      <div class="absolute bottom-0 left-0 w-64 h-64 bg-indigo-800/20 rounded-full translate-y-1/2 -translate-x-1/2"></div>

      <div class="relative z-10">
        <div class="flex items-center gap-3 mb-16">
          <div class="w-10 h-10 bg-indigo-500 rounded-xl flex items-center justify-center shadow-lg shadow-indigo-500/30">
            <i class="pi pi-building text-white text-lg"></i>
          </div>
          <span class="text-white font-bold text-xl tracking-tight">E-Portal</span>
        </div>

        <h1 class="text-4xl font-bold text-white leading-tight mb-4">
          ระบบบริหาร<br>ทรัพยากรองค์กร
        </h1>
        <p class="text-indigo-300 text-base leading-relaxed mb-10">
          จัดการข้อมูลสาธารณูปโภค พลังงาน และทรัพยากรภายในองค์กรได้อย่างมีประสิทธิภาพ
        </p>

        <div class="space-y-3">
          <div v-for="feature in [
            { icon: 'pi-bolt', text: 'ระบบค่าไฟฟ้าและพลังงาน Solar Cell' },
            { icon: 'pi-tint', text: 'ระบบบริหารน้ำประปาและน้ำมันเชื้อเพลิง' },
            { icon: 'pi-chart-bar', text: 'Dashboard และรายงานสถิติเชิงลึก' },
            { icon: 'pi-desktop', text: 'สมุดโทรศัพท์และสถิติ IP-Phone' },
          ]" :key="feature.text" class="flex items-center gap-3">
            <div class="w-8 h-8 bg-white/10 rounded-lg flex items-center justify-center shrink-0">
              <i :class="`pi ${feature.icon} text-indigo-300 text-sm`"></i>
            </div>
            <span class="text-indigo-200 text-sm">{{ feature.text }}</span>
          </div>
        </div>
      </div>

      <p class="relative z-10 text-indigo-500 text-xs">© {{ new Date().getFullYear() }} E-Portal Management System</p>
    </div>

    <!-- Right: Form Panel -->
    <div class="flex-1 flex items-center justify-center bg-gray-50 p-8">
      <div class="w-full max-w-md">
        <!-- Mobile logo -->
        <div class="lg:hidden text-center mb-8">
          <div class="inline-flex items-center justify-center w-14 h-14 bg-indigo-600 rounded-2xl mb-3 shadow-lg shadow-indigo-500/30">
            <i class="pi pi-building text-2xl text-white"></i>
          </div>
          <h1 class="text-2xl font-bold text-gray-900">E-Portal</h1>
        </div>

        <div class="bg-white rounded-2xl shadow-sm border border-gray-100 p-8">
          <div class="mb-7">
            <h2 class="text-2xl font-bold text-gray-900">
              {{ isLoginMode ? 'เข้าสู่ระบบ' : 'สมัครสมาชิกใหม่' }}
            </h2>
            <p class="text-gray-400 text-sm mt-1">
              {{ isLoginMode ? 'ยินดีต้อนรับกลับ กรุณาล็อกอินเพื่อดำเนินการต่อ' : 'กรอกข้อมูลด้านล่างเพื่อสร้างบัญชีใหม่' }}
            </p>
          </div>

          <div class="flex flex-col gap-4">
            <Message v-if="errorMessage" severity="error" :closable="false">{{ errorMessage }}</Message>
            <Message v-if="successMessage" severity="success" :closable="false">{{ successMessage }}</Message>

            <form @submit.prevent="handleEmailAuth" class="flex flex-col gap-4">
              <div v-if="!isLoginMode" class="flex flex-col gap-1.5">
                <label class="text-sm font-semibold text-gray-700">ชื่อ-นามสกุล <span class="text-red-500">*</span></label>
                <InputText v-model="displayName" placeholder="สำหรับแสดงผลในระบบ" class="w-full" />
              </div>

              <div class="flex flex-col gap-1.5">
                <label class="text-sm font-semibold text-gray-700">อีเมล</label>
                <InputText v-model="email" type="email" placeholder="example@domain.com" class="w-full" />
              </div>

              <div class="flex flex-col gap-1.5">
                <label class="text-sm font-semibold text-gray-700">รหัสผ่าน</label>
                <Password v-model="password" :feedback="!isLoginMode" toggleMask placeholder="••••••••" inputClass="w-full" class="w-full" />
              </div>

              <Button type="submit"
                :label="isLoginMode ? 'เข้าสู่ระบบ' : 'สมัครสมาชิก'"
                :icon="isLoginMode ? 'pi pi-sign-in' : 'pi pi-user-plus'"
                class="w-full font-bold mt-1"
                :loading="isLoadingEmail"
                :disabled="isLoadingGoogle" />
            </form>

            <p class="text-center text-sm text-gray-500">
              {{ isLoginMode ? 'ยังไม่มีบัญชี?' : 'มีบัญชีแล้ว?' }}
              <button
                @click="isLoginMode = !isLoginMode; errorMessage = ''; successMessage = '';"
                class="text-indigo-600 font-semibold hover:underline ml-1 bg-transparent border-none cursor-pointer">
                {{ isLoginMode ? 'สมัครสมาชิก' : 'เข้าสู่ระบบ' }}
              </button>
            </p>

            <div class="flex items-center gap-3">
              <div class="flex-1 h-px bg-gray-100"></div>
              <span class="text-xs text-gray-400">หรือดำเนินการต่อด้วย</span>
              <div class="flex-1 h-px bg-gray-100"></div>
            </div>

            <Button type="button"
              label="เข้าสู่ระบบด้วย Google"
              icon="pi pi-google"
              severity="secondary"
              outlined
              class="w-full font-semibold"
              @click="handleGoogleLogin"
              :loading="isLoadingGoogle"
              :disabled="isLoadingEmail" />
          </div>
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
