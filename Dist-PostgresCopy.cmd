REM File: Dist-PostgresCopy.cmd

@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\dist.ps1" %*
