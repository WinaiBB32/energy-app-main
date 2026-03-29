# Energy App

ระบบติดตามและจัดการการใช้พลังงานแบบ Real-time สำหรับ ไฟฟ้า, โซลาร์เซลล์, น้ำ และเชื้อเพลิง

## Tech Stack

- **Frontend:** Vue 3 + TypeScript
- **UI:** PrimeVue + Tailwind CSS
- **State Management:** Pinia
- **Backend/Database:** .NET API + PostgreSQL
- **Build Tool:** Vite

## การเตรียมระบบ

โปรเจกต์นี้ทำงานร่วมกับ Backend API คุณต้องรันระบบฐานข้อมูลและ API ก่อนที่จะเริ่มรัน Frontend

1. **ฐานข้อมูล:** ใช้ Docker Compose ที่ Root Directory (`docker-compose up -d`)
2. **API:** เข้าไปที่ `EnergyApp.API` แล้วรัน `dotnet run`

## การติดตั้ง Frontend

### 1. ติดตั้ง Dependencies

```bash
npm install
```

### 2. ตั้งค่า Environment Variables

สร้างไฟล์ `.env` โดยคัดลอกมาจาก `.env.example`:

```env
VITE_API_URL=https://localhost:7187/api/v1
```

### 3. รัน Development Server

```bash
npm run dev
```

เปิดเบราว์เซอร์ไปที่ `http://localhost:5173`

## Scripts

| คำสั่ง               | คำอธิบาย                                 |
| -------------------- | ---------------------------------------- |
| `npm run dev`        | รัน development server พร้อม Hot Reload  |
| `npm run build`      | Type-check แล้ว build สำหรับ production  |
| `npm run build-only` | Build สำหรับ production (ไม่ type-check) |
| `npm run preview`    | Preview production build แบบ local       |
| `npm run type-check` | ตรวจสอบ TypeScript types                 |
| `npm run lint`       | รัน ESLint + Oxlint พร้อม auto-fix       |
| `npm run format`     | จัด format โค้ดด้วย Prettier             |

## โครงสร้างโปรเจกต์

```
my-web-app/
├── src/
│   ├── assets/          # Static assets (CSS, รูปภาพ)
│   ├── components/
│   │   └── layout/      # Layout หลัก (Sidebar + Navbar)
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
- **Authentication:** Login ด้วย Email/Password
- **Real-time Dashboard:** แสดงข้อมูลพลังงานแบบ Real-time

## IDE ที่แนะนำ

- [VS Code](https://code.visualstudio.com/) + [Volar](https://marketplace.visualstudio.com/items?itemName=Vue.volar) (ปิด Vetur ถ้ามี)
- ติดตั้ง [Vue.js DevTools](https://devtools.vuejs.org/) สำหรับ Browser
