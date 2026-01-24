# CloverBridge - Puente de Integración Clover

Sistema de integración entre terminales de pago Clover y sistemas ERP/Facturación mediante archivos JSON.

## 🎯 Características

- ✅ Integración por archivos (INBOX/OUTBOX) - Sin API REST
- ✅ Procesamiento automático de transacciones
- ✅ Estados claros: Exitosa, Cancelada, Sin Fondos
- ✅ Limpieza automática de INBOX
- ✅ Registro completo en OUTBOX
- ✅ Interfaz de sistema tray
- ✅ Logs detallados

## 📋 Requisitos

- Windows 10/11
- .NET 8.0 Runtime
- Terminal Clover en la misma red
- Network Pay Display habilitado en Clover

## 🚀 Instalación Rápida

1. **Descomprimir** el archivo ZIP en `C:\CloverBridge`
2. **Ejecutar** `CloverBridge.exe`
3. **Configurar** IP del terminal Clover en la UI
4. **Realizar pairing** ingresando el código en el terminal

## 📁 Estructura de Carpetas

```
C:\CloverBridge\
├── CloverBridge.exe          # Aplicación principal
├── config.json               # Configuración
├── INBOX\                    # Transacciones entrantes (desde ERP)
├── OUTBOX\                   # Resultados de transacciones (hacia ERP)
├── ARCHIVE\                  # Historial de transacciones
└── logs\                     # Logs del sistema
```

## 🔄 Flujo de Trabajo

### 1. Sistema de Facturación → INBOX

El sistema de facturación crea un archivo JSON en la carpeta `INBOX`:

```json
{
  "invoiceNumber": "FAC-2026-001234",
  "amount": 150.50,
  "externalId": "ERP-20260117-001",
  "customerName": "Juan Pérez",
  "notes": "Factura de venta",
  "tax": 15.05
}
```

### 2. CloverBridge Procesa

CloverBridge:
1. ✅ Detecta el archivo en INBOX
2. ✅ Marca como **Pendiente**
3. ✅ Envía al terminal Clover
4. ✅ Espera respuesta del cliente
5. ✅ Actualiza estado según resultado
6. ✅ Guarda resultado en OUTBOX
7. ✅ **Elimina archivo de INBOX**

### 3. OUTBOX → Sistema de Facturación

CloverBridge guarda el resultado en `OUTBOX`:

#### Pago Exitoso
```json
{
  "transactionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "externalId": "ERP-20260117-001",
  "timestamp": "2026-01-17T14:30:45.123Z",
  "status": "Successful",
  "type": "SALE",
  "invoiceNumber": "FAC-2026-001234",
  "amount": 150.50,
  "tax": 15.05,
  "customerName": "Juan Pérez",
  "notes": "Factura de venta",
  "paymentInfo": {
    "cloverPaymentId": "CLOVER-PAY-123456",
    "cloverOrderId": "CLOVER-ORD-789012",
    "cardLast4": "4242",
    "cardBrand": "VISA",
    "authCode": "AUTH123",
    "totalAmount": 150.50,
    "tip": 0.00,
    "processingStartTime": "2026-01-17T14:30:45.123Z"
  },
  "errorMessage": null,
  "errorCode": null
}
```

#### Pago Cancelado
```json
{
  "transactionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "externalId": "ERP-20260117-001",
  "timestamp": "2026-01-17T14:32:10.456Z",
  "status": "Cancelled",
  "type": "SALE",
  "invoiceNumber": "FAC-2026-001234",
  "amount": 150.50,
  "tax": 15.05,
  "customerName": "Juan Pérez",
  "notes": "Factura de venta",
  "paymentInfo": null,
  "errorMessage": "Transacción cancelada por el usuario o timeout",
  "errorCode": "CANCELLED"
}
```

#### Sin Fondos / Tarjeta Rechazada
```json
{
  "transactionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "externalId": "ERP-20260117-001",
  "timestamp": "2026-01-17T14:33:22.789Z",
  "status": "InsufficientFunds",
  "type": "SALE",
  "invoiceNumber": "FAC-2026-001234",
  "amount": 150.50,
  "tax": 15.05,
  "customerName": "Juan Pérez",
  "notes": "Factura de venta",
  "paymentInfo": null,
  "errorMessage": "Fondos insuficientes o tarjeta rechazada",
  "errorCode": "INSUFFICIENT_FUNDS"
}
```

## 📊 Estados de Transacción

| Estado | Descripción | Acción ERP |
|--------|-------------|------------|
| `Pending` | Enviada al terminal, esperando cliente | Mostrar "Procesando..." |
| `Successful` | Pago completado exitosamente | **Marcar factura como PAGADA** |
| `Cancelled` | Cliente canceló o timeout | Permitir reintentar |
| `InsufficientFunds` | Tarjeta rechazada/sin fondos | Solicitar otro método de pago |
| `Failed` | Error de sistema | Revisar logs, reintentar |

## 💻 Integración con Sistema ERP

### Ejemplo en C# (.NET)

