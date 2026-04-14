<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import { logAudit } from '@/utils/auditLogger'

const authStore = useAuthStore()

import Card from 'primevue/card'
import InputText from 'primevue/inputtext'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import MultiSelect from 'primevue/multiselect'
import Button from 'primevue/button'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'

// ─── Types ────────────────────────────────────────────────────────────────────
interface LinkedUser {
  uid: string
  displayName: string
  email: string
}

interface Extension {
  id: string
  ownerName: string
  ipPhoneNumber: string
  departmentId: string
  workgroup?: string
  location?: string
  linkedUsers?: LinkedUser[]
  linkedUserEmails?: string[]
}

interface UserOption {
  uid: string
  displayName: string
  email: string
  label: string
}

// ─── State ────────────────────────────────────────────────────────────────────
const extensions = ref<Extension[]>([])
const departments = ref<Record<string, string>>({})
const userOptions = ref<UserOption[]>([])
const loadingExt = ref(true)
const loadingUsers = ref(true)
const saving = ref(false)
const successMsg = ref('')
const errorMsg = ref('')
const filterSearch = ref('')

const mapVisible = ref(false)
const selectedExt = ref<Extension | null>(null)
// array ของ uid ที่เลือก
const selectedUids = ref<string[]>([])

// ─── Data Fetching ────────────────────────────────────────────────────────────
onMounted(async () => {
  try {
    const deptsRes = await api.get('/Department')
    const map: Record<string, string> = {}
    for (const d of deptsRes.data) map[d.id] = d.name
    departments.value = map
  } catch {
    // ignore
  }

  try {
    const usersRes = await api.get('/User', { params: { take: '1000' } })
    const users = usersRes.data.items ?? usersRes.data
    userOptions.value = users
      .filter((u: { status?: string }) => u.status === 'active')
      .map((u: { uid?: string; id?: string; displayName?: string; email?: string }) => {
        const uid = u.uid ?? u.id ?? ''
        const name = u.displayName ?? u.email ?? uid
        const email = u.email ?? ''
        return { uid, displayName: name, email, label: `${name}  —  ${email}` }
      })
    if (userOptions.value.length === 0) {
      errorMsg.value = 'ไม่พบบัญชีผู้ใช้งานที่มีสถานะ Active ในระบบ'
    }
  } catch (e) {
    errorMsg.value = `โหลดรายชื่อผู้ใช้งานไม่สำเร็จ: ${String(e)}`
  } finally {
    loadingUsers.value = false
  }

  try {
    const extRes = await api.get('/IPPhoneDirectory', { params: { take: '1000' } })
    const rows: Extension[] = extRes.data.items ?? extRes.data
    rows.sort((a, b) =>
      (a.ipPhoneNumber || '').localeCompare(b.ipPhoneNumber || '', undefined, { numeric: true }),
    )
    extensions.value = rows
  } catch (err: unknown) {
    errorMsg.value = `โหลดข้อมูลเบอร์โทรไม่สำเร็จ: ${String(err)}`
  } finally {
    loadingExt.value = false
  }
})

// ─── Computed ─────────────────────────────────────────────────────────────────
const filteredExtensions = computed(() => {
  if (!filterSearch.value.trim()) return extensions.value
  const q = filterSearch.value.toLowerCase()
  return extensions.value.filter(
    (e) =>
      e.ipPhoneNumber.includes(q) ||
      e.ownerName.toLowerCase().includes(q) ||
      (departments.value[e.departmentId] ?? '').toLowerCase().includes(q) ||
      (e.linkedUsers ?? []).some(
        (u) => u.displayName.toLowerCase().includes(q) || u.email.toLowerCase().includes(q),
      ),
  )
})

const mappedCount = computed(() =>
  extensions.value.filter((e) => (e.linkedUsers ?? []).length > 0).length,
)

// ─── Actions ──────────────────────────────────────────────────────────────────
function openMap(ext: Extension) {
  selectedExt.value = ext
  selectedUids.value = (ext.linkedUsers ?? []).map((u) => u.uid)
  errorMsg.value = ''
  mapVisible.value = true
}

