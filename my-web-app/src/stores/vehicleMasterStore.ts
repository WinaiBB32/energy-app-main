import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '@/services/api'
import type { VehicleDepartment, VehicleProvince, VehicleMasterPayload } from '@/types/vehicle'

export const useVehicleMasterStore = defineStore('vehicleMaster', () => {
  const departments = ref<VehicleDepartment[]>([])
  const provinces = ref<VehicleProvince[]>([])
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  // ─── Departments ───────────────────────────────────────────
  async function fetchDepartments(): Promise<void> {
    isLoading.value = true
    error.value = null
    try {
      const res = await api.get<VehicleDepartment[]>('/office/vehicle-departments')
      departments.value = res.data
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'ดึงข้อมูลหน่วยงานไม่สำเร็จ'
    } finally {
      isLoading.value = false
    }
  }

  async function createDepartment(payload: VehicleMasterPayload): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.post('/office/vehicle-departments', payload)
      await fetchDepartments()
      return true
    } catch (err) {
      error.value =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        (err instanceof Error ? err.message : 'บันทึกข้อมูลไม่สำเร็จ')
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function updateDepartment(id: number, payload: VehicleMasterPayload): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.put(`/office/vehicle-departments/${id}`, payload)
      await fetchDepartments()
      return true
    } catch (err) {
      error.value =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        (err instanceof Error ? err.message : 'แก้ไขข้อมูลไม่สำเร็จ')
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function deleteDepartment(id: number): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.delete(`/office/vehicle-departments/${id}`)
      departments.value = departments.value.filter((d) => d.id !== id)
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'ลบข้อมูลไม่สำเร็จ'
      return false
    } finally {
      isLoading.value = false
    }
  }

  // ─── Provinces ─────────────────────────────────────────────
  async function fetchProvinces(): Promise<void> {
    isLoading.value = true
    error.value = null
    try {
      const res = await api.get<VehicleProvince[]>('/office/vehicle-provinces')
      provinces.value = res.data
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'ดึงข้อมูลจังหวัดไม่สำเร็จ'
    } finally {
      isLoading.value = false
    }
  }

  async function createProvince(payload: VehicleMasterPayload): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.post('/office/vehicle-provinces', payload)
      await fetchProvinces()
      return true
    } catch (err) {
      error.value =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        (err instanceof Error ? err.message : 'บันทึกข้อมูลไม่สำเร็จ')
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function updateProvince(id: number, payload: VehicleMasterPayload): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.put(`/office/vehicle-provinces/${id}`, payload)
      await fetchProvinces()
      return true
    } catch (err) {
      error.value =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        (err instanceof Error ? err.message : 'แก้ไขข้อมูลไม่สำเร็จ')
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function deleteProvince(id: number): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.delete(`/office/vehicle-provinces/${id}`)
      provinces.value = provinces.value.filter((p) => p.id !== id)
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'ลบข้อมูลไม่สำเร็จ'
      return false
    } finally {
      isLoading.value = false
    }
  }

  return {
    departments,
    provinces,
    isLoading,
    error,
    fetchDepartments,
    createDepartment,
    updateDepartment,
    deleteDepartment,
    fetchProvinces,
    createProvince,
    updateProvince,
    deleteProvince,
  }
})
