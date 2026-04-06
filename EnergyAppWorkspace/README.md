# Energy App Management System

ระบบบริหารจัดการทรัพยากรพลังงานในองค์กร (ไฟฟ้า, น้ำ, น้ำมัน, โทรศัพท์) แบบ Full-stack

- Backend: .NET 8 Web API + Entity Framework Core
- Frontend: Vue 3 + Vite + Pinia + PrimeVue
- Database: PostgreSQL

README นี้อธิบายการติดตั้งและใช้งานแบบ ไม่ใช้ Docker

## โครงสร้างโปรเจกต์

```
EnergyAppWorkspace/
├── EnergyApp.API/   # Backend (.NET 8 API)
├── my-web-app/      # Frontend (Vue 3)
└── README.md
```

## Port ที่ใช้

| Service | Port |
|---------|------|
| Frontend (Vite) | 5173 |
| Backend API | 5008 |
| PostgreSQL | 5432 |

## สิ่งที่ต้องติดตั้ง

1. .NET 8 SDK
2. Node.js 20+ (แนะนำใช้ LTS)
3. PostgreSQL 15+ (หรือเวอร์ชันที่รองรับกับ Npgsql)
4. Git (ถ้าต้อง clone โค้ด)

## 1) ติดตั้งและตั้งค่า Database (PostgreSQL)

### 1.1 สร้างฐานข้อมูล

ตัวอย่างชื่อฐานข้อมูล: energy-app

```sql
CREATE DATABASE "energy-app";
```

### 1.2 ตั้งค่า Connection String ใน API

แก้ไฟล์ `EnergyApp.API/EnergyApp.API/appsettings.json` ในส่วน `ConnectionStrings:DefaultConnection`

ตัวอย่าง:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=energy-app;Username=postgres;Password=your_password;SslMode=Disable;"
  }
}
```

หมายเหตุ: ในไฟล์ปัจจุบันมีค่า Host/Username/Password ที่ผูกกับเครื่องเดิม ควรเปลี่ยนให้ตรงกับเครื่องที่ติดตั้งจริง

### 1.3 ตั้งค่า AllowedOrigins (CORS)

แก้ไฟล์ `EnergyApp.API/EnergyApp.API/appsettings.json`

```json
{
  "AllowedOrigins": "http://localhost:5173"
}
```

ถ้าต้องการหลาย origin ให้คั่นด้วย comma เช่น:

```json
"AllowedOrigins": "http://localhost:5173,http://192.168.1.100:5173"
```

## 2) รัน Backend API

เปิด terminal ที่โฟลเดอร์โปรเจกต์หลัก แล้วรัน:

```powershell
cd EnergyApp.API\EnergyApp.API
dotnet restore
```

ติดตั้ง EF CLI (ครั้งแรกในเครื่อง):

```powershell
dotnet tool install --global dotnet-ef
```

อัปเดตโครงสร้างฐานข้อมูลจาก Migration:

```powershell
dotnet ef database update
```

รัน API:

```powershell
dotnet run
```

เมื่อรันสำเร็จ:

- Swagger: http://localhost:5008/swagger
- Base API: http://localhost:5008

## 3) รัน Web App (Frontend)

เปิด terminal ใหม่ แล้วรัน:

```powershell
cd my-web-app
npm install
```

สร้างไฟล์ `.env` ในโฟลเดอร์ `my-web-app` แล้วใส่ค่า:

```env
VITE_API_URL=http://localhost:5008/api/v1
```

รันเว็บ:

```powershell
npm run dev
```

เปิดใช้งานที่: http://localhost:5173

## 4) วิธีใช้งานระบบ (เริ่มต้น)

1. เปิดเว็บที่ http://localhost:5173
2. สมัครผู้ใช้คนแรกในระบบ
3. ผู้ใช้คนแรกจะได้สิทธิ์ SuperAdmin อัตโนมัติ
4. ตั้งค่าหน่วยงาน/อาคาร/ข้อมูลพื้นฐาน
5. เริ่มบันทึกข้อมูลระบบต่าง ๆ เช่น ไฟฟ้า น้ำ เชื้อเพลิง และโทรศัพท์

## คำสั่งที่ใช้บ่อย

Backend:

```powershell
cd EnergyApp.API\EnergyApp.API
dotnet run
dotnet ef database update
```

Frontend:

```powershell
cd my-web-app
npm run dev
npm run build
```

## Troubleshooting

1. Web เรียก API ไม่ได้ (CORS)
   - ตรวจสอบ `AllowedOrigins` ใน `appsettings.json`
   - ให้มี `http://localhost:5173`

2. ต่อฐานข้อมูลไม่สำเร็จ
   - ตรวจสอบว่า PostgreSQL ทำงานอยู่ที่ port 5432
   - ตรวจสอบ `Host`, `Database`, `Username`, `Password` ใน `DefaultConnection`

3. Migration รันไม่ผ่าน
   - ตรวจสอบว่าได้ติดตั้ง `dotnet-ef` แล้ว
   - ใช้คำสั่ง `dotnet restore` ก่อน `dotnet ef database update`
