<script setup lang="ts">
import { watch } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import Button from 'primevue/button'

const authStore = useAuthStore()
const router = useRouter()

// เมื่อ admin อนุมัติ → userProfile.status เปลี่ยนเป็น 'active' แบบ real-time
// → redirect ไปหน้า Portal ทันที โดยไม่ต้องรอให้ผู้ใช้กดอะไร
watch(
  () => authStore.userProfile?.status,
  (newStatus) => {
    if (newStatus === 'active') router.replace('/')
    if (newStatus === 'suspended') authStore.logout()
  }
)
</script>

<template>
  <div class="min-h-screen bg-gray-50 flex items-center justify-center p-4">
    <div class="bg-white rounded-2xl shadow-lg border border-gray-100 w-full max-w-md p-8 text-center">

      <!-- Icon -->
      <div class="w-20 h-20 rounded-full bg-amber-100 flex items-center justify-center mx-auto mb-6">
        <i class="pi pi-clock text-amber-500 text-4xl"></i>
      </div>

      <!-- Title -->
      <h2 class="text-2xl font-bold text-gray-800 mb-2">รอการอนุมัติ</h2>
      <p class="text-gray-500 text-sm leading-relaxed mb-6">
        บัญชีของคุณได้รับการสร้างเรียบร้อยแล้ว<br>
        กรุณารอให้ผู้ดูแลระบบอนุมัติสิทธิ์การเข้าใช้งาน
      </p>

      <!-- User Info -->
      <div class="bg-gray-50 rounded-xl p-4 mb-6 text-left border border-gray-100">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-full bg-blue-100 text-blue-600 flex items-center justify-center font-bold shrink-0">
            {{ (authStore.user?.displayName || authStore.user?.email || '?').charAt(0).toUpperCase() }}
          </div>
          <div class="min-w-0">
            <p class="font-semibold text-gray-800 text-sm truncate">
              {{ authStore.user?.displayName || 'ไม่มีชื่อ' }}
            </p>
            <p class="text-xs text-gray-400 truncate">{{ authStore.user?.email }}</p>
          </div>
          <span class="ml-auto shrink-0 text-[11px] bg-amber-100 text-amber-700 font-semibold px-2.5 py-1 rounded-full">
            รออนุมัติ
          </span>
        </div>
      </div>

      <!-- Steps -->
      <div class="text-left space-y-3 mb-8">
        <div class="flex items-start gap-3">
          <div class="w-6 h-6 rounded-full bg-green-100 text-green-600 flex items-center justify-center shrink-0 mt-0.5">
            <i class="pi pi-check text-xs"></i>
          </div>
          <div>
            <p class="text-sm font-medium text-gray-700">สมัครสมาชิกสำเร็จ</p>
            <p class="text-xs text-gray-400">บัญชีถูกสร้างในระบบแล้ว</p>
          </div>
        </div>
        <div class="flex items-start gap-3">
          <div class="w-6 h-6 rounded-full bg-amber-100 text-amber-600 flex items-center justify-center shrink-0 mt-0.5">
            <i class="pi pi-clock text-xs"></i>
          </div>
          <div>
            <p class="text-sm font-medium text-gray-700">รอผู้ดูแลระบบอนุมัติ</p>
            <p class="text-xs text-gray-400">Admin จะตรวจสอบและกำหนดสิทธิ์ให้</p>
          </div>
        </div>
        <div class="flex items-start gap-3">
          <div class="w-6 h-6 rounded-full bg-gray-100 text-gray-400 flex items-center justify-center shrink-0 mt-0.5">
            <i class="pi pi-lock text-xs"></i>
          </div>
          <div>
            <p class="text-sm font-medium text-gray-400">เข้าใช้งานระบบได้</p>
            <p class="text-xs text-gray-300">หลังจากได้รับการอนุมัติแล้ว</p>
          </div>
        </div>
      </div>

      <Button
        label="ออกจากระบบ"
        icon="pi pi-sign-out"
        severity="secondary"
        outlined
        class="w-full"
        @click="authStore.logout"
      />
    </div>
  </div>
</template>
