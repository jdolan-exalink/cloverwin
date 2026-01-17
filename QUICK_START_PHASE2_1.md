# 🎯 Guía Rápida - Phase 2.1 - Gestión de Transacciones

**Fecha**: Enero 17, 2026  
**Versión**: v2.1  
**Estado**: ✅ Listo para usar

---

## 🚀 Inicio Rápido

### Paso 1: Lanzar Aplicación
```powershell
cd d:\DEVs\Cloverwin
.\start.ps1
```

### Paso 2: Verificar Conexión
- Debe decir "Paired" en la esquina inferior derecha
- Si no está pareado, usa el botón "Pair Terminal"

---

## 📝 Crear una Transacción (Tab "Testing")

### Paso 1: Ingresar Datos
```
Nro Factura:        FB-12345-12345678  ✓ (ya está)
ID Externo:         TEST-001            (auto-generado)

Producto 1:
  - Nombre:         Widget A
  - Cantidad:       1
  - Precio:         $25.00

Producto 2:
  - Nombre:         Widget B
  - Cantidad:       1
  - Precio:         $25.00

Total:              $50.00 ✓ (calculado automáticamente)
```

### Paso 2: Enviar Pago
1. Click botón: **"Send Sale"**
2. Espera mensaje: "Procesando pago..."
3. Aprueba en la terminal Clover (en el dispositivo)
4. Espera respuesta: "✅ Pago aprobado"

### Paso 3: Confirmar
- Se crea archivo en OUTBOX
- Aparece en Transaction History (izquierda)
- Muestra: Time, Type, $50.00, Status ✅

---

## 👁️ Ver Detalles (Tab "Gestión OUTBOX")

### Paso 1: Actualizar Lista
1. Click botón: **"🔄 Actualizar Lista"**
2. Se cargan todos los archivos de OUTBOX
3. Verás listados los archivos creados

### Paso 2: Seleccionar Archivo
1. Haz click en un archivo de la lista
2. Se cargará automáticamente en "Detalles de Transacción"
3. Verás toda la información JSON

### Alternativa: Ver Detalles Manualmente
1. Selecciona archivo de la lista
2. Click botón: **"📖 Ver Detalles"** (si no se auto-carga)
3. Se muestra JSON formateado

### Contenido del JSON (Ejemplo)
```json
{
  "transactionId": "TRX-20250117-120000",
  "externalId": "TEST-001",
  "detail": {
    "invoiceNumber": "FB-12345-12345678",
    "items": [
      {
        "productId": "PROD-001",
        "productName": "Widget A",
        "quantity": 1,
        "unitPrice": 25.00
      },
      {
        "productId": "PROD-002",
        "productName": "Widget B",
        "quantity": 1,
        "unitPrice": 25.00
      }
    ],
    "total": 50.00
  },
  "status": "Completed",
  "paymentInfo": {
    "cardLast4": "1234",
    "authCode": "ABC123",
    "totalAmount": 5000
  }
}
```

---

## ✅ Aprobar Transacción

### Proceso:
1. Selecciona archivo en la lista
2. Revisa los detalles (JSON)
3. Click botón: **"✅ Aprobar"**
4. Observa:
   - Mensaje: "Transacción aprobada y archivada"
   - Archivo desaparece de OUTBOX
   - Se crea en ARCHIVE/completed/20250117/

### Resultado:
- Estado cambia a: **Approved**
- Se añade: "Aprobado por usuario"
- Archivo archivado: `TEST-001_FB-12345-12345678_Approved_*.json`

---

## ❌ Rechazar Transacción

### Proceso:
1. Selecciona archivo en la lista
2. Revisa los detalles (JSON)
3. Click botón: **"❌ Rechazar"**
4. Observa:
   - Mensaje: "Transacción rechazada y archivada"
   - Archivo desaparece de OUTBOX
   - Se crea en ARCHIVE/completed/20250117/

### Resultado:
- Estado cambia a: **Rejected**
- Se añade: "Rechazado por usuario"
- Archivo archivado: `TEST-001_FB-12345-12345678_Rejected_*.json`

---

## 📁 Archivar Sin Cambiar Estado

### Para archivos que no necesitan aprobación:
1. Selecciona archivo
2. Click botón: **"📁 Archivar"**
3. Se mueve a ARCHIVE manteniendo estado actual

### Resultado:
- Estado NO cambia
- Se archiva con estado original
- Útil para transacciones ya completadas

---

## 🗑️ Limpiar INBOX

### Cuándo usar:
- INBOX está lleno de archivos antiguos
- Quieres que esté limpio para nuevos pagos
- Mantenimiento de carpetas

### Proceso:
1. Ve a Tab "Gestión OUTBOX"
2. Sección: **Limpieza**
3. Click botón: **"🗑️ Limpiar INBOX"**
4. Verás confirmación: "INBOX limpiado correctamente"

### Resultado:
- Todos los archivos de INBOX se eliminan
- INBOX queda vacío (0 archivos)
- Listo para recibir nuevos archivos

---

## 📊 Flujo Completo Visual

