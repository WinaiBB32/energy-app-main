// src/stores/auth.ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { auth, db } from '../firebase/config'
import { onAuthStateChanged, signOut, type User } from 'firebase/auth'
import { doc, onSnapshot } from 'firebase/firestore'
import { useRouter } from 'vue-router'
import { logAudit } from '@/utils/auditLogger'

// 💡 กำหนด Interface ให้เป๊ะ 100% แทนการใช้ any
export interface UserProfile {
  departmentId: string
  role: 'user' | 'admin' | 'superadmin'
  status: 'pending' | 'active' | 'suspended'
  accessibleSystems: string[]
  /** รหัสระบบสำหรับผู้ดูแลระบบย่อย เช่น system5 — ตั้งจากหน้า User Management */
  adminSystems?: string[]
  displayName?: string
  email?: string
}

export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(null)
  const userProfile = ref<UserProfile | null>(null) // Type-Safe เต็มรูปแบบ!
  const loading = ref<boolean>(true)
  const router = useRouter()

  let profileUnsubscribe: (() => void) | null = null

  onAuthStateChanged(auth, (currentUser) => {
    // รีเซ็ต loading ทุกครั้งที่ auth state เปลี่ยน
    // เพื่อบังคับให้ router guard รอ userProfile โหลดเสร็จก่อนเสมอ
    // (ป้องกัน race condition: loading=false แต่ userProfile ยัง null)
    loading.value = true
    user.value = currentUser

    if (currentUser) {
      if (profileUnsubscribe) profileUnsubscribe()
      const userRef = doc(db, 'users', currentUser.uid)
      profileUnsubscribe = onSnapshot(userRef, (docSnap) => {
        userProfile.value = docSnap.exists() ? (docSnap.data() as UserProfile) : null
        loading.value = false
      })
    } else {
      userProfile.value = null
      if (profileUnsubscribe) profileUnsubscribe()
      loading.value = false
    }
  })

  const logout = async (): Promise<void> => {
    try {
      if (user.value && userProfile.value) {
        await logAudit(
          { uid: user.value.uid, displayName: userProfile.value.displayName ?? user.value.email ?? '', email: user.value.email ?? '', role: userProfile.value.role },
          'LOGOUT', 'Auth', 'User logout',
        )
      }
      if (profileUnsubscribe) profileUnsubscribe()
      await signOut(auth)
      user.value = null
      userProfile.value = null
      router.push('/login')
    } catch (error: unknown) {
      console.error('Logout Error:', error)
    }
  }

  return { user, userProfile, loading, logout }
})