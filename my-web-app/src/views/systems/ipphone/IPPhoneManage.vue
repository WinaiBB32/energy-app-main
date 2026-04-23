<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/services/api'
import { useAppToast } from '@/composables/useAppToast'
import { usePermissions } from '@/composables/usePermissions'

import Card from 'primevue/card'
import InputText from 'primevue/inputtext'
import Button from 'primevue/button'
import Message from 'primevue/message'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Dialog from 'primevue/dialog'

interface IPPhone {
  id?: string
  extension: string
  ownerName: string
  location: string
  description: string
}

const toast = useAppToast()
const { isSystemAdmin } = usePermissions()
const isAdmin = isSystemAdmin('ipphone')

const ipphones = ref<IPPhone[]>([])
const isLoading = ref(false)
const dialogVisible = ref(false)
const isEditMode = ref(false)
const editId = ref<string>('')
const form = ref<IPPhone>({ extension: '', ownerName: '', location: '', description: '' })
const errorMsg = ref('')
const successMsg = ref('')

const fetchIPPhones = async () => {
  isLoading.value = true
  try {
    const res = await api.get('/IPPhoneDirectory', { params: { take: 9999, skip: 0 } })
    ipphones.value = res.data.items || []
  } catch (e) {
    toast.fromError(e, 'โหลดข้อมูล IP-Phone ไม่สำเร็จ')
  } finally {
    isLoading.value = false
  }
}
onMounted(fetchIPPhones)

const openNewDialog = () => {
  isEditMode.value = false
  editId.value = ''
  form.value = { extension: '', ownerName: '', location: '', description: '' }
  dialogVisible.value = true
}
const openEditDialog = (item: IPPhone) => {
  isEditMode.value = true
  editId.value = item.id || ''
  form.value = { ...item }
  dialogVisible.value = true
}
const saveIPPhone = async () => {
  if (!form.value.extension || !form.value.ownerName) {
    errorMsg.value = 'กรุณากรอกข้อมูลให้ครบ'
    return
  }
  try {
    if (isEditMode.value && editId.value) {
      await api.put(`/IPPhone/${editId.value}`, form.value)
      successMsg.value = 'แก้ไขข้อมูลสำเร็จ'
    } else {
      await api.post('/IPPhone', form.value)
      successMsg.value = 'เพิ่มข้อมูลสำเร็จ'
    }
    await fetchIPPhones()
    dialogVisible.value = false
  } catch (e) {
    toast.fromError(e, 'บันทึกข้อมูลไม่สำเร็จ')
  }
}
const deleteIPPhone = async (id: string) => {
  if (!confirm('ยืนยันการลบข้อมูลนี้?')) return
  try {
    await api.delete(`/IPPhone/${id}`)
    await fetchIPPhones()
  } catch (e) {
    toast.fromError(e, 'ลบข้อมูลไม่สำเร็จ')
  }
}
</script>

<template>
  <div class="max-w-4xl mx-auto pb-10">
    <h2 class="text-2xl font-bold mb-4">จัดการข้อมูล IP-Phone</h2>
    <Button
      label="เพิ่มหมายเลขใหม่"
      icon="pi pi-plus"
      @click="openNewDialog"
      v-if="isAdmin"
      class="mb-4"
    />
    <DataTable :value="ipphones" :loading="isLoading" paginator :rows="10" stripedRows>
      <Column field="extension" header="เบอร์ภายใน" />
      <Column field="ownerName" header="ชื่อผู้ใช้" />
      <Column field="location" header="สถานที่" />
      <Column field="description" header="หมายเหตุ" />
      <Column header="จัดการ" v-if="isAdmin">
        <template #body="{ data }">
          <Button icon="pi pi-pencil" text @click="openEditDialog(data)" />
          <Button icon="pi pi-trash" text severity="danger" @click="deleteIPPhone(data.id)" />
        </template>
      </Column>
    </DataTable>
    <Dialog v-model:visible="dialogVisible" modal :header="isEditMode ? 'แก้ไข' : 'เพิ่ม'">
      <form @submit.prevent="saveIPPhone" class="flex flex-col gap-4">
        <Message v-if="errorMsg" severity="error">{{ errorMsg }}</Message>
        <Message v-if="successMsg" severity="success">{{ successMsg }}</Message>
        <InputText v-model="form.extension" placeholder="เบอร์ภายใน" />
        <InputText v-model="form.ownerName" placeholder="ชื่อผู้ใช้" />
        <InputText v-model="form.location" placeholder="สถานที่" />
        <InputText v-model="form.description" placeholder="หมายเหตุ" />
        <div class="flex gap-2 justify-end">
          <Button label="บันทึก" type="submit" />
          <Button label="ยกเลิก" severity="secondary" @click="dialogVisible = false" />
        </div>
      </form>
    </Dialog>
  </div>
</template>

<style scoped></style>
