<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import api from '@/services/api'
import axios from 'axios'

// นำเข้า PrimeVue Components ตามเดิม
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Select from 'primevue/select'


const router = useRouter()
const authStore = useAuthStore()

// ตัวแปร State (Strict Types)
const isLoginMode = ref<boolean>(true)
const displayName = ref<string>('')
const email = ref<string>('')
const password = ref<string>('')
const selectedDepartmentId = ref<string | null>(null)
const departments = ref<{ id: string; name: string }[]>([])
const errorMessage = ref<string>('')
const successMessage = ref<string>('')
const isLoadingEmail = ref<boolean>(false)

// ดึงข้อมูลหน่วยงาน
const fetchDepartments = async () => {
  try {
    const response = await api.get('/Department/all')
    departments.value = response.data
  } catch {
    // ถ้าโหลดหน่วยงานไม่ได้ ยังสมัครได้โดยไม่เลือกหน่วยงาน
  }
}

onMounted(() => {
  fetchDepartments()
})

// ฟังก์ชันเข้าสู่ระบบและสมัครสมาชิก (.NET Backend)
const handleEmailAuth = async (): Promise<void> => {
  errorMessage.value = ''
  successMessage.value = ''

  // Validate ฟอร์มพื้นฐาน
  if (!email.value || !password.value) {
    errorMessage.value = 'กรุณากรอกข้อมูลให้ครบถ้วน'
    return
  }
  if (!isLoginMode.value) {
    if (!displayName.value) {
      errorMessage.value = 'กรุณากรอกชื่อ-นามสกุล'
      return
    }
    if (!selectedDepartmentId.value && departments.value.length > 0) {
      errorMessage.value = 'กรุณาเลือกหน่วยงาน'
      return
    }
  }

  try {
    isLoadingEmail.value = true

    if (isLoginMode.value) {
      // 🟢 โหมดเข้าสู่ระบบ (เรียกใช้ Pinia Store)
      await authStore.login(email.value, password.value)

      // ล็อกอินผ่าน พาไปหน้า Dashboard
      router.push('/')
    } else {
      // 🔵 โหมดสมัครสมาชิก (ยิง API ไปหา .NET โดยตรง)

      // แยกชื่อและนามสกุลออกจากกัน เพื่อให้ตรงกับ DTO ของ .NET
      const nameParts = displayName.value.trim().split(' ')
      const firstName = nameParts[0]
      const lastName = nameParts.slice(1).join(' ') || ''

      await api.post('/Auth/register', {
        email: email.value,
        password: password.value,
        firstName: firstName,
        lastName: lastName,
        departmentId: selectedDepartmentId.value,
      })

      successMessage.value = 'สมัครสมาชิกสำเร็จ! กรุณาเข้าสู่ระบบ'

      // หน่วงเวลา 2 วินาที แล้วสลับกลับไปหน้า Login
      setTimeout(() => {
        isLoginMode.value = true
        password.value = ''
        selectedDepartmentId.value = null
        successMessage.value = ''
      }, 2000)
    }
  } catch (error: unknown) {
    // 🔴 จัดการ Error แบบ Strict TypeScript
    if (axios.isAxiosError(error)) {
      // ดึง Error Message ที่พ่นมาจาก .NET
      errorMessage.value = error.response?.data?.message || 'ไม่สามารถเชื่อมต่อกับเซิร์ฟเวอร์ได้'
    } else if (error instanceof Error) {
      errorMessage.value = `เกิดข้อผิดพลาด: ${error.message}`
    } else {
      errorMessage.value = 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ'
    }
  } finally {
    isLoadingEmail.value = false
  }
}

</script>

