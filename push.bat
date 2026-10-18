@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo ================================================
echo  BongoCatNotice  -  push to GitHub
echo ================================================
echo.

where git >nul 2>nul
if errorlevel 1 (
    echo [X] Git not found.
    echo     Install: https://git-scm.com/download/win
    goto :end
)

if not exist ".git" (
    echo [*] Initializing repository ...
    git init
    git branch -M main 2>nul
    echo [OK] Repository initialized
)

git remote get-url origin >nul 2>nul
if errorlevel 1 (
    echo.
    echo [!] No remote named "origin".
    set /p REPO_URL=    Repo URL ^(https://github.com/USER/REPO.git^): 
    if "!REPO_URL!"=="" (
        echo [X] Cancelled.
        goto :end
    )
    git remote add origin "!REPO_URL!"
    echo [OK] Remote added
)

echo.
echo [*] Changes:
git status --short
echo.

set /p COMMIT_MSG=    Commit message ^(Enter = auto^): 
if "!COMMIT_MSG!"=="" set COMMIT_MSG=chore: update

git add -A
git diff --cached --quiet
if errorlevel 1 (
    git commit -m "!COMMIT_MSG!"
    if errorlevel 1 (
        echo [X] Commit failed.
        goto :end
    )
    echo [OK] Committed
) else (
    echo [=] Nothing to commit
)

echo.
echo [*] Pushing current branch to origin ...
git push -u origin HEAD
if errorlevel 1 (
    echo [X] Push failed. Check network, login, or remote URL.
    goto :end
)
echo [OK] Pushed

echo.
set /p TAG_NAME=    Tag to release ^(e.g. v0.1.0.5, Enter = skip^): 
if not "!TAG_NAME!"=="" (
    git rev-parse "!TAG_NAME!" >nul 2>nul
    if not errorlevel 1 (
        echo [!] Tag !TAG_NAME! already exists locally. Skipped.
    ) else (
        git tag "!TAG_NAME!"
        git push origin "!TAG_NAME!"
        if errorlevel 1 (
            echo [X] Tag push failed.
        ) else (
            echo [OK] Tag !TAG_NAME! pushed.
            echo     GitHub Actions will build all platforms and create the release.
        )
    )
)

:end
echo.
pause
