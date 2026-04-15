# EnergyApp — Project Reference for AI

## Overview
ระบบบริหารจัดการพลังงานและสำนักงาน (Energy & Office Management System)
- Backend: ASP.NET Core 8 REST API + SignalR
- Frontend: Vue 3 SPA
- Database: PostgreSQL
- Deploy: Windows Server, Nginx reverse proxy

---

## Tech Stack

### Backend (`EnergyApp.API/`)
| ส่วน | รายละเอียด |
|---|---|
| Framework | .NET 8, ASP.NET Core |
| ORM | Entity Framework Core 8 + Npgsql (PostgreSQL) |
| Auth | JWT Bearer (BCrypt password hash) |
| Realtime | SignalR (`MaintenanceHub`) |
| Docs | Swagger (Swashbuckle) |
| Port | `5008` (http://0.0.0.0:5008) |
| API prefix | `/api/v1/` |

### Frontend (`my-web-app/`)
| ส่วน | รายละเอียด |
|---|---|
| Framework | Vue 3 + TypeScript + Vite 7 |
| UI | PrimeVue 4 + Tailwind CSS 4 |
| State | Pinia |
| HTTP | Axios (instance ใน `src/services/api.ts`) |
| Realtime | @microsoft/signalr |
| Router | Vue Router 5 |
| Base path | `/energy-app/` |
| Dev port | `5173` |

---

## Project Structure

```
energy-app-main/
├── EnergyApp.API/          ← Backend .NET
│   ├── Controllers/        ← แยกตามระบบงาน (Admin, Auth, Core, Energy, Communication, Maintenance, Office)
│   ├── Models/             ← แยกตามระบบงาน (Core, Energy, Communication, Maintenance, Office)
│   ├── DTOs/               ← AuthDto.cs (ยังรวมไฟล์เดียว)
│   ├── Data/AppDbContext.cs ← DbContext หลัก (ทุก DbSet อยู่ที่นี่)
│   ├── Hubs/               ← MaintenanceHub.cs (SignalR)
│   ├── Services/           ← ISystemErrorLogStore, InMemorySystemErrorLogStore
│   ├── Migrations/         ← EF Core migrations
│   ├── AppConstants.cs     ← Roles, UserStatus constants
│   └── Program.cs          ← Entry point, DI, middleware
├── my-web-app/             ← Frontend Vue
│   └── src/
│       ├── views/          ← แยกตามระบบงาน (systems/, admin/, auth/)
│       ├── stores/auth.ts  ← Pinia auth store
│       ├── services/api.ts ← Axios instance (baseURL จาก VITE_API_URL)
│       ├── services/realtime.ts ← SignalR client
│       ├── composables/    ← useAuth, usePermissions, useAppToast
│       └── router/index.ts ← Vue Router
├── deploy.bat              ← Build frontend + publish backend
├── nginx.conf              ← Nginx config (port 8080, proxy /api/ → :5008)
├── EnergyAppWorkspace.sln  ← Visual Studio solution
└── CLAUDE.md               ← ไฟล์นี้
```

---

## Database Models

### Core (ใช้ร่วมทุกระบบ)
| Model | Table | หมายเหตุ |
|---|---|---|
| `User` | Users | มี `AccessibleSystems[]`, `AdminSystems[]` (PostgreSQL text[]) |
| `Department` | Departments | |
| `Building` | Buildings | |
| `AuditLog` | AuditLogs | บันทึกการกระทำสำคัญ |

### Energy (ระบบพลังงาน)
| Model | Table |
|---|---|
| `ElectricityRecord` | ElectricityRecords |
| `ElectricityBill` | ElectricityBills |
| `SolarProduction` | SolarProductions |
| `WaterRecord` | WaterRecords |
| `FuelRecord` | FuelRecords |
| `FuelReceipt` | FuelReceipts |
| `FuelType` | FuelTypes |

### Communication (ระบบสื่อสาร)
| Model | Table |
|---|---|
| `IPPhoneDirectory` | IPPhoneDirectories |
| `IPPhoneCallLog` | IPPhoneCallLogs |
| `IPPhoneMonthStat` | IPPhoneMonthStats |
| `TelephoneRecord` | TelephoneRecords |
| `ChatMessage` | ChatMessages |

### Maintenance (ระบบซ่อมบำรุง)
| Model | Table |
|---|---|
| `ServiceRequest` | ServiceRequests |
| `SparePart` | SpareParts |
| `SparePartTransaction` | SparePartTransactions |
| `SparePartIssueRequest` | SparePartIssueRequests |
| `SparePartIssueRequestItem` | SparePartIssueRequestItems |

### Office (ระบบสำนักงาน)
| Model | Table |
|---|---|
| `MeetingRoom` | MeetingRooms |
| `MeetingRecord` | MeetingRecords |
| `PostalRecord` | PostalRecords |
| `SarabanRecord` | SarabanRecords |
| `VehicleRecord` | VehicleRecords |

---

## Roles & Permissions

```csharp
// AppConstants.cs
Roles.SuperAdmin   = "SuperAdmin"
Roles.Admin        = "admin"
Roles.User         = "User"
Roles.Technician   = "technician"
Roles.Supervisor   = "supervisor"
Roles.AdminBuilding = "adminbuilding"

UserStatus: pending → approved/rejected → active
```

- `User.AccessibleSystems` — รายการระบบที่ user เข้าได้
- `User.AdminSystems` — รายการระบบที่ user เป็น admin

---

## Frontend Systems (views/systems/)
| โฟลเดอร์ | ระบบงาน |
|---|---|
| `electricity/` | ไฟฟ้า + โซลาร์ |
| `fuel/` | เชื้อเพลิง + ใบเสร็จน้ำมัน |
| `water/` | น้ำประปา |
| `ipphone/` | IP Phone |
| `telephone/` | โทรศัพท์ |
| `meeting/` | ห้องประชุม |
| `postal/` | ไปรษณีย์/พัสดุ |
| `saraban/` | สารบรรณ |
| `building-maintenance/` | ซ่อมบำรุงอาคาร |

---

## Key Conventions

- API route prefix: `/api/v1/[controller]`
- Controller namespace ตรงกับโฟลเดอร์: `Controllers/Energy/ElectricityRecordController.cs`
- Model namespace ตรงกับโฟลเดอร์: `Models/Energy/ElectricityRecord.cs`
- Frontend API call ผ่าน `api` instance เท่านั้น (ใน `src/services/api.ts`)
- JWT token เก็บใน `localStorage` key `jwt_token`
- `appsettings.json` = template (ไม่มี credentials จริง)
- `appsettings.Development.json` = credentials จริง dev (ไม่ commit)
- `appsettings.Production.json` = credentials จริง prod (ไม่ commit)

---

## Deploy

```
deploy.bat
  → npm run build  (output: my-web-app/dist/)
  → dotnet publish (output: publish/api/)

Server layout:
  frontend → C:\energy-app\frontend\   (served by Nginx)
  api      → C:\energy-app\api\        (Windows Service)

Nginx: port 8080
  /energy-app/  → serve static files
  /api/         → proxy to localhost:5008
  /hubs/        → proxy WebSocket to localhost:5008

Restart API: sc stop EnergyAppAPI && sc start EnergyAppAPI
```

---

## .gitignore — ไฟล์ที่ไม่ commit
- `publish/` — compiled output (มี binary + credentials)
- `appsettings.Production.json`, `appsettings.Development.json`
- `node_modules/`, `dist/`, `bin/`, `obj/`, `.vs/`
