@echo off
echo ============================================
echo  NEREON - Toolchain Setup Script
echo ============================================
echo.

REM ?? Step 0: Add cargo bin to PATH for this session
set PATH=%USERPROFILE%\.cargo\bin;%PATH%

echo [1/7] Checking Rust...
rustc --version
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Rust is not installed. Run: https://rustup.rs
    pause
    exit /b 1
)
echo.

echo [2/7] Installing Solana CLI v1.18.26...
echo This may take a few minutes...
cmd /c "curl -L https://github.com/anza-xyz/agave/releases/download/v1.18.26/solana-release-x86_64-pc-windows-msvc.tar.bz2 -o %TEMP%\solana.tar.bz2"
if exist "%USERPROFILE%\.local\share\solana\install\active_release" (
    echo Solana may already be installed. Checking...
)
REM Try the official installer instead
echo.
echo !! IMPORTANT !!
echo If Solana CLI is not installed, please run this in a NEW PowerShell:
echo.
echo   cmd /c "curl --proto =https --tlsv1.2 -sSfL https://solana-install.solana.workers.dev | cmd"
echo.
echo Then close and reopen your terminal, and re-run this script.
echo.

solana --version
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Solana CLI not found. Install it first with the command above.
    echo After installing, CLOSE this terminal and open a new one, then re-run.
    pause
    exit /b 1
)
echo.

echo [3/7] Installing Anchor 0.30.1 via avm...
avm install 0.30.1
avm use 0.30.1
anchor --version
echo.

echo [4/7] Configuring Solana for Devnet...
solana config set --url devnet
echo.

echo [5/7] Checking/creating keypair...
if exist "%USERPROFILE%\.config\solana\id.json" (
    echo Keypair already exists.
) else (
    echo Creating new keypair...
    solana-keygen new --outfile "%USERPROFILE%\.config\solana\id.json" --no-bip39-passphrase
)
echo.

echo [6/7] Requesting Devnet airdrop (2 SOL)...
solana airdrop 2
solana balance
echo.

echo [7/7] Building Anchor program...
echo This will take 3-10 minutes on first build...
cd /d "C:\NBF\NEREON GIT\anchor"
anchor build
echo.

if exist "target\deploy\nereon-keypair.json" (
    echo ============================================
    echo  BUILD SUCCESSFUL!
    echo ============================================
    echo.
    echo Your Program ID is:
    solana address -k target\deploy\nereon-keypair.json
    echo.
    echo COPY THE PROGRAM ID ABOVE and paste it into the Copilot chat.
    echo Copilot will update all your files automatically.
    echo ============================================
) else (
    echo.
    echo BUILD FAILED - check errors above.
)
echo.
pause
