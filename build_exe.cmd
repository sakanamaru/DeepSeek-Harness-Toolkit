@echo off
rem ================================================
rem  DeepSeek Harness Toolkit 重编译脚本（备用）
rem  需要 Windows 自带 .NET Framework 4.x
rem ================================================
chcp 65001 >nul
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" ( echo [失败] 未找到 .NET Framework 编译器 & pause & exit /b 1 )
rem 源码递归收集（与 CI 的 Build exe from source 步骤同一套规则）：
rem v2.8 起源码已拆成 src\Core / src\Platform\Windows / src\Platform\Linux / src\Cli，
rem 写死目录列表会漏文件（曾漏掉 Linux 平台实现导致 CS0246）——这里统一递归，新增文件无需改脚本。
powershell -NoProfile -ExecutionPolicy Bypass -Command "$src = @('dsh_v2.cs') + @(Get-ChildItem src -Recurse -Filter *.cs | ForEach-Object FullName); & $env:CSC /nologo /optimize+ /target:exe /win32icon:icon.ico /out:'DeepSeek Harness Toolkit.exe' $src /warn:4; exit $LASTEXITCODE"
if errorlevel 1 ( echo [失败] 编译出错，请检查源码 & pause & exit /b 1 )
echo [成功] 已生成 DeepSeek Harness Toolkit.exe
pause
