# Energy App Management System

ระบบบริหารจัดการทรัพยากร (ไฟฟ้า, น้ำ, น้ำมัน, โทรศัพท์) ภายในองค์กร แบบ Full-stack โดยใช้ .NET 8 API เป็น Backend และ Vue 3 เป็น Frontend

---

## โครงสร้างโปรเจกต์

```
EnergyAppWorkspace/
├── EnergyApp.API/          # Backend (.NET 8, Entity Framework Core, PostgreSQL)
├── my-web-app/             # Frontend (Vue 3, Pinia, PrimeVue, Tailwind CSS)
└── docker-compose.yml      # รัน Infrastructure ทั้งหมดด้วย Docker
```

---

## Port ที่ใช้งาน

| Service | Port |
|---------|------|
| Frontend | 5173 |
| Backend API | 5008 |
| PostgreSQL | 5432 |
| Redis | 6379 |

---

## วิธีย้ายไฟล์ไปเครื่อง Server และติดตั้ง (Deploy ด้วย Docker)

> เครื่อง Server ใช้ **Windows Server**

### สิ่งที่ต้องติดตั้งบนเครื่อง Server

1. **Docker Desktop for Windows**
   - ดาวน์โหลดที่ https://www.docker.com/products/docker-desktop/
   - ติดตั้งแล้ว Restart เครื่อง
   - เปิด Docker Desktop ให้รันอยู่ก่อนใช้งาน

2. **Git for Windows** (ถ้าใช้วิธี clone)
   - ดาวน์โหลดที่ https://git-scm.com/download/win

---

### ขั้นที่ 1 — ย้ายไฟล์ไปเครื่อง Server

**วิธีที่ 1: ผ่าน Git (แนะนำ)**

เปิด PowerShell หรือ Command Prompt บน Server แล้วรัน:

```powershell
git clone <repository-url>
cd energy-app-main\EnergyAppWorkspace
```

**วิธีที่ 2: ผ่าน WinSCP / FileZilla**

- Host: IP ของ Server (`192.168.99.125`)
- Protocol: SFTP หรือ FTP
- Copy โฟลเดอร์ `EnergyAppWorkspace` ทั้งโฟลเดอร์ไปวางบน Server เช่น `C:\energy-app\`

**วิธีที่ 3: Copy ผ่าน Network Share**

- Map Network Drive หรือ copy ผ่าน `\\192.168.99.125\` แล้ววางไฟล์ได้เลย

---

### ขั้นที่ 2 — ตั้งค่า Environment Variables (ครั้งแรก)

เปิด PowerShell ใน Server แล้วไปที่โฟลเดอร์โปรเจกต์:

```powershell
cd C:\energy-app\EnergyAppWorkspace
```

สร้างไฟล์ `.env` โดย copy จาก example หรือสร้างด้วย Notepad แล้วใส่เนื้อหา:

```powershell
copy .env.example .env
notepad .env
```

แก้ไขค่าใน `.env`:

```env
POSTGRES_USER=admin
POSTGRES_PASSWORD=เปลี่ยนรหัสผ่านที่นี่
POSTGRES_DB=EnergyAppDb
JWT_SECRET=เปลี่ยน-Secret-ที่นี่-ให้ยาวอย่างน้อย-32-ตัวอักษร
```

---

### ขั้นที่ 3 — Build และรัน

เปิด PowerShell **ในฐานะ Administrator** แล้วรัน:

```powershell
cd C:\energy-app\EnergyAppWorkspace
docker compose up -d --build
```

รอประมาณ 2-5 นาที (ครั้งแรก) Docker จะ:
1. Build Backend (.NET 8)
2. Build Frontend (Vue 3 → Nginx)
3. รัน PostgreSQL และ Redis
4. รัน API และ Frontend

---

### ขั้นที่ 4 — รัน Database Migration (ครั้งแรกเท่านั้น)

```powershell
docker compose exec api dotnet ef database update
```

---

### ขั้นที่ 5 — ตรวจสอบว่ารันสำเร็จ

```powershell
docker compose ps
```

ควรเห็น 4 services สถานะ `running`:

```
NAME                STATUS
energy_postgres     running (healthy)
energy_redis        running (healthy)
energy_api          running
energy_frontend     running
```

เปิด Browser แล้วเข้า:
- **Frontend:** `http://192.168.99.125:5173`
- **API (Swagger):** `http://192.168.99.125:5008/swagger`

---

## คำสั่งที่ใช้บ่อย (PowerShell)

```powershell
# ดู log ทั้งหมด
docker compose logs -f

# ดู log เฉพาะ service
docker compose logs -f api
docker compose logs -f frontend

# หยุด
docker compose down

# หยุดและลบข้อมูล DB (ระวัง!)
docker compose down -v

# Build ใหม่หลังแก้โค้ด
docker compose up -d --build api
docker compose up -d --build frontend
```

---

## ข้อมูลสำคัญ

- **User คนแรก** ที่สมัครจะได้สิทธิ์ **SuperAdmin** โดยอัตโนมัติ
- ถ้ายังไม่มีหน่วยงานในระบบ สามารถสมัครได้โดยไม่ต้องเลือกหน่วยงาน แล้วค่อยเพิ่มหน่วยงานทีหลัง
- ไฟล์ข้อมูล PostgreSQL เก็บใน Docker Volume `pgdata` (ไม่หายเมื่อ restart)
- หากต้องการดูฐานข้อมูลโดยตรง ใช้ pgAdmin หรือ DBeaver เชื่อมต่อที่ `server-ip:5432`

---

## การพัฒนาบนเครื่อง Local (Dev Mode)

### สิ่งที่ต้องติดตั้ง

- .NET 8 SDK
- Node.js v20+
- Docker Desktop

### ขั้นตอน

```bash
# 1. รัน Database
docker compose up -d postgres redis

# 2. รัน Backend
cd EnergyApp.API/EnergyApp.API
dotnet ef database update
dotnet run

# 3. รัน Frontend (Terminal ใหม่)
cd my-web-app
cp .env.example .env
npm install
npm run dev
```

Frontend: `http://localhost:5173`
API (Swagger): `http://localhost:5008/swagger`
