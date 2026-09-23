# BloodLink Deployment Guide

**Document Version:** 1.0.0  
**Date:** 16 August 2026  
**Author:** Jennifer Banibensu, DevOps & QA/Test Engineer (Student ID: 22013023)  
**Project:** BloodLink — Hospital & Blood Bank Inventory Coordination System  
**Handoff Recipient:** Emmanuel Eyram Korku Agbetor, Project Manager / Team Lead (Student ID: 22206812)  

---

## 1. Prerequisites & Environment Requirements

### 1.1 Software & SDK Requirements
* **.NET Runtime & SDK:** x64 .NET 8 SDK and x64 ASP.NET Core 8 runtime. `global.json` requests SDK `8.0.400` with `rollForward: latestFeature`, which permits compatible .NET 8 servicing and feature-band updates but never selects .NET 9 or .NET 10. A compatible .NET 8 SDK must be installed, and `Microsoft.AspNetCore.App 8.0.x` must appear in `dotnet --list-runtimes`.
* **Database Engine:** Microsoft SQL Server 2019+, Azure SQL Database, or SQL Server LocalDB (`(localdb)\mssqllocaldb`) for local testing.
* **Web Server Host:** Kestrel reverse-proxied behind IIS, Nginx, or Azure App Service.
* **Network & Protocols:** HTTPS with TLS 1.2 or later on production port 443; the local development profile uses HTTPS port 7080.

---

## 2. Configuration Management & Secrets

### 2.1 Configuration Hierarchy
Application configuration uses ASP.NET Core hierarchical configuration providers:
1. `appsettings.json` (Base configuration defaults)
2. `appsettings.{Environment}.json` (Environment overrides, e.g. `Development`, `Staging`, `Production`)
3. Environment Variables (e.g., `ConnectionStrings__DefaultConnection`)
4. Secret Managers / Azure Key Vault (Production secrets)

> [!IMPORTANT]
> **No plaintext connection strings or credentials must ever be committed to git.** Refer to the repository template in [`appsettings.Example.json`](../../appsettings.Example.json).

### 2.2 Standard Configuration Template
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=BloodLink_Development;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  },
  "AllowedHosts": "*"
}
```

---

## 3. Database Migration & Provisioning Steps

Database schema management is strictly owned by **Salimah Salifu (Database Developer 1)** via EF Core code-first migrations located in `BloodLink.Infrastructure/Migrations/`.

### 3.1 Applying Migrations to Target Database
To apply all ordered migrations from the command line:

```bash
# Restore the repository-local dotnet-ef tool
dotnet tool restore

# Apply migrations targeting the target database
dotnet ef database update --project src/BloodLink.Infrastructure/BloodLink.Infrastructure.csproj --startup-project src/BloodLink.Web/BloodLink.Web.csproj --connection "Server=YOUR_SERVER;Database=BloodLink_Production;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=False;Encrypt=True;"
```

### 3.2 Generating Idempotent SQL Deployment Script
For DBAs requiring review before database execution:
```bash
dotnet ef migrations script --idempotent --output ./migrations_bundle.sql --project src/BloodLink.Infrastructure/BloodLink.Infrastructure.csproj --startup-project src/BloodLink.Web/BloodLink.Web.csproj
```

---

## 4. Build & Publishing Pipeline

### 4.1 Production Release Build
Run a self-contained or framework-dependent release build:
```bash
# Clean and restore dependencies
dotnet restore

# Build release binaries
dotnet build --configuration Release --no-restore

# Publish compiled web application artifacts
dotnet publish src/BloodLink.Web/BloodLink.Web.csproj --configuration Release --no-build --output ./dist/BloodLink.Web
```

---

## 5. Local & Production Run Instructions

### 5.1 Running Locally in Development
```bash
dotnet run --project src/BloodLink.Web/BloodLink.Web.csproj
```
* HTTP endpoint: `http://localhost:5080`
* HTTPS endpoint: `https://localhost:7080`

### 5.2 Running in Staging / Production (Kestrel / Docker)
```bash
# Set environment
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__DefaultConnection="Server=db.bloodlink.internal;Database=BloodLink_Prod;User Id=bloodlink_app;Password=REDACTED;Encrypt=True;"

# Launch application
dotnet ./dist/BloodLink.Web/BloodLink.Web.dll
```

---

## 6. Post-Deployment Verification Checklist
1. ☐ **Health Check:** Browse to root URL `/` and confirm HTTP 200 OK.
2. ☐ **Database Connection:** Confirm database connectivity and initial seed roles/facilities exist.
3. ☐ **Authentication Flow:** Test sign-in with a designated test administrator.
4. ☐ **Role Boundaries:** Verify `/system/facilities` is inaccessible to non-SystemAdmins.
5. ☐ **SSL/TLS Certificate:** Verify HTTPS redirection and valid certificate chains.
