<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { ApiError } from '@/services/api'
import api from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import { logAudit } from '@/utils/auditLogger'
import { useAppToast } from '@/composables/useAppToast'

import Card from 'primevue/card'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import Message from 'primevue/message'
import type { MeetingRoom } from '@/types'

interface RoomFormData {
    id: string
    name: string
    description: string
}

// ─── State ──────────────────────────────────────────────────────────────────
const authStore = useAuthStore()
const toast = useAppToast()

const meetingRooms = ref<MeetingRoom[]>([])
const isLoading = ref<boolean>(true)

const dialogVisible = ref<boolean>(false)
const isSaving = ref<boolean>(false)
const isEditMode = ref<boolean>(false)
const successMessage = ref<string>('')
const errorMessage = ref<string>('')

const currentRoom = ref<RoomFormData>({
    id: '', name: '', description: ''
})

// ─── Fetch Data ─────────────────────────────────────────────────────────────
const fetchMeetingRooms = async (): Promise<void> => {
    isLoading.value = true
    try {
        const res = await api.get('/MeetingRoom')
        meetingRooms.value = res.data
    } catch (error) {
        toast.fromError(error, 'ไม่สามารถโหลดข้อมูลห้องประชุมได้')
    } finally {
        isLoading.value = false
    }
}

onMounted(() => {
    fetchMeetingRooms()
})

// ─── Actions ────────────────────────────────────────────────────────────────
const openNewDialog = (): void => {
    successMessage.value = ''
    errorMessage.value = ''
    isEditMode.value = false
    currentRoom.value = { id: '', name: '', description: '' }
    dialogVisible.value = true
}

const openEditDialog = (room: MeetingRoom): void => {
    successMessage.value = ''
    errorMessage.value = ''
    isEditMode.value = true
    currentRoom.value = { id: room.id, name: room.name, description: room.description || '' }
    dialogVisible.value = true
}

const saveRoom = async (): Promise<void> => {
    if (!currentRoom.value.name.trim()) {
        errorMessage.value = 'กรุณากรอกชื่อห้องประชุม'
        return
    }

    try {
        isSaving.value = true
        errorMessage.value = ''

        const actor = {
            uid: authStore.user?.id ?? '',
            displayName: authStore.userProfile?.displayName ?? authStore.user?.firstName ?? authStore.user?.email ?? '',
            email: authStore.user?.email ?? '',
            role: authStore.userProfile?.role ?? authStore.user?.role?.toLowerCase?.() ?? 'user'
        }

        if (isEditMode.value) {
            await api.put(`/MeetingRoom/${currentRoom.value.id}`, {
                name: currentRoom.value.name.trim(),
                description: currentRoom.value.description.trim(),
            })
            logAudit(actor, 'UPDATE', 'MeetingRoomManagement', `แก้ไขห้องประชุม: ${currentRoom.value.name.trim()}`)
            successMessage.value = 'แก้ไขข้อมูลสำเร็จ'
        } else {
            await api.post('/MeetingRoom', {
                name: currentRoom.value.name.trim(),
                description: currentRoom.value.description.trim(),
            })
            logAudit(actor, 'CREATE', 'MeetingRoomManagement', `เพิ่มห้องประชุม: ${currentRoom.value.name.trim()}`)
            successMessage.value = 'เพิ่มห้องประชุมใหม่สำเร็จ'
        }

        await fetchMeetingRooms()
        setTimeout(() => { dialogVisible.value = false }, 900)
    } catch (error: unknown) {
        if (error instanceof ApiError) {
            errorMessage.value = error.response?.data?.message || 'เกิดข้อผิดพลาดในการบันทึกข้อมูล'
        } else if (error instanceof Error) {
            errorMessage.value = `Error: ${error.message}`
        } else {
            errorMessage.value = 'เกิดข้อผิดพลาดในการบันทึกข้อมูล'
        }
    } finally {
        isSaving.value = false
    }
}

const deleteRoom = async (id: string, name: string): Promise<void> => {
    if (confirm(`ยืนยันการลบ "${name}" ?\n(ข้อมูลสถิติที่เคยบันทึกไว้ในเดือนก่อนหน้าจะยังคงอยู่ แต่จะไม่แสดงชื่อห้องนี้ให้บันทึกในอนาคต)`)) {
        try {
            await api.delete(`/MeetingRoom/${id}`)
            logAudit(
                {
                    uid: authStore.user?.id ?? '',
                    displayName: authStore.userProfile?.displayName ?? authStore.user?.firstName ?? authStore.user?.email ?? '',
                    email: authStore.user?.email ?? '',
                    role: authStore.userProfile?.role ?? authStore.user?.role?.toLowerCase?.() ?? 'user'
                },
                'DELETE', 'MeetingRoomManagement', `ลบห้องประชุม: ${name}`,
            )
            await fetchMeetingRooms()
        } catch (error: unknown) {
            toast.fromError(error, 'ไม่สามารถลบข้อมูลห้องประชุมได้')
        }
    }
}
</script>

