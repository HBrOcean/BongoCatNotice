@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

set "DEFAULT_URL=https://github.com/HBrOcean/BongoCatNotice.git"
set "DOBUILD=0"
set "NOTAG=0"
set "CUSTOM_MSG="

rem ================= args =================
:args
if "%~1"=="" goto args_done
if /i "%~1"=="build" (
    set "DOBUILD=1"
    shift
    goto args
)
if /i "%~1"=="notag" (
    set "NOTAG=1"
    shift
    goto args
)
set "CUSTOM_MSG=%~1"
shift
goto args
:args_done

echo.
echo ==================================================
echo    BongoCatNotice  --^>  GitHub
echo ==================================================

rem ================= version =================
set "VER=unknown"
for /f "tokens=3 delims=<>" %%a in ('findstr /c:"<Version>" BongoCatNotice.csproj 2^>nul') do set "VER=%%a"
echo   Version : !VER!
echo.

rem ================= git =================
where git >nul 2>nul
if errorlevel 1 (
    echo   [X] Git not found.
    echo       Install: https://git-scm.com/download/win
    goto end
)

set "UMAIL="
for /f "delims=" %%u in ('git config --get user.email 2^>nul') do set "UMAIL=%%u"
if "!UMAIL!"=="" (
    git config user.name  "HBrOcean"
    git config user.email "oceaniat@users.noreply.github.com"
    echo   [setup] git identity written to .git/config
)

if not exist ".git" (
    git init -q
    git symbolic-ref HEAD refs/heads/main
    echo   [setup] repository initialized on branch main
)

if not exist ".gitignore" (
    >> ".gitignore" echo [Bb]in/
    >> ".gitignore" echo [Oo]bj/
    >> ".gitignore" echo .vs/
    >> ".gitignore" echo .idea/
    >> ".gitignore" echo *.user
    echo   [setup] .gitignore created
)

git remote get-url origin >nul 2>nul
if errorlevel 1 (
    git remote add origin "!DEFAULT_URL!"
    echo   [setup] remote origin added
)
for /f "delims=" %%r in ('git remote get-url origin') do set "RURL=%%r"
echo   Remote  : !RURL!

rem ================= attach history =================
git rev-parse --verify --quiet HEAD >nul 2>nul
if errorlevel 1 (
    git symbolic-ref HEAD refs/heads/main >nul 2>nul
    echo   [sync]  no local commit yet - probing remote ...
    git fetch origin main --quiet >nul 2>nul
    if errorlevel 1 (
        echo   [sync]  remote has no "main" branch - first commit it is
    ) else (
        git update-ref refs/heads/main origin/main
        git reset --mixed HEAD 1>nul
        echo   [sync]  local files attached on top of origin/main
    )
)

rem ================= optional build =================
if "!DOBUILD!"=="1" (
    echo.
    echo   [build] dotnet build -c Release ...
    dotnet build -c Release
    if errorlevel 1 (
        echo.
        echo   [X] Build FAILED.
        set "AGAIN="
        set /p AGAIN=        Push anyway? ^(y/N^): 
        if /i not "!AGAIN!"=="y" (
            echo   [X] Aborted. Nothing was pushed.
            goto end
        )
    ) else (
        echo   [build] OK
    )
) else (
    echo   [build] skipped  ^(run: push.bat build to enable^)
)

rem ================= review =================
echo.
echo --------------------------------------------------
echo   Changes
echo --------------------------------------------------
git status --short
echo.

set "NCHG=0"
for /f %%c in ('git status --porcelain ^| find /c /v ""') do set "NCHG=%%c"
echo   !NCHG! file^(s^) changed
echo.

set "GOON="
set /p GOON=  Continue and push? ^(Y/n^): 
if /i "!GOON!"=="n" (
    echo   [X] Cancelled.
    goto end
)

rem ================= commit =================
if "!CUSTOM_MSG!"=="" (
    set "MSG="
    set /p MSG=  Commit message ^(Enter = feat: v!VER!^): 
    if "!MSG!"=="" set "MSG=feat: v!VER!"
) else (
    set "MSG=!CUSTOM_MSG!"
)

git add -A
git diff --cached --quiet
if errorlevel 1 (
    git commit -q -m "!MSG!"
    if errorlevel 1 (
        echo   [X] Commit failed.
        goto end
    )
    echo   [git]   committed: !MSG!
) else (
    echo   [git]   nothing to commit
)

rem ================= push =================
echo.
echo   [push]  origin main ...
git push -u origin main
if errorlevel 1 (
    echo.
    echo   [X] Push rejected.
    echo       Run this, then try again:
    echo           git pull --rebase origin main
    goto end
)
echo   [push]  OK

rem ================= tag =================
if "!NOTAG!"=="1" goto skip_tag

echo.
set "TAG="
set /p TAG=  Release tag ^(Enter = v!VER!, type "no" to skip^): 
if /i "!TAG!"=="no" goto skip_tag
if "!TAG!"=="" set "TAG=v!VER!"

git rev-parse "!TAG!" >nul 2>nul
if not errorlevel 1 (
    echo   [tag]   !TAG! exists locally - skipped
    goto skip_tag
)

git tag -a "!TAG!" -m "release !TAG!"
git push origin "!TAG!"
if errorlevel 1 (
    echo   [X] Tag push failed.
    goto skip_tag
)
echo   [tag]   !TAG! pushed

:skip_tag
set "BASE=!RURL:.git=!"
echo.
echo ==================================================
echo   Done
echo   Actions : !BASE!/actions
echo   Release : !BASE!/releases
echo ==================================================

:end
echo.
pause
