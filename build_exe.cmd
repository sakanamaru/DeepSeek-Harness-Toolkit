@echo off
rem ================================================
rem  DeepSeek Harness Toolkit 重编译脚本（备用）
rem  需要 Windows 自带 .NET Framework 4.x
rem ================================================
chcp 65001 >nul
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" ( echo [失败] 未找到 .NET Framework 编译器 & pause & exit /b 1 )
"%CSC%" /nologo /optimize+ /target:exe /win32icon:icon.ico /out:"DeepSeek Harness Toolkit V2.0.0.exe" dsh_v2.cs
if errorlevel 1 ( echo [失败] 编译出错，请检查 dsh_v2.cs & pause & exit /b 1 )
echo [成功] 已生成 DeepSeek Harness Toolkit V2.0.0.exe
pause