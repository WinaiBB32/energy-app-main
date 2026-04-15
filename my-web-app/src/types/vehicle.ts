export interface VehicleDepartment {
  id: number
  name: string
}

export interface VehicleProvince {
  id: number
  name: string
}

export interface VehicleMasterPayload {
  name: string
}

export interface VehicleRecord {
  id: number
  faceScanId: string
  fullName: string
  position: string
  department: string
  phoneNumber: string
  licensePlate: string
  brand: string
  model: string
  province: string
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface VehicleRecordCreatePayload {
  faceScanId: string
  fullName: string
  position: string
  department: string
  phoneNumber: string
  licensePlate: string
  brand: string
  model: string
  province: string
}

export type VehicleRecordUpdatePayload = VehicleRecordCreatePayload
