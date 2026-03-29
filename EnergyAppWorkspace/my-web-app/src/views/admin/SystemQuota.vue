<script setup lang="ts">
import Card from 'primevue/card'

const sparkLimits = [
  { name: 'อ่านข้อมูล (Read)', limit: '50,000 ครั้ง/วัน', icon: 'pi-book', color: 'text-blue-500' },
  { name: 'เขียนข้อมูล (Write)', limit: '20,000 ครั้ง/วัน', icon: 'pi-pencil', color: 'text-green-500' },
  { name: 'ลบข้อมูล (Delete)', limit: '20,000 ครั้ง/วัน', icon: 'pi-trash', color: 'text-red-500' },
  { name: 'พื้นที่เก็บข้อมูลรวม', limit: '1 GB', icon: 'pi-database', color: 'text-purple-500' },
]
</script>

<template>
  <div class="max-w-6xl mx-auto pb-10">
    <div class="mb-6 flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
      <div>
        <h2 class="text-2xl font-bold text-gray-800">
          <i class="pi pi-server text-indigo-500 mr-2"></i>ระบบตรวจสอบโควต้า (Firebase Spark Plan)
        </h2>
        <p class="text-gray-500 mt-1">ข้อมูลเกี่ยวกับข้อจำกัดและโควต้ารายวันของฐานข้อมูล Cloud Firestore</p>
      </div>
      <a 
        href="https://console.firebase.google.com/" 
        target="_blank" 
        class="bg-indigo-600 hover:bg-indigo-700 text-white px-4 py-2 rounded-lg font-semibold flex items-center transition-colors shadow-sm"
      >
        <i class="pi pi-external-link mr-2"></i>ตรวจสอบการใช้งานจริงใน Firebase
      </a>
    </div>

    <div class="bg-amber-50 border-l-4 border-amber-500 p-4 rounded-r-lg mb-8 shadow-sm">
      <div class="flex">
        <div class="flex-shrink-0">
          <i class="pi pi-info-circle text-amber-500 text-xl"></i>
        </div>
        <div class="ml-3">
          <h3 class="text-sm font-bold text-amber-800 uppercase tracking-wider">ข้อควรทราบ (Important Note)</h3>
          <div class="mt-2 text-sm text-amber-700 leading-relaxed font-medium">
            <p>
              โปรเจกต์ของคุณรันอยู่บนแพ็กเกจใช้งานฟรี (Spark Plan) ของ Firebase ข้อมูลมีความปลอดภัยสูง แต่จะมีขีดจำกัดรายวัน (Daily Limits) ที่ต้องคอยมอนิเตอร์ <br>
              <span class="text-rose-600 font-bold">* ระบบเว็บแอปพลิเคชัน (Frontend) ไม่สามารถดึงตัวเลขโควต้ามาแสดงผลแบบเรียลไทม์ได้ เนื่องจากเหตุผลด้านความปลอดภัย Admin ต้องกดปุ่มเพื่อตรวจสอบผ่าน Firebase Console เท่านั้น</span>
            </p>
          </div>
        </div>
      </div>
    </div>

    <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-6">
      <Card v-for="(item, idx) in sparkLimits" :key="idx" class="shadow-sm border-t-4 border-slate-200 hover:border-indigo-400 transition-colors">
        <template #content>
          <div class="flex items-center gap-4">
            <div class="w-12 h-12 rounded-full bg-slate-50 flex items-center justify-center shrink-0">
              <i :class="['pi', item.icon, item.color, 'text-xl']"></i>
            </div>
            <div>
              <p class="text-xs text-gray-500 font-semibold mb-1 uppercase">{{ item.name }}</p>
              <h3 class="text-xl font-bold text-gray-800">{{ item.limit }}</h3>
            </div>
          </div>
        </template>
      </Card>
    </div>
  </div>
</template>
