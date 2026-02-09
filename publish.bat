@echo off
echo ============================================
echo  Building Drawing Trainer for Windows x64
echo ============================================
echo.

dotnet publish "DrawingTrainer/DrawingTrainer.csproj" -p:PublishProfile=win-x64

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo BUILD FAILED
    exit /b %ERRORLEVEL%
)

echo.
echo ============================================
echo  Build complete!
echo  Output: DrawingTrainer\bin\publish\win-x64\
echo ============================================
echo.

dir /b "DrawingTrainer\bin\publish\win-x64\DrawingTrainer.exe"
for %%A in ("DrawingTrainer\bin\publish\win-x64\DrawingTrainer.exe") do echo Size: %%~zA bytes
