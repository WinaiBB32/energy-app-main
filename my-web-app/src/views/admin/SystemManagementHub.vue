<script setup lang="ts">
import { useRouter } from 'vue-router'

import Card from 'primevue/card'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import { ADMIN_SYSTEMS } from '@/config/systems'

interface GlobalAdminTool {
  label: string
  desc: string
  path: string
  icon: string
}

const router = useRouter()

const systems = ADMIN_SYSTEMS

const globalTools: GlobalAdminTool[] = [
  {
    label: 'จัดการผู้ใช้งาน/สิทธิ์',
    desc: 'กำหนดสิทธิ์รายระบบและสถานะบัญชีผู้ใช้',
    path: '/admin/users',
    icon: 'pi-users',
  },
  {
    label: 'จัดการหน่วยงาน',
    desc: 'ปรับโครงสร้างหน่วยงานกลางขององค์กร',
    path: '/admin/departments',
    icon: 'pi-sitemap',
  },
  {
    label: 'Audit Log',
    desc: 'ตรวจสอบกิจกรรมการใช้งานทั้งหมด',
    path: '/admin/audit',
    icon: 'pi-shield',
  },
  {
    label: 'ตรวจสอบการใช้งาน Server',
    desc: 'มอนิเตอร์สุขภาพ frontend/backend และฐานข้อมูล',
    path: '/admin/quota',
    icon: 'pi-server',
  },
]

function openPermissions(systemId: string) {
  router.push({ path: '/admin/users', query: { focusSystem: systemId } })
}

function openPath(path: string) {
  router.push(path)
}
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-2xl font-bold text-gray-900">Admin Tool (ระบบที่ 10)</h1>
      <p class="text-sm text-gray-500 mt-1">เครื่องมือผู้ดูแลระบบสำหรับกำหนดสิทธิ์และเข้าจัดการข้อมูลทุกระบบจากจุดเดียว</p>
    </div>

    <Card>
      <template #title>เครื่องมือกลางของผู้ดูแล</template>
      <template #content>
        <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-3">
          <div
            v-for="tool in globalTools"
            :key="tool.path"
            class="border border-gray-200 rounded-xl p-4 bg-white"
          >
            <div class="flex items-center gap-2 mb-2">
              <i :class="`pi ${tool.icon} text-slate-600`"></i>
              <h2 class="font-semibold text-gray-900 text-sm">{{ tool.label }}</h2>
            </div>
            <p class="text-xs text-gray-500 mb-3">{{ tool.desc }}</p>
            <Button size="small" icon="pi pi-arrow-right" label="เปิดใช้งาน" @click="openPath(tool.path)" />
          </div>
        </div>
      </template>
    </Card>

    <Card>
      <template #content>
        <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          <div v-for="system in systems" :key="system.id" class="border border-gray-200 rounded-xl p-4 bg-white">
            <div class="flex items-center justify-between mb-2">
              <h2 class="font-semibold text-gray-900">{{ system.label }}</h2>
              <Tag :value="system.id" severity="info" />
            </div>
            <p class="text-xs text-gray-500 mb-3">{{ system.adminDesc ?? system.desc }}</p>
            <div class="flex flex-wrap gap-2">
              <Button
                size="small"
                icon="pi pi-users"
                label="จัดการสิทธิ์"
                severity="secondary"
                @click="openPermissions(system.id)"
              />
              <Button
                size="small"
                icon="pi pi-database"
                label="จัดการข้อมูล"
                @click="openPath(system.dataPath ?? system.path)"
              />
              <Button
                v-if="system.masterDataPath"
                size="small"
                icon="pi pi-cog"
                label="ตั้งค่าหลัก"
                severity="help"
                @click="openPath(system.masterDataPath)"
              />
              <Button
                v-for="extra in system.adminPaths ?? []"
                :key="`${system.id}-${extra.path}`"
                size="small"
                icon="pi pi-link"
                :label="extra.label"
                text
                @click="openPath(extra.path)"
              />
            </div>
          </div>
        </div>
      </template>
    </Card>
  </div>
</template>
