@echo off
setlocal EnableDelayedExpansion
echo ============================================
echo  NEREON - Windows Toolchain Setup
echo ============================================
echo.

REM ?? Add cargo bin to PATH for this session
set "PATH=%USERPROFILE%\.cargo\bin;%PATH%"

REM ?? Check for Solana
echo [1/6] Checking Solana CLI...
where solana >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo   Solana CLI found:
    solana --version
    echo.
) else (
    echo   Solana CLI NOT FOUND. Installing now...
    echo.
    echo   Downloading Solana CLI v1.18.26 for Windows...
    
    REM Create install directory
    if not exist "%USERPROFILE%\.local\share\solana\install\releases\1.18.26\bin" (
        mkdir "%USERPROFILE%\.local\share\solana\install\releases\1.18.26" 2>nul
    )
    
    REM Download the release
    curl -L "https://github.com/anza-xyz/agave/releases/download/v1.18.26/solana-release-x86_64-pc-windows-msvc.tar.bz2" -o "%TEMP%\solana-release.tar.bz2"
    if not exist "%TEMP%\solana-release.tar.bz2" (
        echo   ERROR: Download failed. Please check your internet connection.
        echo   You can also try manually: https://docs.solanalabs.com/cli/install
        pause
        exit /b 1
    )
    
    echo   Extracting...
    cd /d "%TEMP%"
    tar -xjf solana-release.tar.bz2
    
    REM Copy binaries
    if exist "%TEMP%\solana-release\bin\solana.exe" (
        echo   Moving to install directory...
        xcopy /E /Y /I "%TEMP%\solana-release\bin" "%USERPROFILE%\.local\share\solana\install\active_release\bin" >nul
        
        REM Add to PATH for this session
        set "PATH=%USERPROFILE%\.local\share\solana\install\active_release\bin;!PATH!"
        
        REM Add to user PATH permanently
        echo   Adding Solana to user PATH...
        setx PATH "%USERPROFILE%\.local\share\solana\install\active_release\bin;%PATH%" >nul 2>&1
        
        echo   Solana CLI installed successfully!
        solana --version
    ) else (
        echo.
        echo   ERROR: Extraction failed. tar.bz2 format may need 7-Zip.
        echo.
        echo   MANUAL INSTALL OPTION:
        echo   1. Go to: https://github.com/anza-xyz/agave/releases/tag/v1.18.26
        echo   2. Download: solana-release-x86_64-pc-windows-msvc.tar.bz2
        echo   3. Extract the bin folder to: %USERPROFILE%\.local\share\solana\install\active_release\bin
        echo   4. Add that bin folder to your system PATH
        echo   5. Re-run this script
        pause
        exit /b 1
    )
    echo.
)

REM ?? Check for Anchor
echo [2/6] Checking Anchor CLI...
where anchor >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo   Anchor found:
    anchor --version
) else (
    echo   Anchor NOT FOUND. Installing via avm...
    where avm >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        avm install 0.30.1
        avm use 0.30.1
        echo   Anchor installed:
        anchor --version
    ) else (
        echo   ERROR: avm not found either. Something is wrong with Rust/cargo.
        pause
        exit /b 1
    )
)
echo.

REM ?? Configure Devnet
echo [3/6] Configuring Solana for Devnet...
solana config set --url devnet
echo.

REM ?? Keypair
echo [4/6] Checking keypair...
if exist "%USERPROFILE%\.config\solana\id.json" (
    echo   Keypair already exists.
    echo   Wallet address:
    solana address
) else (
    echo   Creating new keypair...
    solana-keygen new --outfile "%USERPROFILE%\.config\solana\id.json" --no-bip39-passphrase
    echo   Wallet address:
    solana address
)
echo.

REM ?? Airdrop
echo [5/6] Requesting Devnet airdrop (2 SOL)...
solana airdrop 2
echo   Balance:
solana balance
echo.

REM ?? Build
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
    echo   COPY the Program ID above and paste it
    echo   into the Copilot chat window.
    echo ============================================
) else (
    echo   BUILD FAILED - check errors above.
)
echo.
pause
