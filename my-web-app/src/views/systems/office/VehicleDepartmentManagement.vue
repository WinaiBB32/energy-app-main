<template>
  <div class="p-4 space-y-4">
    <!-- Header -->
    <div class="flex flex-wrap justify-between items-center gap-2">
      <div>
        <h2 class="text-xl font-bold text-gray-800">จัดการหน่วยงาน (ระบบรถยนต์)</h2>
        <p class="text-sm text-gray-500 mt-0.5">รายการหน่วยงานสำหรับเลือกในฟอร์มบันทึกรถยนต์</p>
      </div>
      <Button label="เพิ่มหน่วยงาน" icon="pi pi-plus" @click="openAdd" />
    </div>

    <!-- Search -->
    <InputText v-model="search" placeholder="ค้นหาชื่อหน่วยงาน..." class="w-full md:w-72" />

    <!-- Table -->
    <DataTable
      :value="filtered"
      :loading="store.isLoading"
      paginator
      :rows="15"
      dataKey="id"
      class="p-datatable-sm"
    >
      <template #empty>ยังไม่มีข้อมูลหน่วยงาน</template>
      <Column field="id" header="#" style="width: 70px" />
      <Column field="name" header="ชื่อหน่วยงาน" sortable />
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
      :header="editTarget ? 'แก้ไขหน่วยงาน' : 'เพิ่มหน่วยงาน'"
      :style="{ width: '380px' }"
      modal
    >
      <div class="flex flex-col gap-1 pt-2">
        <label class="text-sm font-medium">ชื่อหน่วยงาน *</label>
        <InputText v-model="form.name" placeholder="เช่น ฝ่ายบริหาร" class="w-full" />
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
import type { VehicleDepartment } from '@/types/vehicle'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import ConfirmDialog from 'primevue/confirmdialog'

const store = useVehicleMasterStore()
const confirm = useConfirm()
const { success: showSuccess, error: showError } = useAppToast()

const search = ref('')
const dialogVisible = ref(false)
const editTarget = ref<VehicleDepartment | null>(null)
const form = ref({ name: '' })

const filtered = computed(() => {
  const q = search.value.toLowerCase()
  if (!q) return store.departments
  return store.departments.filter((d) => d.name.toLowerCase().includes(q))
})

function openAdd() {
  editTarget.value = null
  form.value = { name: '' }
  store.error = null
  dialogVisible.value = true
}

function openEdit(item: VehicleDepartment) {
  editTarget.value = item
  form.value = { name: item.name }
  store.error = null
  dialogVisible.value = true
}

async function submitForm() {
  if (!form.value.name.trim()) {
    store.error = 'กรุณากรอกชื่อหน่วยงาน'
    return
  }
  const ok = editTarget.value
    ? await store.updateDepartment(editTarget.value.id, form.value)
    : await store.createDepartment(form.value)

  if (ok) {
    showSuccess(editTarget.value ? 'แก้ไขสำเร็จ' : 'เพิ่มหน่วยงานสำเร็จ')
    dialogVisible.value = false
  } else {
    showError(store.error ?? 'เกิดข้อผิดพลาด')
  }
}

function confirmDelete(item: VehicleDepartment) {
  confirm.require({
    message: `ยืนยันลบหน่วยงาน "${item.name}"?`,
    header: 'ยืนยันการลบ',
    icon: 'pi pi-exclamation-triangle',
    acceptClass: 'p-button-danger',
    acceptLabel: 'ลบ',
    rejectLabel: 'ยกเลิก',
    accept: async () => {
      const ok = await store.deleteDepartment(item.id)
      if (ok) showSuccess('ลบสำเร็จ')
      else showError(store.error ?? 'เกิดข้อผิดพลาด')
    },
  })
}

onMounted(() => store.fetchDepartments())
</script>
