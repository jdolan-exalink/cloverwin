# Quick Test Guide - Phase 2 Product Transaction System

## 🚀 Launch Application

```powershell
# From the project root
.\start.ps1

# Or directly
cd bin\Release\net8.0-windows\win-x64
.\CloverBridge.exe
```

## 📋 Test Scenario: $50 Transaction (2 × $25)

### Step 1: Verify Connection
- [ ] Look at bottom right of window
- [ ] Status should show: **Paired** (green indicator)
- [ ] If not paired, use "Pair Terminal" button first

### Step 2: Fill in Testing Tab

| Field | Value | Notes |
|-------|-------|-------|
| **Invoice Number** | `INV-2025-001` | Any unique number |
| **External ID** | `TEST-001` | Auto-generated, can change |
| **Product 1 Name** | `Widget A` | Any name |
| **Product 1 Qty** | `1` | Default value |
| **Product 1 Price** | `25.00` | Already filled |
| **Product 2 Name** | `Widget B` | Any name |
| **Product 2 Qty** | `1` | Default value |
| **Product 2 Price** | `25.00` | Already filled |
| **Total Display** | `$50.00` | Auto-calculated |

### Step 3: Click "Send Sale"

Expected sequence:
```
💳 Enviando pago de $50.00 (Factura: INV-2025-001)...
[Wait for Clover terminal response]
✅ Respuesta recibida del terminal: APROBADO
📥 Detalles:
[JSON response...]
💾 Resultado guardado: Completed
```

### Step 4: Verify Results

**In Transaction History** (left panel):
- New row appears with:
  - Time: Current timestamp
  - Type: `SALE`
  - Amount: `$50.00`
  - ID: `TEST-001`
  - Status: `✅ COMPLETADA`

**In OUTBOX Folder** (file system):
```
d:\DEVs\Cloverwin\bin\Release\net8.0-windows\win-x64\OUTBOX\
├── TEST-001_INV-2025-001_Pending_20250116_135915.json
└── TEST-001_INV-2025-001_Completed_20250116_135917.json
```

## 🔍 Inspect Transaction File

### Open OUTBOX File
```powershell
cd d:\DEVs\Cloverwin\bin\Release\net8.0-windows\win-x64\OUTBOX
Get-Content TEST-001_INV-2025-001_Completed_*.json | ConvertFrom-Json | Format-List
```

### Expected Structure
```json
{
  "transactionId": "TRX-20250116-135912",
  "externalId": "TEST-001",
  "status": "Completed",
  "detail": {
    "invoiceNumber": "INV-2025-001",
    "items": [
      {
        "productId": "PROD-001",
        "productName": "Widget A",
        "quantity": 1,
        "unitPrice": 25
      },
      {
        "productId": "PROD-002",
        "productName": "Widget B",
        "quantity": 1,
        "unitPrice": 25
      }
    ],
    "total": 50
  }
}
```

## ✅ Validation Checklist

- [ ] **Invoice Number Required**: Try sending without invoice → should show error
- [ ] **Product Prices Required**: Clear price fields → should show error
- [ ] **Quantities Required**: Clear qty fields → should show error
- [ ] **Calculation Correct**: (25 × 1) + (25 × 1) = 50.00 ✓
- [ ] **OUTBOX File Created**: Check that 2 files appear (Pending, then Completed)
- [ ] **Status Progression**: Pending → Completed (or Failed if rejected)
- [ ] **Transaction History**: New entry shows with correct amount and status
- [ ] **Form Clears**: After sending, form resets with new External ID

## 🔧 Troubleshooting

### "Not Paired" Error
→ Need to pair terminal first. Use UI pairing dialog.

### File Not Created in OUTBOX
→ Check logs: `bin\Release\net8.0-windows\win-x64\logs\`
→ Verify write permissions on OUTBOX folder

### JSON Parsing Issues
→ Ensure all product fields are filled
→ Verify prices are valid decimal numbers

### Clover Payment Declined
→ Check terminal has sufficient funds/account active
→ Verify amount in centavos is correct (multiply by 100)

## 📊 Test Results to Record

After running test, document:

1. **UI Test**: _____ (success/fail)
   - Product entry works?
   - Total calculates correctly?

2. **File Creation**: _____ (success/fail)
   - OUTBOX file exists?
   - File contains correct invoice number?
   - File contains 2 line items?

3. **Status Tracking**: _____ (success/fail)
   - Transaction shows in history?
   - Amount is $50.00?
   - Status shows correct result?

4. **End-to-End**: _____ (success/fail)
   - Complete workflow from UI to OUTBOX file works?

---

**Ready to test?** → Run `.\start.ps1` and go to Testing tab!
