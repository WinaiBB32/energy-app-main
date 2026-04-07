@echo off
echo ========================================
echo  BUILD + PACKAGE for Windows Server 2016
echo ========================================

set SERVER_IP=192.168.99.125
set OUTPUT=C:\deploy-output

echo.
echo [1/3] Building Frontend...
cd my-web-app
call npm install
call npm run build
cd ..

echo.
echo [2/3] Publishing .NET API...
cd EnergyApp.API
dotnet publish -c Release -o ..\publish\api --self-contained false
cd ..

echo.
echo [3/3] Copying to output folder...
if not exist "%OUTPUT%\frontend" mkdir "%OUTPUT%\frontend"
if not exist "%OUTPUT%\api" mkdir "%OUTPUT%\api"

xcopy /E /Y my-web-app\dist\* "%OUTPUT%\frontend\"
xcopy /E /Y publish\api\* "%OUTPUT%\api\"
copy nginx.conf "%OUTPUT%\nginx.conf"

echo.
echo ========================================
echo  DONE! Files ready at: %OUTPUT%
echo ========================================
echo.
echo ขั้นตอนต่อไปบน Server:
echo   1. Copy folder C:\deploy-output ไปที่ Server
echo   2. Copy frontend\ ไปที่ C:\energy-app\frontend\
echo   3. Copy api\ ไปที่ C:\energy-app\api\
echo   4. Copy nginx.conf ไปที่ C:\nginx\conf\nginx.conf
echo   5. รัน install-service.bat บน Server
echo ========================================
pause