```csharp
// 1. Crear archivo de solicitud en INBOX
public async Task<bool> EnviarPago(string numeroFactura, decimal monto)
{
    var solicitud = new
    {
        invoiceNumber = numeroFactura,
        amount = monto,
        externalId = $"ERP-{DateTime.Now:yyyyMMdd}-{numeroFactura}",
        customerName = "Cliente XYZ",
        notes = "Pago de factura",
        tax = monto * 0.10m // 10% de IVA
    };

    var json = JsonSerializer.Serialize(solicitud, new JsonSerializerOptions 
    { 
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
    });

    var filename = $"{numeroFactura}_{DateTime.Now:yyyyMMddHHmmss}.json";
    var inboxPath = @"C:\CloverBridge\INBOX";
    
    await File.WriteAllTextAsync(Path.Combine(inboxPath, filename), json);
    
    return true;
}

// 2. Monitorear OUTBOX para resultados
public void MonitorearResultados()
{
    var watcher = new FileSystemWatcher(@"C:\CloverBridge\OUTBOX")
    {
        Filter = "*.json",
        EnableRaisingEvents = true
    };

    watcher.Created += async (sender, e) =>
    {
        await Task.Delay(500); // Esperar a que termine de escribirse
        
        var json = await File.ReadAllTextAsync(e.FullPath);
        var resultado = JsonSerializer.Deserialize<TransactionResult>(json);

        switch (resultado.Status)
        {
            case "Successful":
                // Marcar factura como pagada
                ActualizarFactura(resultado.InvoiceNumber, "PAGADA", resultado.PaymentInfo);
                break;
                
            case "Cancelled":
                // Usuario canceló
                MostrarMensaje($"Pago cancelado para {resultado.InvoiceNumber}");
                break;
                
            case "InsufficientFunds":
                // Sin fondos
                MostrarMensaje($"Tarjeta rechazada: {resultado.InvoiceNumber}");
                break;
                
            case "Failed":
                // Error
                MostrarError($"Error procesando: {resultado.ErrorMessage}");
                break;
        }
        
        // Opcional: Mover o eliminar archivo leído
        File.Delete(e.FullPath);
    };
}
```

### Ejemplo en Python

```python
import json
import time
from pathlib import Path
from watchdog.observers import Observer
from watchdog.events import FileSystemEventHandler

# 1. Enviar pago
def enviar_pago(numero_factura: str, monto: float):
    solicitud = {
        "invoiceNumber": numero_factura,
        "amount": monto,
        "externalId": f"ERP-{int(time.time())}-{numero_factura}",
        "customerName": "Cliente XYZ",
        "notes": "Pago de factura",
        "tax": round(monto * 0.10, 2)
    }
    
    inbox_path = Path("C:/CloverBridge/INBOX")
    filename = f"{numero_factura}_{int(time.time())}.json"
    
    with open(inbox_path / filename, 'w', encoding='utf-8') as f:
        json.dump(solicitud, f, indent=2)
    
    print(f"✅ Pago enviado: {filename}")

# 2. Monitorear resultados
class OutboxHandler(FileSystemEventHandler):
    def on_created(self, event):
        if event.is_directory or not event.src_path.endswith('.json'):
            return
        
        time.sleep(0.5)  # Esperar a que termine de escribirse
        
        with open(event.src_path, 'r', encoding='utf-8') as f:
            resultado = json.load(f)
        
        status = resultado.get('status')
        invoice = resultado.get('invoiceNumber')
        
        if status == 'Successful':
            print(f"✅ PAGADA: {invoice}")
            actualizar_factura_pagada(invoice, resultado['paymentInfo'])
        elif status == 'Cancelled':
            print(f"❌ CANCELADA: {invoice}")
        elif status == 'InsufficientFunds':
            print(f"💳 SIN FONDOS: {invoice}")
        elif status == 'Failed':
            print(f"⚠️ ERROR: {invoice} - {resultado.get('errorMessage')}")
        
        # Eliminar archivo leído
        Path(event.src_path).unlink()

def monitorear_outbox():
    observer = Observer()
    observer.schedule(OutboxHandler(), "C:/CloverBridge/OUTBOX", recursive=False)
    observer.start()
    
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        observer.stop()
    
    observer.join()
```

## ⚙️ Configuración

El archivo `config.json` se crea automáticamente:

```json
{
  "clover": {
    "host": "10.1.1.53",
    "port": 12345,
    "secure": false,
    "authToken": "AUTH_TOKEN_AQUI",
    "remoteAppId": "clover-bridge",
    "posName": "ERP Bridge",
    "serialNumber": "CB-001",
    "reconnectDelayMs": 5000,
    "maxReconnectAttempts": 10
  },
  "api": {
    "port": 3777,
    "host": "127.0.0.1"
  },
  "folders": {
    "inbox": "C:\\CloverBridge\\INBOX",
    "outbox": "C:\\CloverBridge\\OUTBOX",
    "archive": "C:\\CloverBridge\\ARCHIVE"
  },
  "transaction": {
    "timeoutMs": 120000,
    "concurrency": 1
  }
}
```

