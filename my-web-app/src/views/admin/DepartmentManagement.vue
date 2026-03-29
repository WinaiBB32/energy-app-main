<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { collection, query, onSnapshot, doc, addDoc, updateDoc, deleteDoc, orderBy, serverTimestamp, Timestamp } from 'firebase/firestore'
import { db } from '@/firebase/config'
import { useAuthStore } from '@/stores/auth'
import { logAudit } from '@/utils/auditLogger'
import { useAppToast } from '@/composables/useAppToast'

const authStore = useAuthStore()
const toast = useAppToast()

import Card from 'primevue/card'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'

// 1. Interface
interface Department {
    id: string
    name: string
    createdAt?: Timestamp
}

// 2. State
const departments = ref<Department[]>([])
const isLoading = ref<boolean>(true)
let unsubscribeSnapshot: () => void

const dialogVisible = ref<boolean>(false)
const isSaving = ref<boolean>(false)
const isEditMode = ref<boolean>(false)

const successMessage = ref<string>('')
const errorMessage = ref<string>('')

const currentDept = ref<{ id: string; name: string }>({ id: '', name: '' })

// 3. ดึงข้อมูลแบบ Real-time
onMounted(() => {
    const q = query(collection(db, 'departments'), orderBy('createdAt', 'asc'))

    unsubscribeSnapshot = onSnapshot(q, (snapshot) => {
        const fetched: Department[] = []
        snapshot.forEach((doc) => {
            fetched.push({ id: doc.id, ...doc.data() } as Department)
        })
        departments.value = fetched
        isLoading.value = false
    }, (error: unknown) => {
        toast.fromError(error, 'ไม่สามารถโหลดข้อมูลหน่วยงานได้')
        isLoading.value = false
    })
})

onUnmounted(() => {
    if (unsubscribeSnapshot) unsubscribeSnapshot()
})

// 4. ฟังก์ชันเปิดหน้าต่าง
const openNewDialog = () => {
    successMessage.value = ''
    errorMessage.value = ''
    isEditMode.value = false
    currentDept.value = { id: '', name: '' }
    dialogVisible.value = true
}

const openEditDialog = (dept: Department) => {
    successMessage.value = ''
    errorMessage.value = ''
    isEditMode.value = true
    currentDept.value = { ...dept }
    dialogVisible.value = true
}

// 5. บันทึกข้อมูล (สร้างใหม่ หรือ แก้ไข)
const saveDepartment = async () => {
    if (!currentDept.value.name.trim()) {
        errorMessage.value = 'กรุณากรอกชื่อหน่วยงาน'
        return
    }

    try {
        isSaving.value = true
        errorMessage.value = ''

        const actor = { uid: authStore.user?.uid ?? '', displayName: authStore.userProfile?.displayName ?? authStore.user?.email ?? '', email: authStore.user?.email ?? '', role: authStore.userProfile?.role ?? 'user' }
        if (isEditMode.value) {
            // โหมดแก้ไข
            const deptRef = doc(db, 'departments', currentDept.value.id)
            await updateDoc(deptRef, { name: currentDept.value.name })
            logAudit(actor, 'UPDATE', 'DepartmentManagement', `แก้ไขหน่วยงาน: ${currentDept.value.name}`)
            successMessage.value = 'แก้ไขชื่อหน่วยงานสำเร็จ'
        } else {
            // โหมดสร้างใหม่
            await addDoc(collection(db, 'departments'), {
                name: currentDept.value.name,
                createdAt: serverTimestamp()
            })
            logAudit(actor, 'CREATE', 'DepartmentManagement', `เพิ่มหน่วยงาน: ${currentDept.value.name}`)
            successMessage.value = 'เพิ่มหน่วยงานใหม่สำเร็จ'
        }

        setTimeout(() => { dialogVisible.value = false }, 1000)

    } catch (error: unknown) {
        errorMessage.value = error instanceof Error ? `Error: ${error.message}` : 'เกิดข้อผิดพลาด'
    } finally {
        isSaving.value = false
    }
}

