<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import {
    collection, doc, writeBatch, serverTimestamp, query, orderBy, getDocs, Timestamp, increment
} from 'firebase/firestore'
// Firebase Removed
import { useAuthStore } from '@/stores/auth'
import { useAppToast } from '@/composables/useAppToast'

defineOptions({ name: 'MeetingRecordSystem' })

const toast = useAppToast()
const authStore = useAuthStore()

import Card from 'primevue/card'
import InputNumber from 'primevue/inputnumber'
import DatePicker from 'primevue/datepicker'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Tag from 'primevue/tag'

// ─── State ──────────────────────────────────────────────────────────────────
const currentUserRole = computed(() => authStore.userProfile?.role || 'user')

// กำหนด Default เป็นเดือนที่แล้ว
const getLastMonth = () => {
    const now = new Date()
    return new Date(now.getFullYear(), now.getMonth() - 1, 1)
}
const selectedMonth = ref<Date | null>(getLastMonth())

interface RoomEntry {
    roomId: string
    roomName: string
    usageCount: number | null
}
const roomEntries = ref<RoomEntry[]>([])

const isSubmitting = ref<boolean>(false)
const isLoadingRooms = ref<boolean>(true)
const successMessage = ref<string>('')
const errorMessage = ref<string>('')

// ─── ดึงข้อมูล (One-time fetch) ─────────────────────────────────────────────
onMounted(async () => {
    try {
        const roomSnap = await getDocs(query(collection(db, 'meeting_rooms'), orderBy('name')))
        roomEntries.value = roomSnap.docs.map(d => ({
            roomId: d.id,
            roomName: d.data().name as string,
            usageCount: null
        }))
    } catch {
        toast.error('ไม่สามารถโหลดข้อมูลรายชื่อห้องประชุมได้')
    } finally {
        isLoadingRooms.value = false
    }
})

// ─── บันทึกข้อมูล (Batch + Aggregation) ──────────────────────────────────────
const submitForm = async (): Promise<void> => {
    successMessage.value = ''
    errorMessage.value = ''

    if (!selectedMonth.value) {
        errorMessage.value = 'กรุณาเลือก "รอบเดือน" ให้ครบถ้วน'
        return
    }

    const filledEntries = roomEntries.value.filter(r => r.usageCount !== null && r.usageCount >= 0)

    if (filledEntries.length === 0) {
        errorMessage.value = 'กรุณากรอกจำนวนการใช้งานอย่างน้อย 1 ห้องประชุม'
        return
    }

    try {
        isSubmitting.value = true
        const batch = writeBatch(db)

        // บังคับให้เป็นวันที่ 1 ของเดือนนั้นๆ เสมอ เพื่อความเป็นระเบียบ
        const targetDate = new Date(selectedMonth.value.getFullYear(), selectedMonth.value.getMonth(), 1)
        const recordMonthTs = Timestamp.fromDate(targetDate)
        const monthKey = `${targetDate.getFullYear()}-${String(targetDate.getMonth() + 1).padStart(2, '0')}`

        let totalUsageForMonth = 0

        filledEntries.forEach(entry => {
            const newRecordRef = doc(collection(db, 'meeting_records'))
            batch.set(newRecordRef, {
                recordMonthTs: recordMonthTs,
                roomId: entry.roomId,
                roomName: entry.roomName,
                usageCount: entry.usageCount,
                recordedByName: authStore.userProfile?.displayName || authStore.user?.email || 'ไม่ระบุชื่อ',
                recordedByUid: authStore.user?.uid || 'unknown',
                createdAt: serverTimestamp()
            })
            totalUsageForMonth += (entry.usageCount || 0)
        })

        const summaryRef = doc(db, 'monthly_summaries', `MEETING_${monthKey}`)

        batch.set(summaryRef, {
            monthKey: monthKey,
            totalUsage: increment(totalUsageForMonth),
            updatedAt: serverTimestamp()
        }, { merge: true })

        await batch.commit()

        successMessage.value = `บันทึกข้อมูลสถิติประจำเดือนสำเร็จ (${filledEntries.length} ห้อง)`
        roomEntries.value.forEach(r => r.usageCount = null)

    } catch (error: unknown) {
        errorMessage.value = error instanceof Error ? `เกิดข้อผิดพลาด: ${error.message}` : 'เกิดข้อผิดพลาดในการบันทึก'
    } finally {
        isSubmitting.value = false
    }
}
</script>

