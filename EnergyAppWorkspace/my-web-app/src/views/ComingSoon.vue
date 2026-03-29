<script setup lang="ts">
import Card from 'primevue/card'
</script>

<template>
  <div class="max-w-4xl mx-auto pb-10">
    <Card class="shadow-sm border-none overflow-hidden">
      <template #content>
        <div class="py-10 text-center">
          <div class="w-14 h-14 mx-auto rounded-2xl bg-amber-50 border border-amber-100 flex items-center justify-center">
            <i class="pi pi-wrench text-amber-600 text-2xl"></i>
          </div>
          <h2 class="mt-4 text-2xl font-bold text-gray-800">กำลังปรับปรุงระบบ</h2>
          <p class="mt-2 text-gray-500">
            หน้านี้กำลังย้ายการทำงานไปใช้ Backend API และลบ Firebase ออกทั้งหมด
          </p>
        </div>
      </template>
    </Card>
  </div>
</template>

