; File: installer/PostgresCopy.nsi
;
; Per-user, no-admin NSIS installer for the PostgresCopy desktop app.
;
; This script deliberately does NOT duplicate install/uninstall logic —
; it stages the published exe plus scripts\install-desktop.ps1 and
; scripts\uninstall-desktop.ps1 into a temp folder and shells out to them,
; so the installer, the scripted per-user install path, and the uninstall
; registry entry all stay driven by the same PowerShell source of truth.
;
; Expected /D command-line define (set by scripts/build-installer.ps1):
;   /DAPP_VERSION=0.2.0
;
; Expected files alongside this script at build time (staged by
; build-installer.ps1 into the same directory as this .nsi before
; invoking makensis):
;   PostgresCopy.Desktop.exe
;   install-desktop.ps1
;   uninstall-desktop.ps1
;   icon.ico
;   LICENSE.md

!ifndef APP_VERSION
  !define APP_VERSION "0.0.0"
!endif

!define APP_NAME "PostgresCopy"
!define APP_PUBLISHER "PostgresCopy contributors"
!define APP_EXE "PostgresCopy.Desktop.exe"

Name "${APP_NAME} ${APP_VERSION}"
OutFile "PostgresCopy-Setup-${APP_VERSION}.exe"

; Per-user, no elevation. Do not use RequestExecutionLevel admin/highest here —
; the whole point of this installer is to match install-desktop.ps1's
; no-admin-rights design.
RequestExecutionLevel user

InstallDir "$LOCALAPPDATA\Programs\PostgresCopy"
InstallDirRegKey HKCU "Software\PostgresCopy" "InstallDir"

Icon "icon.ico"
UninstallIcon "icon.ico"

!include "MUI2.nsh"

!define MUI_ABORTWARNING
!define MUI_ICON "icon.ico"
!define MUI_UNICON "icon.ico"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "LICENSE.md"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

Section "Install"
  SetOutPath "$TEMP\PostgresCopy-install-stage"
  File "PostgresCopy.Desktop.exe"
  File "install-desktop.ps1"
  File "uninstall-desktop.ps1"

  DetailPrint "Running per-user install (no admin rights required)..."
  nsExec::ExecToLog 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$TEMP\PostgresCopy-install-stage\install-desktop.ps1" -InstallDir "$INSTDIR" -AppSource "$TEMP\PostgresCopy-install-stage\PostgresCopy.Desktop.exe"'
  Pop $0
  ${If} $0 != 0
    DetailPrint "install-desktop.ps1 failed with exit code $0."
    Abort "Installation failed. See the log above for details."
  ${EndIf}

  RMDir /r "$TEMP\PostgresCopy-install-stage"

  WriteRegStr HKCU "Software\PostgresCopy" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\PostgresCopy" "Version" "${APP_VERSION}"

  ; install-desktop.ps1 already writes its own
  ; HKCU\...\Uninstall\PostgresCopy entry pointing at uninstall-desktop.ps1
  ; for Windows "Installed apps". This uninstaller (Uninstall.exe, written
  ; below) is a convenience for users who launch it directly rather than
  ; through Settings; both paths converge on uninstall-desktop.ps1.
  WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

Section "Uninstall"
  DetailPrint "Running per-user uninstall..."
  IfFileExists "$INSTDIR\Uninstall-PostgresCopy.ps1" 0 SkipScriptedUninstall
  nsExec::ExecToLog 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$INSTDIR\Uninstall-PostgresCopy.ps1" -InstallDir "$INSTDIR"'
  Pop $0
  SkipScriptedUninstall:

  DeleteRegKey HKCU "Software\PostgresCopy"

  ; uninstall-desktop.ps1 removes $INSTDIR itself via a detached cleanup
  ; script (it cannot delete its own running directory synchronously);
  ; Uninstall.exe's own presence in $INSTDIR is handled the same way.
SectionEnd
