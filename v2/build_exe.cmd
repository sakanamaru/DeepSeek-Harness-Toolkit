@echo off
rem ================================================
rem  DeepSeek Harness Toolkit 重编译脚本（备用）
rem  需要 Windows 自带 .NET Framework 4.x
rem  两种布局都能跑：解压后的发布包根目录（平铺），或仓库里的 v2\ 目录
rem ================================================
chcp 65001 >nul
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" ( echo [失败] 未找到 .NET Framework 编译器 & pause & exit /b 1 )
rem 在脚本所在目录工作：发布包里它就是包根，仓库里它是 v2\（源码都在旁边）
pushd "%~dp0"
rem 图标：发布包里与脚本同级；仓库里 icon.ico 留在仓库根（..\icon.ico）——两处都找一下
set "ICO=icon.ico"
if not exist "%ICO%" if exist "..\icon.ico" set "ICO=..\icon.ico"
rem 源码递归收集（与 CI 的 Build exe from source 步骤同一套规则）：
rem v2.8 起源码已拆成 src\Core / src\Platform\Windows / src\Platform\Linux / src\Cli，
rem 写死目录列表会漏文件（曾漏掉 Linux 平台实现导致 CS0246）——这里统一递归，新增文件无需改脚本。
rem 路径一律取绝对：csc 对相对源码路径不可靠（曾把 v2/dsh_v2.cs 解析成仓库根的 dsh_v2.cs → CS1504）。
powershell -NoProfile -ExecutionPolicy Bypass -Command "$src = @((Join-Path $PWD 'dsh_v2.cs')) + @(Get-ChildItem (Join-Path $PWD 'src') -Recurse -Filter *.cs | ForEach-Object FullName); & $env:CSC /nologo /optimize+ /target:exe /win32icon:'%ICO%' /out:'DeepSeek Harness Toolkit.exe' $src /warn:4; exit $LASTEXITCODE"
if errorlevel 1 ( echo [失败] 编译出错，请检查源码 & popd & pause & exit /b 1 )
popd
echo [成功] 已生成 DeepSeek Harness Toolkit.exe
pause