<template>
  <div class="min-h-screen flex">
    <div
      class="hidden lg:flex flex-col w-5/12 bg-gradient-to-br from-slate-900 via-indigo-950 to-indigo-900 p-12 justify-between relative overflow-hidden"
    >
      <div
        class="absolute top-0 right-0 w-96 h-96 bg-indigo-600/10 rounded-full -translate-y-1/2 translate-x-1/2"
      ></div>
      <div
        class="absolute bottom-0 left-0 w-64 h-64 bg-indigo-800/20 rounded-full translate-y-1/2 -translate-x-1/2"
      ></div>

      <div class="relative z-10">
        <div class="flex items-center gap-3 mb-16">
          <div
            class="w-10 h-10 bg-indigo-500 rounded-xl flex items-center justify-center shadow-lg shadow-indigo-500/30"
          >
            <i class="pi pi-building text-white text-lg"></i>
          </div>
          <span class="text-white font-bold text-xl tracking-tight">E-Portal</span>
        </div>

        <h1 class="text-4xl font-bold text-white leading-tight mb-4">
          ระบบบริหาร<br />ทรัพยากรองค์กร
        </h1>
        <p class="text-indigo-300 text-base leading-relaxed mb-10">
          จัดการข้อมูลสาธารณูปโภค พลังงาน และทรัพยากรภายในองค์กรได้อย่างมีประสิทธิภาพ
        </p>

        <div class="space-y-3">
          <div
            v-for="feature in [
              { icon: 'pi-bolt', text: 'ระบบค่าไฟฟ้าและพลังงาน Solar Cell' },
              { icon: 'pi-tint', text: 'ระบบบริหารน้ำประปาและน้ำมันเชื้อเพลิง' },
              { icon: 'pi-chart-bar', text: 'Dashboard และรายงานสถิติเชิงลึก' },
              { icon: 'pi-desktop', text: 'สมุดโทรศัพท์และสถิติ IP-Phone' },
            ]"
            :key="feature.text"
            class="flex items-center gap-3"
          >
            <div class="w-8 h-8 bg-white/10 rounded-lg flex items-center justify-center shrink-0">
              <i :class="`pi ${feature.icon} text-indigo-300 text-sm`"></i>
            </div>
            <span class="text-indigo-200 text-sm">{{ feature.text }}</span>
          </div>
        </div>
      </div>

      <p class="relative z-10 text-indigo-500 text-xs">
        © {{ new Date().getFullYear() }} E-Portal Management System
      </p>
    </div>

    <div class="flex-1 flex items-center justify-center bg-gray-50 p-8">
      <div class="w-full max-w-md">
        <div class="lg:hidden text-center mb-8">
          <div
            class="inline-flex items-center justify-center w-14 h-14 bg-indigo-600 rounded-2xl mb-3 shadow-lg shadow-indigo-500/30"
          >
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
              {{
                isLoginMode
                  ? 'ยินดีต้อนรับกลับ กรุณาล็อกอินเพื่อดำเนินการต่อ'
                  : 'กรอกข้อมูลด้านล่างเพื่อสร้างบัญชีใหม่'
              }}
            </p>
          </div>

          <div class="flex flex-col gap-4">
            <Message v-if="errorMessage" severity="error" :closable="false">{{
              errorMessage
            }}</Message>
            <Message v-if="successMessage" severity="success" :closable="false">{{
              successMessage
            }}</Message>

            <form @submit.prevent="handleEmailAuth" class="flex flex-col gap-4">
              <div v-if="!isLoginMode" class="flex flex-col gap-1.5">
                <label class="text-sm font-semibold text-gray-700"
                  >ชื่อ-นามสกุล <span class="text-red-500">*</span></label
                >
                <InputText v-model="displayName" placeholder="สำหรับแสดงผลในระบบ" class="w-full" />
              </div>

              <div v-if="!isLoginMode" class="flex flex-col gap-1.5">
                <label class="text-sm font-semibold text-gray-700"
                  >หน่วยงาน <span class="text-red-500">*</span></label
                >
                <Select
                  v-model="selectedDepartmentId"
                  :options="departments"
                  optionLabel="name"
                  optionValue="id"
                  placeholder="เลือกหน่วยงานของคุณ"
                  class="w-full"
                />
              </div>

              <div class="flex flex-col gap-1.5">
                <label class="text-sm font-semibold text-gray-700">อีเมล</label>
                <InputText
                  v-model="email"
                  type="email"
                  placeholder="example@domain.com"
                  class="w-full"
                />
              </div>

              <div class="flex flex-col gap-1.5">
                <label class="text-sm font-semibold text-gray-700">รหัสผ่าน</label>
                <Password
                  v-model="password"
                  :feedback="!isLoginMode"
                  toggleMask
                  placeholder="••••••••"
                  inputClass="w-full"
                  class="w-full"
                />
              </div>

              <Button
                type="submit"
                :label="isLoginMode ? 'เข้าสู่ระบบ' : 'สมัครสมาชิก'"
                :icon="isLoginMode ? 'pi pi-sign-in' : 'pi pi-user-plus'"
                class="w-full font-bold mt-1"
                :loading="isLoadingEmail"
              />
            </form>

            <p class="text-center text-sm text-gray-500">
              {{ isLoginMode ? 'ยังไม่มีบัญชี?' : 'มีบัญชีแล้ว?' }}
              <button
                @click="isLoginMode = !isLoginMode; errorMessage = ''; successMessage = ''"
                class="text-indigo-600 font-semibold hover:underline ml-1 bg-transparent border-none cursor-pointer"
              >
                {{ isLoginMode ? 'สมัครสมาชิก' : 'เข้าสู่ระบบ' }}
              </button>
            </p>

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