## 🔧 Configuración del Terminal Clover

1. Abrir **Settings** en el terminal Clover
2. Ir a **Setup** → **Developer Options**
3. Habilitar **Network Pay Display**
4. Anotar la **IP** mostrada
5. Configurar esa IP en CloverBridge

## 🔐 Proceso de Pairing

1. **Primera vez:** CloverBridge solicita pairing automáticamente
2. Aparece un código de 6 dígitos en CloverBridge
3. Ingresar ese código en el terminal Clover
4. ¡Listo! La integración queda permanente

## 📝 Campos del Archivo INBOX

### Campos Obligatorios
- `invoiceNumber` (string): Número de factura único
- `amount` (decimal): Monto total en pesos/dólares

### Campos Opcionales
- `externalId` (string): ID de tu sistema ERP (default: invoiceNumber)
- `customerName` (string): Nombre del cliente
- `notes` (string): Notas adicionales
- `tax` (decimal): Monto de impuestos

## 📤 Campos del Archivo OUTBOX

### Siempre Presentes
- `transactionId`: ID único generado por CloverBridge
- `externalId`: Tu ID del sistema ERP
- `timestamp`: Fecha/hora de procesamiento (UTC)
- `status`: Estado final (Successful/Cancelled/InsufficientFunds/Failed)
- `type`: Tipo de transacción (SALE/REFUND/VOID)
- `invoiceNumber`: Número de factura
- `amount`: Monto de la transacción

### Si el Pago fue Exitoso (`status: "Successful"`)
- `paymentInfo`: Objeto con detalles del pago
  - `cloverPaymentId`: ID del pago en Clover
  - `cloverOrderId`: ID de la orden en Clover
  - `cardLast4`: Últimos 4 dígitos de la tarjeta
  - `cardBrand`: Marca de la tarjeta (VISA, MASTERCARD, etc.)
  - `authCode`: Código de autorización
  - `totalAmount`: Monto total cobrado
  - `tip`: Propina (si aplica)

### Si el Pago Falló
- `errorMessage`: Descripción del error
- `errorCode`: Código del error

## 🚨 Solución de Problemas

### El sistema no detecta archivos en INBOX
- ✅ Verificar que los archivos sean `.json`
- ✅ Verificar que el JSON sea válido
- ✅ Revisar logs en `logs\cloverbridge.log`

### Terminal no responde
- ✅ Verificar que esté en la misma red
- ✅ Verificar IP configurada
- ✅ Verificar que Network Pay Display esté habilitado
- ✅ Hacer ping al terminal: `ping 10.1.1.53`

### Error de pairing
- ✅ Eliminar `authToken` del config.json
- ✅ Reiniciar CloverBridge
- ✅ Ingresar código nuevo en el terminal

## 📊 Logs

Los logs se guardan en `logs\cloverbridge.log`:

```
[14:30:45 INF] Processing file: INBOX\FAC-2026-001234.json
[14:30:45 INF] Processing transaction: Invoice=FAC-2026-001234 Amount=$150.50
[14:30:52 INF] Transaction processed: Invoice=FAC-2026-001234 Status=Successful
[14:30:52 INF] Transaction written to OUTBOX: FAC-2026-001234_successful_20260117_143052.json
[14:30:52 INF] INBOX file deleted: INBOX\FAC-2026-001234.json
```

## 🛠️ Compilación desde Código Fuente

```bash
# Clonar repositorio
git clone https://github.com/tu-usuario/cloverbridge.git
cd cloverbridge

# Compilar
dotnet build -c Release

# Publicar ejecutable standalone
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# El ejecutable estará en: bin\Release\net8.0-windows\win-x64\publish\
```

## 📞 Soporte

- **Logs:** `C:\CloverBridge\logs\`
- **Email:** soporte@tuempresa.com
- **GitHub:** https://github.com/tu-usuario/cloverbridge/issues

## 📄 Licencia

Propietario - Todos los derechos reservados

---

## 🎯 Ejemplo Completo de Integración

### Escenario: Cobro de Factura

1. **Cliente solicita pagar factura FAC-001234 por $150.50**

2. **Sistema ERP crea archivo en INBOX:**
   ```json
   {
     "invoiceNumber": "FAC-001234",
     "amount": 150.50,
     "customerName": "Juan Pérez",
     "tax": 15.05
   }
   ```

3. **CloverBridge procesa automáticamente**

4. **Cliente pasa su tarjeta en el terminal Clover**

5. **CloverBridge guarda resultado en OUTBOX:**
   ```json
   {
     "invoiceNumber": "FAC-001234",
     "status": "Successful",
     "amount": 150.50,
     "paymentInfo": {
       "cardLast4": "4242",
       "cardBrand": "VISA",
       "authCode": "123456"
     }
   }
   ```

6. **Sistema ERP lee OUTBOX y marca factura como PAGADA**

7. **Archivo de INBOX se elimina automáticamente**

✅ **¡Flujo completado!**

---

**Versión:** 1.0.0  
**Última actualización:** Enero 2026
