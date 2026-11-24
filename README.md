# ⚡ NotifyX - Payment Reminder
### Premium Windows Desktop Application & Cloud Installment Management System

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)]()
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-blue.svg)]()
[![Framework](https://img.shields.io/badge/framework-.NET%2010%20WPF-purple.svg)]()
[![License](https://img.shields.io/badge/license-MIT-green.svg)]()

**NotifyX Payment Reminder** is an enterprise-grade desktop & cloud financial tracking application designed to track installments and deferred payments across buy now, pay later (BNPL) providers like **Mintpay**, **Koko**, **Payzy**, and custom credit cards.

🌐 **Live Cloud Web Dashboard:** [https://codexpaymentreminder.vlogifystudios.workers.dev/](https://codexpaymentreminder.vlogifystudios.workers.dev/)

---

## ✨ Highlights & Features

- **📊 Dynamic Executive Dashboard**: Real-time KPI summaries for total commitments, settled amount, remaining balance, and active installment timelines.
- **☁️ Real-time Two-Way Google Sheets & Cloud Sync**: Every purchase, modification, payment, and deletion syncs real-time to your Google Sheet and Cloud Webhook.
- **🌐 Mobile-Friendly Web Dashboard**: Responsive web viewer hosted on Cloudflare Workers/Pages for checking payments anywhere from your phone: [https://codexpaymentreminder.vlogifystudios.workers.dev/](https://codexpaymentreminder.vlogifystudios.workers.dev/)
- **🔔 Glassmorphic Desktop Toast Notifications**: Elegant bottom-right sliding notification alerts for 7-day, 3-day, 1-day, due-today, and overdue payment deadlines.
- **🔄 In-Place Software Updates (No re-downloading installers)**: Checks GitHub releases for new versions and updates your installed desktop app in-place seamlessly.
- **🛡️ Silent Background Engine & System Tray**: Starts automatically with Windows boot in the background and monitors due dates continuously.
- **📑 ClosedXML Multi-Sheet Excel Reports**: Export/import comprehensive formatted Excel spreadsheets (`Purchases`, `Installments`, `Payments`, `Settings`).
- **💾 Automated Hot Database Backups**: Zero-corruption SQLite backups using hot `.cbz` snapshot packaging (`VACUUM INTO`).

---

## 🛠️ How to Rebuild the EXE (After Code Changes)

When you make changes to the code or UI and want to compile a fresh `CodexBlaze.PaymentReminder.Wpf.exe`:

### Method 1: Using the 1-Click Build Script
Double-click **`build.bat`** (or run `.\build.ps1` in PowerShell) in the root directory.

It will automatically:
1. Compile the project in `Release` mode with .NET 10.
2. Produce a single-file executable at `publish\CodexBlaze.PaymentReminder.Wpf.exe`.
3. Automatically copy the latest binaries to `installer\` so your installer is immediately updated.

### Method 2: Using the Command Line
Run the following command from the project root:
```bash
dotnet publish src/CodexBlaze.PaymentReminder.Wpf/CodexBlaze.PaymentReminder.Wpf.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -o publish
```
Then copy the contents of `publish\` to `installer\`.

---

## 🚀 How to Install on PC

1. Download or clone this repository.
2. Open the **`installer`** folder:
   ```
   E:\PROJECT FILES\CODEXBLAZE PAYMENT REMINDER\installer
   ```
3. Right-click **`Setup.bat`** and click **Run as administrator**.
4. The setup installs the app into `C:\Program Files\CodexBlaze\PaymentReminder`, configures Desktop and Start Menu shortcuts, and registers background startup on PC boot.

---

## 🔄 How In-Place Software Updates Work (GitHub Release Flow)

When you make updates in the future, your users **do not need to download or run setup files again**:

1. **Bump Version**:
   - In [version.json](file:///e:/PROJECT%20FILES/CODEXBLAZE%20PAYMENT%20REMINDER/version.json), change `"latestVersion": "1.0.1"` and update `"releaseNotes"`.
2. **Push to GitHub**:
   - Push your code to your GitHub repository.
3. **Attach Release Binary**:
   - Create a Release on GitHub (e.g., `v1.0.1`) and upload the new `CodexBlaze.PaymentReminder.Wpf.exe`.
4. **Instant Update in PC**:
   - Inside the app, navigate to **Settings & Backup** &rarr; **Check for Updates**.
   - Click **"Update Installed Version Now"** &mdash; the application downloads the update, swaps the installed executable in-place, and restarts automatically!

---

## 🛠️ Tech Stack

- **C# / .NET 10** (WPF MVVM with `CommunityToolkit.Mvvm`)
- **SQLite & Entity Framework Core**
- **Hardcodet NotifyIcon** (System Tray)
- **ClosedXML** (Excel Engine)
- **Serilog** (Diagnostics Logging)
- **HTML5 / CSS3** (Cloud Web Dashboard)

---

## 📄 License
This project is licensed under the MIT License.
