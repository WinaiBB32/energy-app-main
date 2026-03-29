# Energy App

ระบบติดตามและจัดการการใช้พลังงานแบบ Real-time สำหรับ ไฟฟ้า, โซลาร์เซลล์, น้ำ และเชื้อเพลิง

## Tech Stack

- **Frontend:** Vue 3 + TypeScript
- **UI:** PrimeVue + Tailwind CSS
- **State Management:** Pinia
- **Backend/Database:** Firebase (Auth, Firestore, Storage)
- **Build Tool:** Vite

## ความต้องการของระบบ

- **Node.js:** `^20.19.0` หรือ `>=22.12.0`
- **npm:** `>=10`
- บัญชี [Firebase](https://firebase.google.com/) พร้อม Project ที่ตั้งค่าไว้แล้ว

## การติดตั้ง

### 1. Clone โปรเจกต์

```bash
git clone <repository-url>
cd energy-app/my-web-app
```

### 2. ติดตั้ง Dependencies

```bash
npm install
```

### 3. ตั้งค่า Environment Variables

สร้างไฟล์ `.env` ที่ root ของโปรเจกต์ (`my-web-app/.env`) แล้วใส่ค่า Firebase Config:

```env
VITE_FIREBASE_API_KEY=your_api_key_here
VITE_FIREBASE_AUTH_DOMAIN=your_project_id.firebaseapp.com
VITE_FIREBASE_PROJECT_ID=your_project_id
VITE_FIREBASE_STORAGE_BUCKET=your_project_id.firebasestorage.app
VITE_FIREBASE_MESSAGING_SENDER_ID=your_messaging_sender_id
VITE_FIREBASE_APP_ID=your_app_id
```

> ดูค่า Config ได้จาก Firebase Console → Project Settings → Your apps

### 4. รัน Development Server

```bash
npm run dev
```

เปิดเบราว์เซอร์ไปที่ `http://localhost:5173`

## Scripts

| คำสั่ง | คำอธิบาย |
|--------|-----------|
| `npm run dev` | รัน development server พร้อม Hot Reload |
| `npm run build` | Type-check แล้ว build สำหรับ production |
| `npm run build-only` | Build สำหรับ production (ไม่ type-check) |
| `npm run preview` | Preview production build แบบ local |
| `npm run type-check` | ตรวจสอบ TypeScript types |
| `npm run lint` | รัน ESLint + Oxlint พร้อม auto-fix |
| `npm run format` | จัด format โค้ดด้วย Prettier |

## โครงสร้างโปรเจกต์

```
my-web-app/
├── src/
│   ├── assets/          # Static assets (CSS, รูปภาพ)
│   ├── components/
│   │   └── layout/      # Layout หลัก (Sidebar + Navbar)
│   ├── firebase/        # Firebase configuration
│   ├── router/          # Vue Router (route definitions)
│   ├── stores/          # Pinia stores (state management)
│   └── views/
│       ├── auth/        # หน้า Login
│       ├── PortalView.vue
│       └── systems/     # Dashboard ของแต่ละระบบ
│           ├── System1_* (ไฟฟ้า & โซลาร์)
│           ├── System2_* (น้ำ)
│           └── System3_* (เชื้อเพลิง)
├── public/
├── index.html
├── vite.config.ts
├── tsconfig.json
└── .env                 # Environment variables (ไม่ commit ไปใน git)
```

## ฟีเจอร์หลัก

- **ระบบ 1 – ไฟฟ้า & โซลาร์:** ติดตามการใช้ไฟฟ้าและการผลิตพลังงานโซลาร์
- **ระบบ 2 – น้ำ:** ติดตามการใช้น้ำ
- **ระบบ 3 – เชื้อเพลิง:** ติดตามการใช้เชื้อเพลิง
- **Authentication:** Login ด้วย Email/Password หรือ Google
- **Real-time Dashboard:** แสดงข้อมูลพลังงานแบบ Real-time ผ่าน Firestore

## IDE ที่แนะนำ

- [VS Code](https://code.visualstudio.com/) + [Volar](https://marketplace.visualstudio.com/items?itemName=Vue.volar) (ปิด Vetur ถ้ามี)
- ติดตั้ง [Vue.js DevTools](https://devtools.vuejs.org/) สำหรับ Browser
