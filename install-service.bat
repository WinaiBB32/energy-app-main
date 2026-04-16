@echo off
echo ========================================
echo  INSTALL EnergyApp บน Windows Server
echo  (รัน script นี้บน Server ด้วย Admin)
echo ========================================

set API_PATH=C:\energy-app\api
set FRONTEND_PATH=C:\xampp\htdocs\energy-app

echo.
echo [1/3] สร้างโฟลเดอร์...
if not exist "%API_PATH%" mkdir "%API_PATH%"
if not exist "%FRONTEND_PATH%" mkdir "%FRONTEND_PATH%"

echo.
echo [2/3] Run Database Migration...
echo (Migration จะถูก apply อัตโนมัติเมื่อ API เริ่มทำงาน)
echo (หรือรัน: dotnet ef database update จาก source code ก่อน deploy)

echo.
echo [3/3] ติดตั้ง API เป็น Windows Service...
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
echo ========================================
echo  เสร็จสิ้น! เปิดเบราว์เซอร์ที่:
echo  http://192.168.99.125
echo ========================================
pause
