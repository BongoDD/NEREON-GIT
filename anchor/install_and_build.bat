@echo off
setlocal EnableDelayedExpansion
echo ============================================
echo  NEREON - Complete Setup (Solana + Anchor)
echo ============================================
echo.

REM ?? Add cargo bin to PATH for this session
set "PATH=%USERPROFILE%\.cargo\bin;%PATH%"

REM ?? Step 1: Install Solana from the downloaded installer
echo [1/6] Installing Solana CLI...
echo   Looking for agave-install-init in Downloads...

if exist "%USERPROFILE%\Downloads\agave-install-init-x86_64-pc-windows-msvc.exe" (
    echo   Found! Running installer for v1.18.26...
    "%USERPROFILE%\Downloads\agave-install-init-x86_64-pc-windows-msvc.exe" v1.18.26
) else (
    echo   ERROR: Cannot find agave-install-init-x86_64-pc-windows-msvc.exe
    echo   in your Downloads folder. Make sure the file is there.
    pause
    exit /b 1
)

REM ?? Add Solana to PATH for this session
set "PATH=%USERPROFILE%\.local\share\solana\install\active_release\bin;!PATH!"
echo.

echo   Verifying Solana...
solana --version
if %ERRORLEVEL% NEQ 0 (
    echo   ERROR: Solana still not found. Close this window, open a NEW terminal, and re-run.
    pause
    exit /b 1
)
echo.

REM ?? Step 2: Install Anchor via avm
echo [2/6] Installing Anchor 0.30.1 via avm...
avm install 0.30.1
avm use 0.30.1
echo   Verifying Anchor...
anchor --version
echo.

REM ?? Step 3: Configure Devnet
echo [3/6] Configuring Solana for Devnet...
solana config set --url devnet
echo.

REM ?? Step 4: Keypair
echo [4/6] Checking keypair...
if exist "%USERPROFILE%\.config\solana\id.json" (
    echo   Keypair already exists.
) else (
    echo   Creating new keypair...
    solana-keygen new --outfile "%USERPROFILE%\.config\solana\id.json" --no-bip39-passphrase
)
echo   Your wallet address:
solana address
echo.

REM ?? Step 5: Airdrop
echo [5/6] Requesting Devnet airdrop (2 SOL)...
solana airdrop 2
echo   Balance:
solana balance
echo.

REM ?? Step 6: Build
echo [6/6] Building Anchor program...
echo   This will take 3-10 minutes on first build...
echo.
cd /d "C:\NBF\NEREON GIT\anchor"
anchor build

echo.
if exist "target\deploy\nereon-keypair.json" (
    echo ============================================
    echo   BUILD SUCCESSFUL!
    echo ============================================
    echo.
    echo   Your Program ID:
    echo.
    solana address -k target\deploy\nereon-keypair.json
    echo.
    echo   *** COPY the Program ID above ***
    echo   *** Paste it into the Copilot chat ***
    echo   Copilot will update all your files.
    echo ============================================
) else (
    echo   BUILD FAILED - check errors above.
)
echo.
pause
