@echo off
echo ========================================
echo  INSTALL EnergyApp บน Windows Server
echo  (รัน script นี้บน Server ด้วย Admin)
echo ========================================

set API_PATH=C:\energy-app\api
set FRONTEND_PATH=C:\energy-app\frontend
set NGINX_PATH=C:\nginx

echo.
echo [1/4] สร้างโฟลเดอร์...
if not exist "C:\energy-app\api" mkdir "C:\energy-app\api"
if not exist "C:\energy-app\frontend" mkdir "C:\energy-app\frontend"

echo.
echo [2/4] Run Database Migration...
cd /d %API_PATH%
set ASPNETCORE_ENVIRONMENT=Production
dotnet EnergyApp.API.dll -- ef database update
if %errorlevel% neq 0 (
    echo Migration ล้มเหลว กรุณาตรวจสอบ connection string
    pause
    exit /b 1
)

echo.
echo [3/4] ติดตั้ง API เป็น Windows Service...
sc query EnergyAppAPI >nul 2>&1
if %errorlevel% equ 0 (
    echo หยุด service เก่า...
    sc stop EnergyAppAPI
    sc delete EnergyAppAPI
    timeout /t 3 >nul
)

sc create EnergyAppAPI binPath= "%API_PATH%\EnergyApp.API.exe --environment Production" start= auto DisplayName= "EnergyApp API"
sc description EnergyAppAPI "EnergyApp .NET 8 API Service"
sc start EnergyAppAPI
echo API Service ติดตั้งแล้ว

echo.
echo [4/4] ติดตั้ง nginx เป็น Windows Service...
sc query nginx >nul 2>&1
if %errorlevel% equ 0 (
    sc stop nginx
    sc delete nginx
    timeout /t 3 >nul
)

%NGINX_PATH%\nginx.exe -s quit >nul 2>&1
timeout /t 2 >nul

sc create nginx binPath= "%NGINX_PATH%\nginx.exe" start= auto DisplayName= "nginx Web Server"
sc start nginx
echo nginx Service ติดตั้งแล้ว

echo.
echo ========================================
echo  เสร็จสิ้น! เปิดเบราว์เซอร์ที่:
echo  http://192.168.99.125
echo ========================================
pause
