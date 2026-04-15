@echo off
setlocal
chcp 65001 >nul

echo ========================================
echo  IslandCaller U盘验证密钥清理工具
echo ========================================
echo.
echo 这会清理当前用户下保存的 U盘验证密钥配置。
echo 不会删除其他普通设置。
echo.
echo 将执行以下操作：
echo  1. 关闭 U盘验证开关
echo  2. 删除 PublicKey
echo  3. 删除 ProtectedPrivateKey
echo  4. 清理旧版 Gacha 兼容字段
echo.
set /p CONFIRM=确认继续吗？输入 Y 继续：
if /I not "%CONFIRM%"=="Y" (
    echo 已取消。
    exit /b 0
)

echo.
echo [1/4] 关闭 U盘验证...
reg add "HKCU\Software\IslandCaller\UsbAuth" /v "Enabled" /t REG_DWORD /d 0 /f >nul 2>nul

echo [2/4] 删除 PublicKey...
reg delete "HKCU\Software\IslandCaller\UsbAuth" /v "PublicKey" /f >nul 2>nul

echo [3/4] 删除 ProtectedPrivateKey...
reg delete "HKCU\Software\IslandCaller\UsbAuth" /v "ProtectedPrivateKey" /f >nul 2>nul

echo [4/4] 清理旧版兼容字段...
reg delete "HKCU\Software\IslandCaller\Gacha" /v "RequireUsbAuth" /f >nul 2>nul
reg delete "HKCU\Software\IslandCaller\Gacha" /v "UsbAuthFileName" /f >nul 2>nul
reg delete "HKCU\Software\IslandCaller\Gacha" /v "UsbAuthToken" /f >nul 2>nul

echo.
echo 清理完成。
echo 建议现在完全退出 ClassIsland 后重新打开插件。
echo.
pause
endlocal
