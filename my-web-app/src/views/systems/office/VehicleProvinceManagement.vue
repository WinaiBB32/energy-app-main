<template>
  <div class="p-4 space-y-4">
    <!-- Header -->
    <div class="flex flex-wrap justify-between items-center gap-2">
      <div>
        <h2 class="text-xl font-bold text-gray-800">จัดการจังหวัด (ระบบรถยนต์)</h2>
        <p class="text-sm text-gray-500 mt-0.5">รายการจังหวัดสำหรับเลือกในฟอร์มบันทึกรถยนต์</p>
      </div>
      <div class="flex gap-2">
        <Button label="เพิ่มทั้งหมด 77 จังหวัด" icon="pi pi-list" severity="secondary" @click="seedProvinces" :loading="store.isLoading" />
        <Button label="เพิ่มจังหวัด" icon="pi pi-plus" @click="openAdd" />
      </div>
    </div>

    <!-- Search -->
    <InputText v-model="search" placeholder="ค้นหาชื่อจังหวัด..." class="w-full md:w-72" />

    <!-- Table -->
    <DataTable
      :value="filtered"
      :loading="store.isLoading"
      paginator
      :rows="20"
      dataKey="id"
      class="p-datatable-sm"
    >
      <template #empty>ยังไม่มีข้อมูลจังหวัด — กด "เพิ่มทั้งหมด 77 จังหวัด" เพื่อเริ่มต้น</template>
      <Column field="id" header="#" style="width: 70px" />
      <Column field="name" header="ชื่อจังหวัด" sortable />
      <Column header="" style="width: 90px">
        <template #body="{ data }">
          <div class="flex gap-1">
            <Button icon="pi pi-pencil" text rounded size="small" severity="info" @click="openEdit(data)" />
            <Button icon="pi pi-trash" text rounded size="small" severity="danger" @click="confirmDelete(data)" />
          </div>
        </template>
      </Column>
    </DataTable>

    <!-- Add / Edit Dialog -->
    <Dialog
      v-model:visible="dialogVisible"
      :header="editTarget ? 'แก้ไขจังหวัด' : 'เพิ่มจังหวัด'"
      :style="{ width: '380px' }"
      modal
    >
      <div class="flex flex-col gap-1 pt-2">
        <label class="text-sm font-medium">ชื่อจังหวัด *</label>
        <InputText v-model="form.name" placeholder="เช่น กรุงเทพมหานคร" class="w-full" />
      </div>
      <p v-if="store.error" class="mt-3 text-sm text-red-600">{{ store.error }}</p>
      <template #footer>
        <Button label="ยกเลิก" text @click="dialogVisible = false" />
        <Button
          :label="editTarget ? 'บันทึกการแก้ไข' : 'เพิ่ม'"
          icon="pi pi-check"
          :loading="store.isLoading"
          @click="submitForm"
        />
      </template>
    </Dialog>

    <ConfirmDialog />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useConfirm } from 'primevue/useconfirm'
import { useVehicleMasterStore } from '@/stores/vehicleMasterStore'
import { useAppToast } from '@/composables/useAppToast'
import type { VehicleProvince } from '@/types/vehicle'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import ConfirmDialog from 'primevue/confirmdialog'

const THAILAND_PROVINCES = [
  'กรุงเทพมหานคร','กระบี่','กาญจนบุรี','กาฬสินธุ์','กำแพงเพชร',
  'ขอนแก่น','จันทบุรี','ฉะเชิงเทรา','ชลบุรี','ชัยนาท',
  'ชัยภูมิ','ชุมพร','เชียงราย','เชียงใหม่','ตรัง',
  'ตราด','ตาก','นครนายก','นครปฐม','นครพนม',
  'นครราชสีมา','นครศรีธรรมราช','นครสวรรค์','นนทบุรี','นราธิวาส',
  'น่าน','บึงกาฬ','บุรีรัมย์','ปทุมธานี','ประจวบคีรีขันธ์',
  'ปราจีนบุรี','ปัตตานี','พระนครศรีอยุธยา','พะเยา','พังงา',
  'พัทลุง','พิจิตร','พิษณุโลก','เพชรบุรี','เพชรบูรณ์',
  'แพร่','ภูเก็ต','มหาสารคาม','มุกดาหาร','แม่ฮ่องสอน',
  'ยโสธร','ยะลา','ร้อยเอ็ด','ระนอง','ระยอง',
  'ราชบุรี','ลพบุรี','ลำปาง','ลำพูน','เลย',
  'ศรีสะเกษ','สกลนคร','สงขลา','สตูล','สมุทรปราการ',
  'สมุทรสงคราม','สมุทรสาคร','สระแก้ว','สระบุรี','สิงห์บุรี',
  'สุโขทัย','สุพรรณบุรี','สุราษฎร์ธานี','สุรินทร์','หนองคาย',
  'หนองบัวลำภู','อ่างทอง','อำนาจเจริญ','อุดรธานี','อุตรดิตถ์',
  'อุทัยธานี','อุบลราชธานี',
]

const store = useVehicleMasterStore()
const confirm = useConfirm()
const { success: showSuccess, error: showError } = useAppToast()

const search = ref('')
const dialogVisible = ref(false)
const editTarget = ref<VehicleProvince | null>(null)
const form = ref({ name: '' })

const filtered = computed(() => {
  const q = search.value.toLowerCase()
  if (!q) return store.provinces
  return store.provinces.filter((p) => p.name.toLowerCase().includes(q))
})

function openAdd() {
  editTarget.value = null
  form.value = { name: '' }
  store.error = null
  dialogVisible.value = true
}

function openEdit(item: VehicleProvince) {
  editTarget.value = item
  form.value = { name: item.name }
  store.error = null
  dialogVisible.value = true
}

async function submitForm() {
  if (!form.value.name.trim()) {
    store.error = 'กรุณากรอกชื่อจังหวัด'
    return
  }
  const ok = editTarget.value
    ? await store.updateProvince(editTarget.value.id, form.value)
    : await store.createProvince(form.value)

  if (ok) {
    showSuccess(editTarget.value ? 'แก้ไขสำเร็จ' : 'เพิ่มจังหวัดสำเร็จ')
    dialogVisible.value = false
  } else {
    showError(store.error ?? 'เกิดข้อผิดพลาด')
  }
}

function confirmDelete(item: VehicleProvince) {
  confirm.require({
    message: `ยืนยันลบจังหวัด "${item.name}"?`,
    header: 'ยืนยันการลบ',
    icon: 'pi pi-exclamation-triangle',
    acceptClass: 'p-button-danger',
    acceptLabel: 'ลบ',
    rejectLabel: 'ยกเลิก',
    accept: async () => {
      const ok = await store.deleteProvince(item.id)
      if (ok) showSuccess('ลบสำเร็จ')
      else showError(store.error ?? 'เกิดข้อผิดพลาด')
    },
  })
}

async function seedProvinces() {
  const existing = new Set(store.provinces.map((p) => p.name))
  const toAdd = THAILAND_PROVINCES.filter((name) => !existing.has(name))
  if (toAdd.length === 0) {
    showSuccess('มีจังหวัดทั้งหมดครบแล้ว')
    return
  }
  let failed = 0
  for (const name of toAdd) {
    const ok = await store.createProvince({ name })
    if (!ok) failed++
  }
  if (failed === 0) showSuccess(`เพิ่ม ${toAdd.length} จังหวัดสำเร็จ`)
  else showError(`เพิ่มสำเร็จ ${toAdd.length - failed} รายการ, ล้มเหลว ${failed} รายการ`)
}

onMounted(() => store.fetchProvinces())
</script>
