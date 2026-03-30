# Energy App Management System

ระบบบริหารจัดการทรัพยากร (ไฟฟ้า, น้ำ, น้ำมัน, โทรศัพท์) ภายในองค์กร แบบ Full-stack โดยใช้ .NET API เป็น Backend และ Vue 3 เป็น Frontend

---

## 🛠 สิ่งที่ต้องติดตั้งก่อนเริ่ม (Prerequisites)

เพื่อให้สามารถรันโปรเจกต์ได้ทุกส่วน คุณจำเป็นต้องติดตั้งโปรแกรมดังต่อไปนี้:

1.  **Docker Desktop:** สำหรับรัน Database (PostgreSQL) และ Redis
2.  **.NET 8 SDK:** สำหรับพัฒนาและรัน Backend API
3.  **Node.js (v20+):** สำหรับพัฒนาและรัน Frontend (Vue 3)
4.  **IDE ที่แนะนำ:**
    - **VS Code** (พร้อม Extension: C#, Volar, Tailwind CSS)
    - หรือ **Visual Studio 2022**

---

## 🚀 ขั้นตอนการติดตั้งเมื่อย้ายเครื่องใหม่

### 1. การเตรียม Database (Docker)

โปรเจกต์นี้ใช้ PostgreSQL และ Redis ผ่าน Docker Compose เพื่อความสะดวก:

1.  เปิด Terminal ที่ Root Directory ของโปรเจกต์
2.  รันคำสั่งเพื่อเริ่มระบบฐานข้อมูล:
    ```bash
    docker-compose up -d
    ```
    _ระบบจะสร้าง Container สำหรับ PostgreSQL (Port 5432) และ Redis (Port 6379) ให้โดยอัตโนมัติ_

### 2. การตั้งค่า Backend (.NET API)

1.  เข้าไปที่โฟลเดอร์ Backend:
    ```bash
    cd EnergyApp.API
    ```
2.  Restore dependencies:
    ```bash
    dotnet restore
    ```
3.  **สร้างฐานข้อมูลและตาราง (Database Migrations):**
    เพื่อให้ฐานข้อมูลมีโครงสร้างตามโค้ดล่าสุด ให้รันคำสั่ง:
    ```bash
    dotnet ef database update --project EnergyApp.API
    ```
    _(หากยังไม่มี dotnet-ef ให้ติดตั้งด้วยคำสั่ง: `dotnet tool install --global dotnet-ef`)_
4.  **รัน Backend:**
    ```bash
    dotnet run --project EnergyApp.API
    ```
    _API จะรันอยู่ที่ `http://localhost:5007` (ตรวจสอบ Port ได้ใน `launchSettings.json`)_

### 3. การตั้งค่า Frontend (Vue 3)

1.  เปิด Terminal ใหม่แล้วเข้าไปที่โฟลเดอร์ Frontend:
    ```bash
    cd my-web-app
    ```
2.  ติดตั้ง Dependencies:
    ```bash
    npm install
    ```
3.  **ตั้งค่า Environment Variables:**
    - คัดลอกไฟล์ `.env.example` เป็น `.env`
    - ตรวจสอบค่า `VITE_API_URL` ให้ตรงกับ URL ของ Backend API:
      ```env
      VITE_API_URL=http://localhost:5007/api/v1
      ```
4.  **รัน Frontend:**
    ```bash
    npm run dev
    ```
    _Frontend จะรันอยู่ที่ `http://localhost:5173`_

---

## 📝 ข้อมูลที่ควรทราบ

- **สิทธิ์ผู้ใช้ (Roles):**
  - ผู้สมัครคนแรกของระบบจะได้รับสิทธิ์เป็น **SuperAdmin** โดยอัตโนมัติ
  - ผู้สมัครคนต่อๆ ไปจะเป็นสิทธิ์ **User** ซึ่งต้องรอการอนุมัติจาก Admin ก่อน
- **การเข้าสู่ระบบ:**
  - ใช้ Email และ Password ที่สมัครไว้
  - หากลืมรหัสผ่านหรือต้องการแก้ไขฐานข้อมูลโดยตรง สามารถใช้เครื่องมือเช่น pgAdmin หรือ DBeaver เชื่อมต่อไปที่ `localhost:5432`

---

## 📂 โครงสร้างโปรเจกต์

- `/EnergyApp.API` - ซอร์สโค้ดฝั่ง Backend (.NET 8, Entity Framework Core)
- `/my-web-app` - ซอร์สโค้ดฝั่ง Frontend (Vue 3, Pinia, PrimeVue, Tailwind CSS)
- `docker-compose.yml` - ไฟล์คอนฟิกสำหรับ Infrastructure (DB, Cache)
- `.gitignore` - ตั้งค่าการยกเว้นไฟล์ที่ไม่ควรขึ้น Git (bin, obj, node_modules, .env)


Port ที่ว่างอยู่ (สำหรับ Docker ของเรา):

Port	ใช้สำหรับ	สถานะ
5173	Frontend	ว่าง ✓
5008	Backend API	ว่าง ✓
5432	PostgreSQL	ว่าง ✓
6379	Redis	ว่าง ✓
Docker compose ที่ทำไว้ใช้ port เหล่านี้ได้เลยครับ ไม่ชนกับอะไร