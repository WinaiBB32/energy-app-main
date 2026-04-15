import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '@/services/api'
import type { VehicleRecord, VehicleRecordCreatePayload, VehicleRecordUpdatePayload } from '@/types/vehicle'

export const useVehicleStore = defineStore('vehicle', () => {
  const records = ref<VehicleRecord[]>([])
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  async function fetchRecords(): Promise<void> {
    isLoading.value = true
    error.value = null
    try {
      const res = await api.get<VehicleRecord[]>('/office/vehicles')
      records.value = res.data
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'ดึงข้อมูลไม่สำเร็จ'
    } finally {
      isLoading.value = false
    }
  }

  async function createRecord(payload: VehicleRecordCreatePayload): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.post('/office/vehicles', payload)
      await fetchRecords()
      return true
    } catch (err) {
      error.value = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? (err instanceof Error ? err.message : 'บันทึกข้อมูลไม่สำเร็จ')
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function updateRecord(id: number, payload: VehicleRecordUpdatePayload): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.put(`/office/vehicles/${id}`, payload)
      await fetchRecords()
      return true
    } catch (err) {
      error.value = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? (err instanceof Error ? err.message : 'แก้ไขข้อมูลไม่สำเร็จ')
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function deleteRecord(id: number): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.delete(`/office/vehicles/${id}`)
      records.value = records.value.filter((v) => v.id !== id)
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'ลบข้อมูลไม่สำเร็จ'
      return false
    } finally {
      isLoading.value = false
    }
  }

  return { records, isLoading, error, fetchRecords, createRecord, updateRecord, deleteRecord }
})
