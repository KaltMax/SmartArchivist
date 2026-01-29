@echo off
REM Template batch file for running SmartArchivist.Batch via Task Scheduler
REM Copy this file and adjust the paths for your environment

cd /d "%~dp0bin\Release\net8.0"
SmartArchivist.Batch.exe >> backup-log.txt 2>&1