// 6. ฟังก์ชันลบ
const deleteDepartment = async (id: string, name: string) => {
    if (confirm(`คุณแน่ใจหรือไม่ว่าต้องการลบหน่วยงาน "${name}" ?\n(การกระทำนี้ไม่สามารถย้อนกลับได้)`)) {
        try {
            await deleteDoc(doc(db, 'departments', id))
            logAudit(
              { uid: authStore.user?.uid ?? '', displayName: authStore.userProfile?.displayName ?? authStore.user?.email ?? '', email: authStore.user?.email ?? '', role: authStore.userProfile?.role ?? 'user' },
              'DELETE', 'DepartmentManagement', `ลบหน่วยงาน: ${name}`,
            )
        } catch (error) {
            toast.fromError(error, 'ไม่สามารถลบหน่วยงานได้')
            alert("เกิดข้อผิดพลาดในการลบข้อมูล")
        }
    }
}
</script>

<template>
    <div class="max-w-5xl mx-auto pb-10">
        <div class="mb-6 flex flex-col md:flex-row md:items-end justify-between gap-4">
            <div>
                <h2 class="text-3xl font-bold text-gray-800"><i
                        class="pi pi-sitemap text-gray-600 mr-2"></i>ตั้งค่าหน่วยงาน</h2>
                <p class="text-gray-500 mt-1">จัดการรายชื่อหน่วยงานทั้งหมดภายในองค์กร</p>
            </div>
            <Button label="เพิ่มหน่วยงานใหม่" icon="pi pi-plus" @click="openNewDialog" />
        </div>

        <Card class="shadow-sm border-none overflow-hidden">
            <template #content>
                <DataTable :value="departments" :loading="isLoading" paginator :rows="10" stripedRows
                    responsiveLayout="scroll" emptyMessage="ยังไม่มีข้อมูลหน่วยงาน">

                    <Column header="รหัสอ้างอิง (ID)" class="w-1/4">
                        <template #body="sp">
                            <span class="text-xs text-gray-400 font-mono">{{ sp.data.id }}</span>
                        </template>
                    </Column>

                    <Column header="ชื่อหน่วยงาน">
                        <template #body="sp">
                            <span class="font-bold text-gray-800 text-lg">{{ sp.data.name }}</span>
                        </template>
                    </Column>

                    <Column header="จัดการ" alignFrozen="right" class="w-32">
                        <template #body="sp">
                            <div class="flex gap-2">
                                <Button icon="pi pi-pencil" severity="secondary" text rounded aria-label="Edit"
                                    @click="openEditDialog(sp.data)" />
                                <Button icon="pi pi-trash" severity="danger" text rounded aria-label="Delete"
                                    @click="deleteDepartment(sp.data.id, sp.data.name)" />
                            </div>
                        </template>
                    </Column>

                </DataTable>
            </template>
        </Card>

        <Dialog v-model:visible="dialogVisible" modal :header="isEditMode ? 'แก้ไขหน่วยงาน' : 'เพิ่มหน่วยงานใหม่'"
            :style="{ width: '30rem' }">
            <Message v-if="successMessage" severity="success" :closable="false" class="mb-4">{{ successMessage }}
            </Message>
            <Message v-if="errorMessage" severity="error" :closable="false" class="mb-4">{{ errorMessage }}</Message>

            <div class="flex flex-col gap-2 mt-2">
                <label class="font-semibold text-sm text-gray-700">ชื่อหน่วยงาน <span
                        class="text-red-500">*</span></label>
                <InputText v-model="currentDept.name" placeholder="เช่น กองช่าง, สำนักปลัด" autofocus
                    @keyup.enter="saveDepartment" />
            </div>

            <template #footer>
                <Button label="ยกเลิก" icon="pi pi-times" text severity="secondary" @click="dialogVisible = false" />
                <Button :label="isEditMode ? 'บันทึกการแก้ไข' : 'เพิ่มหน่วยงาน'" icon="pi pi-check" :loading="isSaving"
                    @click="saveDepartment" />
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