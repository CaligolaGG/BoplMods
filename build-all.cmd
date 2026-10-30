@echo off
rem Builds every mod in mods\ (Release) and packs its Thunderstore zip into dist\.
rem Doesn't deploy to the mod manager profile;
rem build a single mod with "dotnet build mods\<Mod>\<Mod>.csproj -c Release" to deploy it.
rem Usage: build-all.cmd            (every mod)
rem        build-all.cmd CaliSkills (only the mods named)

setlocal EnableDelayedExpansion
cd /d "%~dp0"

set "MODS=%*"
if "%MODS%"=="" (
    for /d %%D in (mods\*) do if exist "mods\%%~nxD\%%~nxD.csproj" set "MODS=!MODS! %%~nxD"
)

set FAILED=
for %%M in (%MODS%) do (
    echo.
    echo ===== %%M =====
    dotnet build "mods\%%M\%%M.csproj" -c Release -t:PackThunderstore -p:DeployToProfile=false -nologo -v:minimal
    if errorlevel 1 set "FAILED=!FAILED! %%M"
)

echo.
if defined FAILED (
    echo FAILED:%FAILED%
    exit /b 1
)
echo All mods built. Zips in %~dp0dist:
dir /b dist\*.zip
exit /b 0
