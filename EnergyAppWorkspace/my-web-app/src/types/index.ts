// src/types/index.ts
// Centralized shared TypeScript interfaces for the energy-app

import type { Timestamp } from 'firebase/firestore'
import type { User } from 'firebase/auth'

// ─── Auth / User ──────────────────────────────────────────────────────────────

export type UserRole = 'user' | 'admin' | 'superadmin'
export type UserStatus = 'pending' | 'active' | 'suspended'

export interface UserProfile {
  departmentId: string
  role: UserRole
  status: UserStatus
  accessibleSystems: string[]
  adminSystems?: string[]
  displayName?: string
  email?: string
}

export interface AppUser {
  uid: string
  displayName: string
  email: string
  role: UserRole
  status: UserStatus
  departmentId?: string
  accessibleSystems?: string[]
  createdAt?: Timestamp
}

// ─── Audit ────────────────────────────────────────────────────────────────────

export interface AuditLog {
  id?: string
  uid: string
  displayName: string
  email: string
  role: string
  action: string
  module: string
  detail: string
  createdAt: Timestamp | null
}

// ─── Common / Shared ──────────────────────────────────────────────────────────

export interface Department {
  id: string
  name: string
}

export interface Building {
  id: string
  name: string
}

// ─── Electricity ──────────────────────────────────────────────────────────────

export interface ElectricityRecord {
  docReceiveNumber: string
  docNumber: string
  buildingId: string
  billingCycle: Date | null
  peaUnitUsed: number | null
  peaAmount: number | null
  ftRate: number | null
}

export interface FetchedElectricityRecord {
  id: string
  type: string
  docReceiveNumber: string
  docNumber: string
  buildingId: string
  billingCycle: Timestamp | null
  peaUnitUsed: number
  peaAmount: number
  ftRate: number
  recordedBy: string
  createdAt: Timestamp
}

export interface SolarRecord {
  recordDate: Date | null
  buildingId: string
  solarUnitProduced: number | null
  note: string
}

export interface FetchedSolarRecord {
  id: string
  type: string
  recordDate: Timestamp | null
  buildingId: string
  solarUnitProduced: number
  note: string
  recordedBy: string
  createdAt: Timestamp
}

// ─── Water ────────────────────────────────────────────────────────────────────

export interface WaterRecord {
  docReceiveNumber: string
  docNumber: string
  invoiceNumber: string
  billingCycle: Date | null
  registrationNo: string
  userName: string
  usageAddress: string
  readingDate: Date | null
  currentMeter: number | null
  waterUnitUsed: number | null
  rawWaterCharge: number | null
  waterAmount: number | null
  monthlyServiceFee: number | null
  vatAmount: number | null
}

export interface FetchedWaterRecord {
  id: string
  docReceiveNumber: string
  docNumber: string
  invoiceNumber: string
  billingCycle: Timestamp | null
  registrationNo: string
  userName: string
  usageAddress: string
  readingDate: Timestamp | null
  currentMeter: number
  waterUnitUsed: number
  rawWaterCharge: number
  waterAmount: number
  monthlyServiceFee: number
  vatAmount: number
  totalAmount: number
  recordedBy: string
  createdAt: Timestamp
}

// ─── Fuel ─────────────────────────────────────────────────────────────────────

export interface FuelRecord {
  id?: string
  fuelType: string
  amount: number | null
  unitPrice: number | null
  totalCost: number | null
  departmentId: string
  vehicleId?: string
  recordDate: Date | null
  note: string
  recordedBy: string
  createdAt: Timestamp | null
}

export interface OmitReceiptEntry {
  day: number | null
  month: string
  year: number | null
  detail: string
  receiptNo: string
  bookNo: string
  amount: number | null
  driverName: string
}

export interface ReceiptEntry extends OmitReceiptEntry {
  _editDate?: Date | null
}

export interface FetchedReceipt {
  id: string
  departmentId: string
  entries: ReceiptEntry[]
  totalAmount: number
  declarerName?: string
  declarerPosition?: string
  declarerDept?: string
  note?: string
  recordedByName: string
  recordedByUid: string
  createdAt: Timestamp
}

// ─── Telephone ────────────────────────────────────────────────────────────────

export interface TelephoneRecord {
  docReceiveNumber: string
  docNumber: string
  billingCycle: Date | null
  phoneNumber: string
  providerName: string
  usageAmount: number | null
  vatAmount: number | null
  totalAmount: number | null
}

export interface FetchedTelephoneRecord {
  id: string
  docReceiveNumber: string
  docNumber: string
  billingCycle: Timestamp | null
  phoneNumber: string
  providerName: string
  usageAmount: number
  vatAmount: number
  totalAmount: number
  recordedBy: string
  createdAt: Timestamp
}

// ─── Saraban ──────────────────────────────────────────────────────────────────

export interface SarabanRecord {
  id?: string
  docType: 'BOOK' | 'PERSON'
  docNumber: string
  subject: string
  fromOrganization: string
  toOrganization?: string
  receivedDate: Date | null
  dueDate?: Date | null
  priority: string
  status: string
  assignedTo?: string
  note?: string
  recordedBy: string
  departmentId: string
  createdAt: Timestamp | null
}

// ─── IP Phone ─────────────────────────────────────────────────────────────────

export interface LinkedUser {
  uid: string
  displayName: string
  email: string
}

export interface IPPhoneDirectory {
  id?: string
  ownerName: string
  location: string
  ipPhoneNumber: string
  analogNumber: string
  deviceCode: string
  departmentId: string
  workgroup: string
  description: string
  keywords: string
  isPublished: boolean
  linkedUsers?: LinkedUser[]
  linkedUserEmails?: string[]
}

export interface ServiceRequest {
  id: string
  title: string
  description: string
  category: string
  priority: string
  status: string
  extension: string
  requesterName: string
  requesterEmail: string
  requesterUid?: string
  assignedTo?: string
  note?: string
  createdAt: Timestamp | null
  updatedAt?: Timestamp | null
}

export interface ChatMessage {
  id: string
  text: string
  senderName: string
  senderEmail: string
  senderId: string
  senderRole: string
  createdAt: Timestamp | null
}

export interface PostalRecord {
  departmentId: string
  recordMonth: Date | null
  normalMail: number | null
  registeredMail: number | null
  emsMail: number | null
  totalMail: number | null
}

// ─── System 8: Meeting Room ───────────────────────────────────────────────────

export interface MeetingRoom {
  id: string
  name: string
  description: string
  createdAt?: Timestamp
}

export interface MeetingRecord {
  id?: string
  recordMonth: Date | null
  recordMonthTs?: Timestamp | null
  roomId: string
  roomName?: string
  usageCount: number | null
  recordedByUid?: string
  recordedByName?: string
  createdAt?: Timestamp
}

// Re-export firebase User for convenience
export type { User }