```
┌─────────────────────────────────────────────────────────┐
│ 1. TESTING TAB - Crear Transacción                     │
│    ┌──────────────────────────────────────────────┐   │
│    │ Factura: FB-12345-12345678 ✓               │   │
│    │ Producto 1: Widget A × 1 @ $25.00         │   │
│    │ Producto 2: Widget B × 1 @ $25.00         │   │
│    │ Total: $50.00                             │   │
│    │ [Click "Send Sale"]                        │   │
│    └──────────────────────────────────────────────┘   │
│                        ↓                               │
│    → Archivo Creado: TEST-001_..._Pending_*.json    │
│    → Se envía a Clover Terminal                      │
│    → Se recibe respuesta                             │
│    → Se actualiza a: TEST-001_..._Completed_*.json  │
└─────────────────────────────────────────────────────────┘
                        ↓
┌─────────────────────────────────────────────────────────┐
│ 2. GESTIÓN OUTBOX TAB - Revisar y Aprobar            │
│    ┌──────────────────────────────────────────────┐   │
│    │ [Click "Actualizar Lista"]                  │   │
│    │ [Selecciona archivo]                        │   │
│    │ [Ver detalles en JSON]                      │   │
│    │ Elige una acción:                           │   │
│    │  ✅ Aprobar  ❌ Rechazar  📁 Archivar     │   │
│    └──────────────────────────────────────────────┘   │
│                        ↓                               │
│    → Archivo movido a ARCHIVE                        │
│    → Estado actualizado (Approved/Rejected)          │
│    → Se elimina de OUTBOX                           │
└─────────────────────────────────────────────────────────┘
                        ↓
┌─────────────────────────────────────────────────────────┐
│ 3. ARCHIVE FOLDER - Historial Permanente             │
│    ├── completed/                                      │
│    │   └── 20250117/                                  │
│    │       ├── TEST-001_..._Approved_*.json         │
│    │       └── TEST-001_..._Rejected_*.json         │
│    └──────────────────────────────────────────────────┘
└─────────────────────────────────────────────────────────┘
```

---

## 🔍 Verificar Transacciones Archivadas

### En el Explorador:
```powershell
# Navega a:
d:\DEVs\Cloverwin\bin\Release\net8.0-windows\win-x64\ARCHIVE\completed\20250117\

# Verás archivos como:
TEST-001_FB-12345-12345678_Approved_20250117_120005.json
TEST-001_FB-12345-12345678_Rejected_20250117_120010.json
```

### Inspeccionar archivo:
```powershell
# PowerShell
cd d:\DEVs\Cloverwin\bin\Release\net8.0-windows\win-x64\ARCHIVE\completed\20250117\
Get-Content TEST-001_*_Approved_*.json | ConvertFrom-Json | Format-List

# Verás: status = "Approved"
```

---

## 📋 Checklist Rápido

### Para Crear y Procesar Transacción:
- [ ] Aplicación lanzada: ✅ Running
- [ ] Terminal pareado: ✅ Paired
- [ ] Tab "Testing" activo
- [ ] Factura: FB-12345-12345678 (verificar)
- [ ] Productos: 2 × $25 (llenar nombres si quieres)
- [ ] Total: $50.00 (debe calcular automáticamente)
- [ ] Click "Send Sale"
- [ ] Aprobar en terminal Clover
- [ ] Tab "Gestión OUTBOX"
- [ ] Click "Actualizar Lista"
- [ ] Seleccionar archivo
- [ ] Ver detalles
- [ ] Click "Aprobar" o "Rechazar"
- [ ] Verificar en ARCHIVE

---

## 🎨 Atajos de Teclado

| Acción | Atajo |
|--------|-------|
| Tab Testing | Ctrl+1 |
| Tab Gestión OUTBOX | Ctrl+3 |
| Copiar JSON | Ctrl+A (en TextBox) → Ctrl+C |

---

## 🆘 Problemas Comunes

### Problema: "No puedo escribir en Factura"
**Solución**: Ya está con valor por defecto `FB-12345-12345678`
- Puedes borrarlo y poner otro número
- O usar el que está

### Problema: "OUTBOX no muestra archivos"
**Solución**:
- Click "Actualizar Lista"
- Verifica que hayas clickeado "Send Sale"
- Revisa que Clover aprobara el pago

### Problema: "No veo archivos en ARCHIVE"
**Solución**:
- Debes haber hecho click en "Aprobar" o "Rechazar"
- Después click en "Actualizar Lista"
- Abre la carpeta manualmente:
  `bin\Release\net8.0-windows\win-x64\ARCHIVE\completed\YYYYMMDD\`

### Problema: "Aplicación no abre"
**Solución**:
```powershell
cd d:\DEVs\Cloverwin
dotnet build Cloverwin.sln -c Release
.\bin\Release\net8.0-windows\win-x64\CloverBridge.exe
```

---

## 📞 Estados Posibles de Transacción

```
Estado               Significado
──────────────────────────────────────────────
Pending              Creada, esperando envío
Processing           Enviada a Clover
Completed            Pago procesado exitosamente
Approved             Aprobada por usuario ✅
Rejected             Rechazada por usuario ❌
Failed               Error en pago
Cancelled            Cancelada
Reversed             Revertida
```

---

## 💡 Tips Útiles

1. **Factura**: Puedes cambiar `FB-12345-12345678` a cualquier valor
2. **ID Externo**: Se auto-genera pero puedes cambiar
3. **Productos**: Son solo ejemplos, pon los nombres que quieras
4. **Total**: Se calcula automáticamente, no necesitas escribir
5. **JSON**: Es JSON válido, puedes copiar y usar en otros sistemas
6. **ARCHIVE**: Los archivos se organizan por fecha (YYYYMMDD)

---

## ✨ Resumen

- ✅ **Crear**: Testing Tab con valores por defecto
- ✅ **Enviar**: Click "Send Sale" → Aprueba en terminal
- ✅ **Revisar**: Gestión OUTBOX Tab → Ver detalles
- ✅ **Aprobar/Rechazar**: Buttons - Aprobación automática
- ✅ **Archivar**: Automático al aprobar/rechazar
- ✅ **Limpiar**: INBOX limpio cuando necesites

---

**¡Ahora estás listo para usar el sistema completo!** 🎉

Ejecuta: `.\start.ps1` y pruébalo!

---

*Guía Rápida - Phase 2.1*  
*CloverBridge v2.1 - Transaction Management*