<template>
    <div class="max-w-4xl mx-auto pb-10">
        <div class="mb-6 flex flex-col sm:flex-row justify-between sm:items-end gap-4">
            <div>
                <h2 class="text-2xl font-bold text-gray-800">
                    <i class="pi pi-file-edit text-teal-500 mr-2"></i>บันทึกสถิติห้องประชุม
                </h2>
                <p class="text-gray-500 mt-1">บันทึกจำนวนการใช้ห้องประชุมส่วนกลางประจำเดือน</p>
            </div>
            <Tag :value="`ระดับสิทธิ์: ${currentUserRole.toUpperCase()}`"
                :severity="currentUserRole === 'superadmin' ? 'danger' : currentUserRole === 'admin' ? 'warn' : 'info'"
                rounded />
        </div>

        <Card class="shadow-sm border-none mt-2">
            <template #content>
                <div v-if="isLoadingRooms" class="flex justify-center items-center py-10">
                    <i class="pi pi-spin pi-spinner text-teal-500 text-4xl"></i>
                </div>

                <form v-else @submit.prevent="submitForm" class="flex flex-col gap-6">
                    <Message v-if="successMessage" severity="success" :closable="true">{{ successMessage }}</Message>
                    <Message v-if="errorMessage" severity="error" :closable="true">{{ errorMessage }}</Message>

                    <div class="bg-gray-50 p-5 rounded-xl border border-gray-100 flex flex-col gap-2 w-full md:w-1/2">
                        <label class="font-semibold text-sm text-gray-700">รอบเดือนที่ใช้งาน <span
                                class="text-red-500">*</span></label>
                        <DatePicker v-model="selectedMonth" view="month" dateFormat="MM yy" class="w-full" showIcon
                            placeholder="-- เลือกเดือน/ปี --" />
                    </div>

                    <div class="border border-gray-200 rounded-xl overflow-hidden mt-2">
                        <table class="w-full text-left text-sm">
                            <thead class="bg-teal-50 border-b border-teal-100">
                                <tr>
                                    <th class="px-4 py-3 font-bold text-teal-800 w-2/3">รายชื่อห้องประชุม</th>
                                    <th class="px-4 py-3 font-bold text-teal-800 text-center">จำนวนครั้งที่ใช้งาน</th>
                                </tr>
                            </thead>
                            <tbody class="divide-y divide-gray-100">
                                <tr v-for="entry in roomEntries" :key="entry.roomId"
                                    class="hover:bg-gray-50/50 transition-colors">
                                    <td class="px-4 py-3">
                                        <span class="font-semibold text-gray-700">{{ entry.roomName }}</span>
                                    </td>
                                    <td class="px-4 py-2">
                                        <InputNumber v-model="entry.usageCount" :min="0" placeholder="-" class="w-full"
                                            inputClass="text-center font-bold text-teal-700" />
                                    </td>
                                </tr>
                            </tbody>
                        </table>
                    </div>

                    <p class="text-xs text-amber-600 font-medium bg-amber-50 p-2 rounded border border-amber-100">
                        <i class="pi pi-info-circle mr-1"></i> ห้องประชุมใดที่ไม่มีการใช้งานในรอบเดือนนั้น
                        สามารถเว้นว่างไว้ได้
                    </p>

                    <div class="flex justify-end mt-2 pt-4 border-t border-gray-100">
                        <Button type="submit" label="บันทึกสถิติรายเดือน" icon="pi pi-save" severity="teal"
                            :loading="isSubmitting" class="px-8 py-3 text-lg" :disabled="roomEntries.length === 0" />
                    </div>
                </form>
            </template>
        </Card>
    </div>
</template>

<style scoped>
:deep(.p-inputnumber-input) {
    width: 100%;
}
</style>