async function saveMapping() {
  if (!selectedExt.value) return
  saving.value = true
  errorMsg.value = ''
  try {
    const linked: LinkedUser[] = selectedUids.value
      .map((uid) => {
        const u = userOptions.value.find((o) => o.uid === uid)
        return u ? { uid: u.uid, displayName: u.displayName, email: u.email } : null
      })
      .filter(Boolean) as LinkedUser[]

    const extNum = selectedExt.value.ipPhoneNumber
    await api.put(`/IPPhoneDirectory/${selectedExt.value.id}`, {
      ...selectedExt.value,
      linkedUsers: linked,
      linkedUserEmails: linked.map((u) => u.email),
    })
    logAudit(
      { uid: authStore.user?.uid ?? '', displayName: authStore.userProfile?.displayName ?? authStore.user?.email ?? '', email: authStore.user?.email ?? '', role: authStore.userProfile?.role ?? 'user' },
      'UPDATE', 'IPPhoneMapping',
      linked.length > 0
        ? `ผูกเบอร์ ${extNum} กับ: ${linked.map((u) => u.email).join(', ')}`
        : `ยกเลิกการผูกเบอร์ ${extNum}`,
    )
    // update local state
    const idx = extensions.value.findIndex((e) => e.id === selectedExt.value!.id)
    const existingExt = extensions.value[idx]
    if (idx !== -1 && existingExt) {
      extensions.value[idx] = { ...existingExt, linkedUsers: linked, linkedUserEmails: linked.map((u) => u.email) }
    }
    mapVisible.value = false
    successMsg.value = linked.length > 0
      ? `ผูกเบอร์ ${extNum} กับ ${linked.length} ผู้ใช้งานเรียบร้อยแล้ว`
      : `ยกเลิกการผูกเบอร์ ${extNum} เรียบร้อยแล้ว`
    setTimeout(() => (successMsg.value = ''), 5000)
  } catch (e) {
    errorMsg.value = `บันทึกไม่สำเร็จ: ${String(e)}`
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-2xl font-bold text-gray-900">ผูกผู้ใช้งานกับเบอร์โทรศัพท์</h1>
      <p class="text-sm text-gray-400 mt-1">1 เบอร์โทรศัพท์สามารถผูกได้หลายผู้ใช้งาน</p>
    </div>

    <Message v-if="successMsg" severity="success" :closable="false">{{ successMsg }}</Message>
    <Message v-if="errorMsg && !mapVisible" severity="error" :closable="false">{{ errorMsg }}</Message>

    <div class="grid grid-cols-2 gap-4">
      <div class="bg-white rounded-xl border border-gray-100 p-4 flex items-center gap-3">
        <div class="w-10 h-10 bg-indigo-100 rounded-lg flex items-center justify-center">
          <i class="pi pi-desktop text-indigo-600"></i>
        </div>
        <div>
          <p class="text-2xl font-bold text-gray-900">{{ extensions.length }}</p>
          <p class="text-xs text-gray-400">เบอร์ทั้งหมด</p>
        </div>
      </div>
      <div class="bg-white rounded-xl border border-gray-100 p-4 flex items-center gap-3">
        <div class="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center">
          <i class="pi pi-link text-green-600"></i>
        </div>
        <div>
          <p class="text-2xl font-bold text-gray-900">{{ mappedCount }}</p>
          <p class="text-xs text-gray-400">ผูกกับผู้ใช้งานแล้ว</p>
        </div>
      </div>
    </div>

    <Card>
      <template #content>
        <div class="mb-4">
          <IconField class="w-full">
            <InputIcon class="pi pi-search" />
            <InputText v-model="filterSearch" placeholder="ค้นหาเบอร์, ชื่อ, หน่วยงาน, ผู้ใช้งาน..." class="w-full" />
          </IconField>
        </div>

        <DataTable
          :value="filteredExtensions"
          :loading="loadingExt"
          paginator
          :rows="20"
          size="small"
          striped-rows
          empty-message="ไม่พบข้อมูล — กรุณานำเข้าข้อมูลสมุดโทรศัพท์ก่อน"
        >
          <Column field="ipPhoneNumber" header="เบอร์ต่อ" style="width: 7rem">
            <template #body="{ data }">
              <span class="font-mono font-bold text-gray-800">{{ data.ipPhoneNumber }}</span>
            </template>
          </Column>
          <Column field="ownerName" header="ชื่อ-นามสกุล" />
          <Column header="หน่วยงาน">
            <template #body="{ data }">
              {{ departments[data.departmentId] ?? data.departmentId ?? '-' }}
            </template>
          </Column>
          <Column field="workgroup" header="กลุ่มงาน" />
          <Column header="ผู้ใช้งานที่ผูกไว้" style="width: 20rem">
            <template #body="{ data }">
              <div v-if="(data.linkedUsers ?? []).length > 0" class="flex flex-col gap-1">
                <div
                  v-for="u in (data.linkedUsers ?? [])"
                  :key="u.uid"
                  class="flex items-center gap-2"
                >
                  <div class="w-6 h-6 bg-indigo-100 rounded-full flex items-center justify-center shrink-0">
                    <i class="pi pi-user text-indigo-600 text-[10px]"></i>
                  </div>
                  <div class="min-w-0">
                    <p class="text-sm font-medium text-gray-800 leading-none truncate">{{ u.displayName }}</p>
                    <p class="text-xs text-gray-400 truncate">{{ u.email }}</p>
                  </div>
                </div>
              </div>
              <Tag v-else severity="secondary" value="ยังไม่ได้ผูก" />
            </template>
          </Column>
          <Column header="จัดการ" style="width: 7rem">
            <template #body="{ data }">
              <Button
                :icon="(data.linkedUsers ?? []).length > 0 ? 'pi pi-pencil' : 'pi pi-link'"
                :label="(data.linkedUsers ?? []).length > 0 ? 'แก้ไข' : 'ผูก'"
                size="small"
                :severity="(data.linkedUsers ?? []).length > 0 ? 'secondary' : 'info'"
                outlined
                @click="openMap(data)"
              />
            </template>
          </Column>
        </DataTable>
      </template>
    </Card>

    <!-- ── Map Dialog ── -->
    <Dialog
      v-model:visible="mapVisible"
      :header="`ผูกเบอร์ ${selectedExt?.ipPhoneNumber} — ${selectedExt?.ownerName}`"
      modal
      :style="{ width: '42rem' }"
    >
      <div class="space-y-4">
        <Message v-if="errorMsg" severity="error" :closable="false">{{ errorMsg }}</Message>

        <!-- Extension info -->
        <div class="bg-gray-50 rounded-xl p-4 text-sm grid grid-cols-2 gap-3">
          <div>
            <p class="text-xs text-gray-400 mb-0.5">เบอร์ต่อ</p>
            <p class="font-mono font-bold text-gray-800 text-lg">{{ selectedExt?.ipPhoneNumber }}</p>
          </div>
          <div>
            <p class="text-xs text-gray-400 mb-0.5">ชื่อ</p>
            <p class="font-medium text-gray-800">{{ selectedExt?.ownerName }}</p>
          </div>
          <div>
            <p class="text-xs text-gray-400 mb-0.5">หน่วยงาน</p>
            <p class="text-gray-700">{{ selectedExt ? (departments[selectedExt.departmentId] ?? selectedExt.departmentId ?? '-') : '-' }}</p>
          </div>
          <div>
            <p class="text-xs text-gray-400 mb-0.5">กลุ่มงาน</p>
            <p class="text-gray-700">{{ selectedExt?.workgroup ?? '-' }}</p>
          </div>
        </div>

        <!-- Multi-select users -->
        <div class="flex flex-col gap-1.5">
          <label class="text-sm font-medium text-gray-700">
            เลือกผู้ใช้งาน
            <span class="text-gray-400 font-normal">(เลือกได้หลายคน)</span>
          </label>
          <div v-if="loadingUsers" class="flex items-center gap-2 text-sm text-gray-400 py-2">
            <i class="pi pi-spin pi-spinner"></i> กำลังโหลดรายชื่อ...
          </div>
          <MultiSelect
            v-else
            v-model="selectedUids"
            :options="userOptions"
            option-label="label"
            option-value="uid"
            placeholder="เลือกผู้ใช้งาน..."
            filter
            filter-placeholder="ค้นหาชื่อหรืออีเมล..."
            class="w-full"
            display="chip"
            :empty-filter-message="userOptions.length === 0 ? 'ไม่พบบัญชีที่ Active' : 'ไม่พบผลการค้นหา'"
          />
          <p class="text-xs text-gray-400">
            แสดงเฉพาะบัญชีที่มีสถานะ Active ({{ userOptions.length }} บัญชี) ·
            เลือกแล้ว {{ selectedUids.length }} คน
          </p>
        </div>

        <!-- Preview -->
        <div v-if="selectedUids.length > 0" class="bg-indigo-50 rounded-xl p-3 space-y-1.5">
          <p class="text-xs font-semibold text-indigo-700 uppercase tracking-wide">ผู้ใช้งานที่จะผูกกับเบอร์นี้</p>
          <div
            v-for="uid in selectedUids"
            :key="uid"
            class="flex items-center gap-2"
          >
            <i class="pi pi-user text-indigo-400 text-xs"></i>
            <span class="text-sm text-indigo-800">
              {{ userOptions.find(u => u.uid === uid)?.displayName }}
              <span class="text-indigo-400">— {{ userOptions.find(u => u.uid === uid)?.email }}</span>
            </span>
          </div>
        </div>
        <div v-else class="bg-gray-50 rounded-xl p-3 text-sm text-gray-400 text-center">
          ไม่มีผู้ใช้งานที่ผูกไว้ (เบอร์นี้จะไม่มีผู้รับผิดชอบ)
        </div>
      </div>

      <template #footer>
        <Button label="ยกเลิก" text @click="mapVisible = false" />
        <Button
          label="บันทึก"
          icon="pi pi-check"
          :loading="saving"
          :disabled="loadingUsers"
          @click="saveMapping"
        />
      </template>
    </Dialog>
  </div>
</template>
