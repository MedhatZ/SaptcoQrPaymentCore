# 🟢 Saptco QR Payment

A secure ASP.NET MVC application for handling SAPTCO QR payments through HyperPay (3DS) integration.  
This project allows users to register via mobile number, generate secure payment requests, and verify transactions through the SAPTCO backend.

---

## 🚀 Features

- ✅ Login and session handling with SAPTCO UAT backend  
- ✅ HyperPay 3DS payment integration (`/api/v1/payments/3ds/check`)  
- ✅ Post-payment verification and confirmation (`/api/v1/payments/3ds/confirm`)  
- ✅ Dynamic QR code generation using **QRCoder**  
- ✅ SQL Server integration for user registration  
- ✅ Client-side validation via `wwwroot/js/validation.js`  
- ✅ Clean MVC architecture supporting `@Url.Action` for dynamic routing  
- ✅ Works seamlessly on both localhost and IIS hosted paths (`/SaptcoQrPayment`)

---

## 🧱 Tech Stack

| Layer | Technology |
|--------|-------------|
| Backend | ASP.NET 8 MVC |
| Database | Microsoft SQL Server |
| Frontend | Razor Views + Bootstrap 5 |
| Payment Gateway | HyperPay / OPPWA |
| QR Generator | QRCoder 1.4.3 |
| Validation | Custom JavaScript (`wwwroot/js/validation.js`) |

---

## ⚙️ Configuration

### 🔑 `appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SQL_SERVER;Database=SaptcoQrPayment;User Id=YOUR_USER;Password=YOUR_PASS;TrustServerCertificate=True;"
  },
  "PaymentSettings": {
    "SuccessUrl": "https://apptest.saptco.com.sa/SaptcoQrPayment/QrPayment/Success",
    "FailUrl": "https://apptest.saptco.com.sa/SaptcoQrPayment/QrPayment/Fail"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
