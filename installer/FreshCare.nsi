Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"

!define APP_NAME "Fresh & Care"
!define APP_VERSION "1.0.3"
!define APP_PUBLISHER "Fresh & Care"
!define APP_EXE "Pressings.Desktop.exe"
!define APP_DIR "payload"
!define APP_ICON "FreshCare.ico"
!define APP_UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\FreshCare"

Name "${APP_NAME}"
Caption "Installation de ${APP_NAME}"
OutFile "..\release\FreshCare-Setup.exe"
InstallDir "$LOCALAPPDATA\Programs\Fresh & Care"
InstallDirRegKey HKCU "Software\FreshCare" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
ShowInstDetails show
ShowUninstDetails show
BrandingText "Fresh & Care"

VIProductVersion "1.0.3.0"
VIAddVersionKey "ProductName" "Fresh & Care"
VIAddVersionKey "ProductVersion" "1.0.3"
VIAddVersionKey "FileDescription" "Installation de Fresh & Care"
VIAddVersionKey "CompanyName" "Fresh & Care"

!define MUI_ICON "${APP_ICON}"
!define MUI_UNICON "${APP_ICON}"
!define MUI_WELCOMEFINISHPAGE_BITMAP "FreshCareWizard.bmp"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_BITMAP "FreshCareHeader.bmp"
!define MUI_WELCOMEPAGE_TITLE "Bienvenue dans Fresh & Care"
!define MUI_WELCOMEPAGE_TEXT "L’assistant va installer Fresh & Care sur votre ordinateur.$\r$\n$\r$\nL’application de gestion de pressing fonctionne sans connexion Internet avec la base locale de cet ordinateur."
!define MUI_DIRECTORYPAGE_TEXT_TOP "Choisissez le dossier d’installation de Fresh & Care."
!define MUI_FINISHPAGE_TITLE "Installation terminée"
!define MUI_FINISHPAGE_TEXT "Fresh & Care est prêt à être utilisé."
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Lancer Fresh & Care"
!define MUI_UNCONFIRMPAGE_TEXT "Fresh & Care sera retiré de cet ordinateur. Vos données locales et votre base de pressing seront conservées."

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "French"

Function .onInit
    SetRegView 64
    ReadRegStr $0 HKLM "SOFTWARE\ODBC\ODBCINST.INI\ODBC Drivers" "Microsoft Access Driver (*.mdb, *.accdb)"
    ${If} $0 != "Installed"
        MessageBox MB_ICONSTOP|MB_OK "Le pilote ODBC Microsoft Access 64 bits est requis. Installez-le, puis relancez Fresh & Care."
        Abort
    ${EndIf}

    ReadRegStr $0 HKLM "SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" "pv"
    ${If} $0 == ""
        SetRegView 32
        ReadRegStr $0 HKLM "SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" "pv"
        SetRegView 64
    ${EndIf}
    ${If} $0 == ""
        ReadRegStr $0 HKCU "Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" "pv"
    ${EndIf}
    ${If} $0 == ""
        MessageBox MB_ICONSTOP|MB_OK "Le runtime Microsoft Edge WebView2 est requis. Installez-le, puis relancez Fresh & Care."
        Abort
    ${EndIf}
FunctionEnd

Section "Fresh & Care" SEC_MAIN
    SectionIn RO
    SetShellVarContext current
    SetOutPath "$INSTDIR"
    File /r "${APP_DIR}\*.*"
    File "${APP_ICON}"
    WriteUninstaller "$INSTDIR\Uninstall.exe"

    CreateDirectory "$SMPROGRAMS\Fresh & Care"
    CreateShortcut "$SMPROGRAMS\Fresh & Care\Fresh & Care.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_ICON}" 0
    CreateShortcut "$DESKTOP\Fresh & Care.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_ICON}" 0
    CreateShortcut "$SMPROGRAMS\Fresh & Care\Désinstaller Fresh & Care.lnk" "$INSTDIR\Uninstall.exe"

    WriteRegStr HKCU "Software\FreshCare" "InstallDir" "$INSTDIR"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "DisplayName" "${APP_NAME}"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "DisplayVersion" "${APP_VERSION}"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "Publisher" "${APP_PUBLISHER}"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\${APP_ICON}"
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
    WriteRegStr HKCU "${APP_UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
    WriteRegDWORD HKCU "${APP_UNINSTALL_KEY}" "NoModify" 1
    WriteRegDWORD HKCU "${APP_UNINSTALL_KEY}" "NoRepair" 1

SectionEnd

Section "Uninstall"
    SetShellVarContext current
    Delete "$DESKTOP\Fresh & Care.lnk"
    Delete "$SMPROGRAMS\Fresh & Care\Fresh & Care.lnk"
    Delete "$SMPROGRAMS\Fresh & Care\Désinstaller Fresh & Care.lnk"
    RMDir "$SMPROGRAMS\Fresh & Care"
    DeleteRegKey HKCU "${APP_UNINSTALL_KEY}"
    DeleteRegKey HKCU "Software\FreshCare"
    RMDir /r "$INSTDIR"
SectionEnd
