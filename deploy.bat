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
echo ========================================
echo  DONE! Build & Publish เสร็จสมบูรณ์
echo ========================================
echo.
echo Copy ไฟล์ไปบน Server:
echo   frontend  →  my-web-app\dist\     ไปไว้ที่  C:\xampp\htdocs\energy-app\
echo   api       →  publish\api\         ไปไว้ที่  C:\energy-app\api\
echo.
echo ครั้งแรก: รัน install-service.bat บน Server (ด้วย Admin)
echo ครั้งต่อไป:
echo   sc stop EnergyAppAPI ^& sc start EnergyAppAPI
echo ========================================
pause
