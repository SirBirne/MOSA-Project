if exist %SYSTEMROOT%\Microsoft.NET\Framework\v4.0.30319 set MSBUILDPATH=%SYSTEMROOT%\Microsoft.NET\Framework\v4.0.30319
if exist "%ProgramFiles%\MSBuild\14.0\Bin" set MSBUILDPATH="%ProgramFiles%\MSBuild\14.0\Bin"
if exist "%ProgramFiles(x86)%\MSBuild\14.0\Bin" set MSBUILDPATH="%ProgramFiles(x86)%\MSBuild\14.0\Bin"

set MSBUILD=%MSBUILDPATH%\msbuild.exe

set GIT="%ProgramFiles%\Git\bin\git.exe" 

rmdir /q /s source
mkdir source

%GIT% clone --branch v1.6.0-mosa --depth 1 https://github.com/mosa/dnlib.git source/dnlib

%MSBUILD% /nologo /m /p:BuildInParallel=true /p:Configuration=Release /p:Platform="Any CPU" source/dnlib\dnlib.sln

copy dnlib\Release\bin\dnlib.dll ..

pause

