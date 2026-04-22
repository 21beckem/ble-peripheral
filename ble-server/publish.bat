@ECHO OFF
pushd "%~dp0"

dotnet publish -r win-x64 -c Release --self-contained true -p:PublishSingleFile=true -o ./publish

:: move the .exe to the parent directory and delete the publish folder
move publish\*.exe ..\ >nul
rd /s /q publish

popd