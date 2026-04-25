# Energy App Management System

ระบบบริหารจัดการทรัพยากรพลังงานในองค์กร (ไฟฟ้า, น้ำ, น้ำมัน, โทรศัพท์) แบบ Full-stack

- Backend: .NET 8 Web API + Entity Framework Core
- Frontend: Vue 3 + Vite + Pinia + PrimeVue
- Database: PostgreSQL

---

## โครงสร้างโปรเจกต์

```
energy-app-main/
├── EnergyApp.API/          # Backend (.NET 8 API)
│   ├── Controllers/        # แยกตามระบบงาน (Admin, Auth, Core, Energy, Communication, Maintenance, Office)
│   ├── Models/             # แยกตามระบบงาน
│   ├── DTOs/               # Data Transfer Objects
│   ├── Data/               # AppDbContext.cs
│   ├── Hubs/               # SignalR (MaintenanceHub.cs)
│   ├── Services/           # SystemErrorLogStore
│   ├── Migrations/         # EF Core migrations
│   └── Program.cs
├── my-web-app/             # Frontend (Vue 3 + TypeScript)
│   └── src/
│       ├── views/          # แยกตามระบบงาน (systems/, admin/, auth/)
│       ├── stores/         # Pinia stores (auth, vehicle, vehicleMaster)
│       ├── services/       # api.ts (Axios), realtime.ts (SignalR)
│       ├── composables/    # useAuth, usePermissions, useAppToast
│       └── router/
├── deploy.bat              # Script build + package สำหรับ Deploy
├── nginx.conf              # Config Nginx สำหรับ Server
├── EnergyAppWorkspace.sln  # Visual Studio solution
└── README.md
```

---

## Port ที่ใช้

| Service | Port |
|---------|------|
| Frontend Dev (Vite) | 5173 |
| Frontend Production (XAMPP) | 8080 |
| Backend API | 5008 |
| PostgreSQL | 5432 |

---

## สิ่งที่ต้องติดตั้ง

### เครื่อง Dev
1. .NET 8 SDK
2. Node.js 20+ LTS
3. PostgreSQL 15+

### Server (Windows Server)
1. .NET 8 Runtime (ไม่ต้อง SDK)
2. PostgreSQL 15+
3. XAMPP (Apache เสิร์ฟ Frontend) + Nginx (proxy API)

---

## การติดตั้งสำหรับ Development

### 1) ตั้งค่าฐานข้อมูล

สร้าง database:
```sql
CREATE DATABASE "energy-app";
```

แก้ไข `EnergyApp.API/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=energy-app;Username=postgres;Password=your_password;SslMode=Disable;"
  },
  "AllowedOrigins": "http://localhost:5173"
}
```

### 2) รัน Backend API

```powershell
cd EnergyApp.API

# ครั้งแรก — ติดตั้ง EF CLI
dotnet tool install --global dotnet-ef

dotnet restore
dotnet ef database update
dotnet run
```

- Swagger: http://localhost:5008/swagger
- Base API: http://localhost:5008

### 3) รัน Frontend

```powershell
cd my-web-app
npm install
npm run dev
```

เปิดใช้งานที่: http://localhost:5173

---

## การ Deploy บน Windows Server

### ขั้นตอนที่ 1 — Build และ Package (บนเครื่อง Dev)

```bat
cd EnergyAppWorkspace
deploy.bat
```

สคริปต์จะ build frontend + publish API แล้วรวมไฟล์ไว้ที่ `publish/`

### ขั้นตอนที่ 2 — Copy ไฟล์ไปที่ Server

| จาก (Dev) | ไปที่ (Server) |
|---|---|
| `my-web-app/dist/` | `C:\energy-app\frontend\` |
| `publish/api/` | `C:\energy-app\api\` |
| `nginx.conf` | `C:\nginx\conf\nginx.conf` |

### ขั้นตอนที่ 3 — ตั้งค่า appsettings.Production.json บน Server

แก้ไขไฟล์ `C:\energy-app\api\appsettings.Production.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=energy-app;Username=postgres;Password=your_password;SslMode=Disable;"
  },
  "JwtSettings": {
    "Secret": "your_secret_key_min_32_chars"
  },
  "AllowedOrigins": "http://server-ip,http://server-ip:8080"
}
```

### ขั้นตอนที่ 4 — ติดตั้ง API เป็น Windows Service (Admin)

```bat
sc create EnergyAppAPI binPath= "C:\energy-app\api\EnergyApp.API.exe --environment Production" start= auto DisplayName= "EnergyApp API"
sc start EnergyAppAPI
```

หยุด/เริ่มใหม่:
```bat
sc stop EnergyAppAPI && sc start EnergyAppAPI
```

### ขั้นตอนที่ 5 — ติดตั้ง Frontend บน XAMPP

1. Copy ไฟล์จาก `my-web-app/dist/` ไปที่ `C:\xampp\htdocs\energy-app\`
2. สร้างไฟล์ `C:\xampp\htdocs\energy-app\.htaccess`:

```apache
<IfModule mod_rewrite.c>
    RewriteEngine On
    RewriteBase /energy-app/
    RewriteRule ^index\.html$ - [L]
    RewriteCond %{REQUEST_FILENAME} !-f
    RewriteCond %{REQUEST_FILENAME} !-d
    RewriteRule . /energy-app/index.html [L]
</IfModule>
```

3. เปิดใช้งานที่: `http://server-ip:8080/energy-app/`

### ขั้นตอนที่ 6 — เปิด Firewall Port 5008

รันบน Server ด้วย Admin:
```bat
netsh advfirewall firewall add rule name="EnergyApp API" dir=in action=allow protocol=TCP localport=5008
```

---

## วิธีใช้งานระบบ (เริ่มต้น)

1. เปิดเว็บแล้ว **สมัครผู้ใช้คนแรก** — จะได้สิทธิ์ **SuperAdmin** อัตโนมัติ
2. ตั้งค่าข้อมูลพื้นฐาน: หน่วยงาน / อาคาร
3. เพิ่มผู้ใช้และกำหนดสิทธิ์
4. เริ่มบันทึกข้อมูลระบบ: ไฟฟ้า, น้ำ, เชื้อเพลิง, โทรศัพท์

---

## Troubleshooting

### เชื่อมต่อ API ไม่ได้จากเครื่องอื่น
- ตรวจสอบว่าเปิด Firewall port 5008 แล้ว
- ตรวจสอบว่า API รันอยู่: เปิด `http://server-ip:5008/swagger` บน Server

### Frontend โหลดไม่ได้ (404 assets)
- ตรวจสอบว่า `vite.config.ts` มี `base: '/energy-app/'`
- Build ใหม่แล้ว copy ไปแทนที่

### Vue Router ไม่ทำงาน (refresh แล้ว 404)
- ตรวจสอบว่ามีไฟล์ `.htaccess` ใน `C:\xampp\htdocs\energy-app\`

### ต่อฐานข้อมูลไม่ได้
- ตรวจสอบว่า PostgreSQL รันอยู่ที่ port 5432
- ตรวจสอบ `Host`, `Username`, `Password` ใน `appsettings.json`

### CORS Error
- ตรวจสอบ `AllowedOrigins` ใน `appsettings.json` ให้ตรงกับ URL ของ Frontend