<template>
    <div class="max-w-5xl mx-auto pb-10">
        <div class="mb-6 flex flex-col md:flex-row md:items-end justify-between gap-4">
            <div>
                <h2 class="text-3xl font-bold text-gray-800">
                    <i class="pi pi-objects-column text-teal-500 mr-2"></i>จัดการห้องประชุม
                </h2>
                <p class="text-gray-500 mt-1">เพิ่ม ลบ หรือแก้ไข รายชื่อห้องประชุมส่วนกลาง (Master Data)</p>
            </div>
            <Button label="เพิ่มห้องประชุม" icon="pi pi-plus" severity="info" @click="openNewDialog" />
        </div>

        <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-6">
            <div class="bg-white rounded-2xl border border-gray-100 shadow-sm p-5 flex items-center gap-4">
                <div
                    class="w-12 h-12 rounded-xl bg-linear-to-br from-teal-400 to-cyan-500 flex items-center justify-center text-white shadow-sm">
                    <i class="pi pi-users text-xl"></i>
                </div>
                <div>
                    <p class="text-sm text-gray-500">ห้องประชุมทั้งหมด</p>
                    <p class="text-3xl font-bold text-gray-800">{{ meetingRooms.length }}</p>
                </div>
            </div>
        </div>

        <Card class="shadow-sm border-none overflow-hidden">
            <template #content>
                <DataTable :value="meetingRooms" :loading="isLoading" paginator :rows="15" stripedRows
                    responsiveLayout="scroll"
                    emptyMessage="ยังไม่มีรายชื่อห้องประชุม — กดปุ่ม 'เพิ่มห้องประชุม' เพื่อเริ่มต้น">
                    <Column header="#" style="width: 3.5rem">
                        <template #body="sp">
                            <span class="text-gray-400 text-sm font-mono">{{ sp.index + 1 }}</span>
                        </template>
                    </Column>

                    <Column header="ชื่อห้องประชุม">
                        <template #body="sp">
                            <div class="flex items-center gap-3">
                                <div
                                    class="w-9 h-9 rounded-lg bg-teal-50 border border-teal-100 flex items-center justify-center shrink-0">
                                    <i class="pi pi-objects-column text-teal-500"></i>
                                </div>
                                <div>
                                    <p class="font-bold text-gray-800">{{ sp.data.name }}</p>
                                    <p v-if="sp.data.description" class="text-xs text-gray-400 mt-0.5">{{
                                        sp.data.description }}</p>
                                </div>
                            </div>
                        </template>
                    </Column>

                    <Column header="รหัสอ้างอิง (ID)" style="width: 14rem">
                        <template #body="sp">
                            <span class="text-xs text-gray-400 font-mono">{{ sp.data.id }}</span>
                        </template>
                    </Column>

                    <Column header="จัดการ" style="width: 8rem">
                        <template #body="sp">
                            <div class="flex gap-1">
                                <Button icon="pi pi-pencil" severity="secondary" text rounded v-tooltip="'แก้ไข'"
                                    @click="openEditDialog(sp.data)" />
                                <Button icon="pi pi-trash" severity="danger" text rounded v-tooltip="'ลบ'"
                                    @click="deleteRoom(sp.data.id, sp.data.name)" />
                            </div>
                        </template>
                    </Column>
                </DataTable>
            </template>
        </Card>

        <Dialog v-model:visible="dialogVisible" modal :header="isEditMode ? 'แก้ไขห้องประชุม' : 'เพิ่มห้องประชุมใหม่'"
            :style="{ width: '440px' }" :draggable="false">
            <Message v-if="successMessage" severity="success" :closable="false" class="mb-4">{{ successMessage }}
            </Message>
            <Message v-if="errorMessage" severity="error" :closable="false" class="mb-4">{{ errorMessage }}</Message>

            <div class="flex flex-col gap-4 mt-2">
                <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">
                        ชื่อห้องประชุม <span class="text-red-500">*</span>
                    </label>
                    <InputText v-model="currentRoom.name" placeholder="เช่น ห้องประชุมชัยนาทนเรนทร, ห้องประชุม 1 ชั้น 3"
                        autofocus @keyup.enter="saveRoom" class="w-full" />
                </div>
                <div class="flex flex-col gap-2">
                    <label class="font-semibold text-sm text-gray-700">รายละเอียดเพิ่มเติม / พิกัด (ถ้ามี)</label>
                    <Textarea v-model="currentRoom.description" placeholder="เช่น ตึก A ชั้น 2 ความจุ 50 คน" rows="2"
                        class="w-full" />
                </div>
            </div>

            <template #footer>
                <Button label="ยกเลิก" severity="secondary" text @click="dialogVisible = false" />
                <Button :label="isEditMode ? 'บันทึกการแก้ไข' : 'เพิ่มห้องประชุม'" icon="pi pi-check" severity="info"
                    :loading="isSaving" @click="saveRoom" />
            </template>
        </Dialog>
    </div>
</template>

<style scoped>
:deep(.p-datatable-header-cell) {
    background-color: #f8fafc !important;
    color: #475569 !important;
    font-weight: 700 !important;
}
</style>
