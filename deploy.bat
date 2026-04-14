@echo off
echo ========================================
echo  BUILD + PACKAGE for Deploy
echo ========================================

set OUTPUT=C:\deploy-output

echo.
echo [1/3] Building Frontend...
cd my-web-app
call npm install
call npm run build
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Frontend build ล้มเหลว
    pause
    exit /b 1
)
cd ..

echo.
echo [2/3] Publishing .NET API...
cd EnergyApp.API
dotnet publish -c Release -o ..\publish\api --self-contained false
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Backend publish ล้มเหลว
    pause
    exit /b 1
)
cd ..

echo.
echo ========================================
echo  DONE! Files ready at: %OUTPUT%
echo ========================================
echo.
echo ขั้นตอนต่อไป — Copy ด้วยมือไปที่ Server:
echo   frontend  →  C:\xampp\htdocs\energy-app\
echo   api       →  C:\energy-app\api\
echo.
echo หลัง copy เสร็จ บน Server:
echo   sc stop EnergyAppAPI ^& sc start EnergyAppAPI
echo ========================================
pause
echo.
echo ========================================
echo  DONE! Build & Publish เสร็จสมบูรณ์
echo ========================================
echo.
echo กรุณานำไฟล์ที่ build แล้วไปวางบน Server ด้วยตนเอง
echo   frontend  →  my-web-app\dist\
echo   api       →  publish\api\
echo.
echo หลัง copy เสร็จ บน Server:
echo   sc stop EnergyAppAPI ^& sc start EnergyAppAPI
echo ========================================
pause
