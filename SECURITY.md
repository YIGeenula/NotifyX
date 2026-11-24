# Security Policy & Audit Guide
## CODEXBLAZE PAYMENT REMINDER

This document outlines the security architecture and measures enforced across the desktop application, cloud webhook, and web dashboard.

---

### 1. Zero-Credential Architecture
- **No Banking Data Collected**: The application strictly manages schedule tracking and does not collect, transmit, or store credit card numbers, CVVs, PINs, or online banking passwords.
- **No Hardcoded API Keys**: All communication is done via authorized per-user endpoints or parameter configurations.

### 2. Desktop Database Security (SQLite & EF Core)
- **Parameterized Queries**: All database interactions utilize Entity Framework Core parameterized commands to eliminate SQL Injection (SQLi) vulnerabilities.
- **Isolated User Profile Storage**: Database files (`paymentreminder.db`) are stored in `%LOCALAPPDATA%\CodexBlazePaymentReminder`, protected by Windows user access control lists (ACLs).
- **Safe Hot Backups**: Local backups utilize SQLite native `VACUUM INTO` APIs, ensuring archive consistency without locking or database corruption.

### 3. Web Dashboard Hardening
- **Cross-Site Scripting (XSS) Prevention**: All remote data returned from Google Sheets is processed through strict `escapeHtml()` sanitizers before DOM injection.
- **Content Security Policy (CSP)**: `default-src 'self' https:; script-src 'self' 'unsafe-inline' https:; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com;`
- **Referrer Policy**: Set to `strict-origin-when-cross-origin` to prevent URL leakage.
- **Rel Noopener / Noreferrer**: All external links are secured with `rel="noopener noreferrer"`.

### 4. Safe Auto-Update Validation
- In-place auto updates verify binary signatures and parse responses from raw GitHub release manifests over TLS 1.3 (`https://`).
