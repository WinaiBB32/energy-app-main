<script setup lang="ts">
import { useRouter } from 'vue-router'

import Card from 'primevue/card'
import Button from 'primevue/button'
import Tag from 'primevue/tag'

interface SystemAdminItem {
  id: string
  name: string
  permissionLabel: string
  dataLabel: string
  dataPath: string
  masterDataPath?: string
  extraPaths?: Array<{ label: string; path: string }>
}

interface GlobalAdminTool {
  label: string
  desc: string
  path: string
  icon: string
}

const router = useRouter()

const systems: SystemAdminItem[] = [
  {
    id: 'system1',
    name: 'ค่าไฟฟ้า & Solar',
    permissionLabel: 'ตั้งสิทธิ์ system1',
    dataLabel: 'จัดการข้อมูลค่าไฟและ Solar',
    dataPath: '/electricity',
    masterDataPath: '/admin/buildings',
    extraPaths: [
      { label: 'แดชบอร์ดระบบ', path: '/electricity/dashboard' },
      { label: 'บันทึก Solar', path: '/electricity/solar' },
    ],
  },
  {
    id: 'system2',
    name: 'ค่าน้ำประปา',
    permissionLabel: 'ตั้งสิทธิ์ system2',
    dataLabel: 'จัดการข้อมูลค่าน้ำประปา',
    dataPath: '/water',
    extraPaths: [{ label: 'แดชบอร์ดระบบ', path: '/water/dashboard' }],
  },
  {
    id: 'system3',
    name: 'น้ำมันเชื้อเพลิง',
    permissionLabel: 'ตั้งสิทธิ์ system3',
    dataLabel: 'จัดการข้อมูลน้ำมันเชื้อเพลิง',
    dataPath: '/fuel',
    masterDataPath: '/admin/fuel-types',
    extraPaths: [
      { label: 'แดชบอร์ดระบบ', path: '/fuel/dashboard' },
      { label: 'ประวัติการเติมน้ำมัน', path: '/fuel/history' },
    ],
  },
  {
    id: 'system4',
    name: 'ค่าโทรศัพท์',
    permissionLabel: 'ตั้งสิทธิ์ system4',
    dataLabel: 'จัดการข้อมูลค่าโทรศัพท์',
    dataPath: '/telephone',
    extraPaths: [{ label: 'แดชบอร์ดระบบ', path: '/telephone/dashboard' }],
  },
  {
    id: 'system5',
    name: 'สถิติงานสารบรรณ',
    permissionLabel: 'ตั้งสิทธิ์ system5',
    dataLabel: 'จัดการข้อมูลงานสารบรรณ',
    dataPath: '/saraban',
    extraPaths: [{ label: 'แดชบอร์ดระบบ', path: '/saraban/dashboard' }],
  },
  {
    id: 'system6',
    name: 'ระบบ IP-Phone',
    permissionLabel: 'ตั้งสิทธิ์ system6',
    dataLabel: 'สถิติการโทรเข้า/ออกรายเดือน',
    dataPath: '/ipphone/dashboard',
    extraPaths: [
      { label: 'นำเข้าสถิติ CSV', path: '/ipphone/upload' },
      { label: 'ผูกผู้ใช้-เบอร์โทร', path: '/ipphone/mapping' },
    ],
  },
  {
    id: 'system12',
    name: 'สมุดโทรศัพท์องค์กร',
    permissionLabel: 'ตั้งสิทธิ์ system12',
    dataLabel: 'จัดการรายชื่อเบอร์โทรศัพท์ภายใน IP-Phone / Analog',
    dataPath: '/directory',
  },
  {
    id: 'system7',
    name: 'ระบบไปรษณีย์',
    permissionLabel: 'ตั้งสิทธิ์ system7',
    dataLabel: 'จัดการข้อมูลไปรษณีย์',
    dataPath: '/postal',
    extraPaths: [{ label: 'แดชบอร์ดระบบ', path: '/postal/dashboard' }],
  },
  {
    id: 'system8',
    name: 'สถิติห้องประชุม',
    permissionLabel: 'ตั้งสิทธิ์ system8',
    dataLabel: 'จัดการข้อมูลสถิติห้องประชุม',
    dataPath: '/meeting',
    masterDataPath: '/admin/meeting-rooms',
    extraPaths: [{ label: 'แดชบอร์ดระบบ', path: '/meeting/dashboard' }],
  },
  {
    id: 'system9',
    name: 'ระบบแจ้งซ่อมงานอาคาร',
    permissionLabel: 'ตั้งสิทธิ์ system9',
    dataLabel: 'จัดการข้อมูลใบงานซ่อม/อะไหล่/ช่างภายนอก',
    dataPath: '/maintenance/service',
    extraPaths: [
      { label: 'คลังอะไหล่', path: '/maintenance/spare-parts' },
      { label: 'Timeline ช่างภายนอก', path: '/maintenance/external-timeline' },
    ],
  },
  {
    id: 'system11',
    name: 'ระบบรถยนต์สำนักงาน',
    permissionLabel: 'ตั้งสิทธิ์ system11',
    dataLabel: 'จัดการข้อมูลรถยนต์สำนักงาน',
    dataPath: '/vehicle',
    extraPaths: [
      { label: 'จัดการหน่วยงาน', path: '/vehicle/departments' },
      { label: 'จัดการจังหวัด', path: '/vehicle/provinces' },
    ],
  },
]

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
              <h2 class="font-semibold text-gray-900">{{ system.name }}</h2>
              <Tag :value="system.id" severity="info" />
            </div>
            <p class="text-xs text-gray-500 mb-3">{{ system.dataLabel }}</p>
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
                @click="openPath(system.dataPath)"
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
                v-for="extra in system.extraPaths ?? []"
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
