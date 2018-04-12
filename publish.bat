@echo off
rem // check params
if "%~1"=="" (
    echo Please provide a host to publish to.
    exit
)
if "%~2"=="" (
    echo Please provide a port.
    exit
)
if "%~3"=="" (
    echo Please provide a username for authentification.
    exit
)
if "%~4"=="" (
    echo Please provide a password for authentification.
    exit
)
set target=ModeratelyUsefulBot
if not "%~5"=="" set target=%~5
rem // store initial working dir
set cwd=%cd%
rem // publish project
cd %target%
dotnet publish -r linux-arm -c "Release"
rem // open output dir
cd bin\Release\netcoreapp2.0\linux-arm\
rem // add start script
copy %cwd%\start.sh publish\start.sh
rem // prepare commands for psftp
mkdir temp
.>temp\psftp.bat 2>nul
rem // remove old stuff
echo del %target%/data/* >> temp\psftp.bat
echo del %target%/* >> temp\psftp.bat
rem // copy first layer of files
echo mkdir %target% >> temp\psftp.bat
echo cd %target% >> temp\psftp.bat
echo mput publish/* >> temp\psftp.bat
rem // copy second layer of files
echo mkdir data >> temp\psftp.bat
echo cd data >> temp\psftp.bat
echo mput publish/data/* >> temp\psftp.bat
rem // change permissions - make target runnable
echo cd .. >> temp\psftp.bat
echo chmod 755 ./%target% >> temp\psftp.bat
echo chmod 755 ./start.sh >> temp\psftp.bat
rem // exit psftp
echo quit >> temp\psftp.bat
rem // run psftp with previously stored commands
psftp "%~1" -P "%~2" -be -b temp\psftp.bat -l "%~3" -pw "%~4"
rem // clean up
del temp\* /q
rmdir temp
