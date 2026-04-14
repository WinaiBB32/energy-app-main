import axios from 'axios'

// 1. สร้าง Axios Instance
const api = axios.create({
  // เปลี่ยน 7187 เป็นพอร์ตที่รันอยู่ในหน้า Swagger ของคุณ
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5008/api/v1',
  timeout: 10000, // ป้องกันแอปค้าง ถ้ายิง API แล้ว Backend ไม่ตอบสนองใน 10 วินาที
  headers: {
    'Content-Type': 'application/json',
  },
})

// 2. Request Interceptor (ด่านตรวจก่อนส่งออก)
api.interceptors.request.use(
  (config) => {
    const requestUrl = (config.url || '').toLowerCase()
    const isAuthEndpoint =
      requestUrl.includes('/auth/login') || requestUrl.includes('/auth/register')

    // ดึง Token จาก LocalStorage โดยตรง (หลีกเลี่ยงการ import useAuthStore เพื่อลด Circular Dependency)
    const token = localStorage.getItem('jwt_token')

    // ถ้ามี Token อยู่ ให้แนบไปกับ Header เสมอ
    if (token && !isAuthEndpoint) {
      config.headers.Authorization = `Bearer ${token}`
    }

    return config
  },
  (error) => {
    return Promise.reject(error)
  },
)

// 3. Response Interceptor (ด่านตรวจขากลับ - เผื่ออนาคตทำระบบ Auto Logout ถ้า Token หมดอายุ)
api.interceptors.response.use(
  (response) => response,
  (error) => {
    const requestUrl = (error.config?.url || '').toLowerCase()
    const isAuthEndpoint =
      requestUrl.includes('/auth/login') || requestUrl.includes('/auth/register')

    // ถ้า Backend ตอบกลับมาว่า 401 Unauthorized (Token หมดอายุ หรือ ไม่มีสิทธิ์)
    if (error.response && error.response.status === 401 && !isAuthEndpoint) {
      console.warn('Token expired or unauthorized. Please login again.')
      // อนาคตสามารถสั่งเด้งกลับหน้า Login ตรงนี้ได้
    }
    return Promise.reject(error)
  },
)

export default api
