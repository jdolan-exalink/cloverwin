# 🚀 Guía de Inicio Rápido - CloverBridge UI C#

## ⚡ START HERE

### 1. Compilar (si es necesario)
```powershell
cd D:\DEVs\Clover2\windows
dotnet build
```

### 2. Ejecutar
```powershell
.\bin\Debug\net8.0-windows\CloverBridge.exe --ui
```

---

## 📂 Estructura de Archivos

Después de ejecutar por primera vez, verás:

```
📁 D:\DEVs\Clover2\windows\bin\Debug\net8.0-windows\
│
├── 🟢 CloverBridge.exe     ← Ejecutable principal (152 KB)
├── 📄 config.json          ← Configuración (editable)
│
├── 📁 INBOX/               ← Aquí se crean las solicitudes
├── 📁 OUTBOX/              ← Aquí llegan las respuestas
├── 📁 ARCHIVE/             ← Transacciones procesadas
└── 📁 logs/                ← Logs diarios
    └── cloverbridge-20260115.log
```

---

## 🎯 Probar una Venta

### Paso 1: Configurar Terminal
```
1. Abrir la app con --ui
2. Click en tab "Config"
3. Verificar:
   - Host: 10.1.1.53 (o IP de tu terminal)
   - Port: 12345
   - Secure: ✓ true
4. Click "Guardar Config"
```

### Paso 2: Enviar Transacción
```
1. Click en tab "Venta"
2. Ingresar monto: 1000
   (1000 = $10.00)
3. Click "Enviar Venta"
```

### Paso 3: Verificar
```powershell
# Ver archivo creado:
Get-ChildItem .\bin\Debug\net8.0-windows\INBOX

# Ver contenido:
Get-Content .\bin\Debug\net8.0-windows\INBOX\sale_*.json
```

**Salida esperada:**
```json
{
  "type": "sale",
  "amount": 1000,
  "externalId": "TEST-20260115-230645",
  "timestamp": "2026-01-15T23:06:45.000Z"
}
```

---

## 📋 Tabs Disponibles

| Tab | Función | Monto ejemplo |
|-----|---------|---------------|
| 🎫 **QR Code** | Generar código QR de pago | 1000 = $10.00 |
| 💳 **Venta** | Transacción de venta | 2500 = $25.00 |
| 🔐 **Auth** | Pre-autorización | 5000 = $50.00 |
| ❌ **Void** | Anular transacción | - |
| 💰 **Refund** | Devolución | 1000 = $10.00 |
| ⚙️ **Config** | Editar configuración | - |
| 📋 **Logs** | Ver logs en tiempo real | - |

---

## 🔍 Ver Logs

### En la UI
```
1. Click en tab "Logs"
2. Scroll automático al final
3. Filtrar por tipo de mensaje
```

### En archivo
```powershell
# Ver últimas 20 líneas:
Get-Content .\bin\Debug\net8.0-windows\logs\cloverbridge-*.log -Tail 20

# Ver en tiempo real (follow):
Get-Content .\bin\Debug\net8.0-windows\logs\cloverbridge-*.log -Wait -Tail 10
```

---

## 💡 Tips Rápidos

### ✅ Archivos portables
Toda la aplicación está en una carpeta. Para hacer backup:
```powershell
# Copiar todo:
Copy-Item -Recurse .\bin\Debug\net8.0-windows\ C:\Backup\CloverBridge-$(Get-Date -Format 'yyyyMMdd')
```

### ✅ Múltiples instancias
Ejecuta copias en diferentes carpetas para testing:
```powershell
# Instancia 1 - Terminal A
cd D:\Test\TerminalA
.\CloverBridge.exe --ui

# Instancia 2 - Terminal B  
cd D:\Test\TerminalB
.\CloverBridge.exe --ui
```

### ✅ Editar config manualmente
```powershell
notepad .\bin\Debug\net8.0-windows\config.json
```

### ✅ Limpiar datos de prueba
```powershell
# Borrar solicitudes procesadas:
Remove-Item .\bin\Debug\net8.0-windows\INBOX\*.json
Remove-Item .\bin\Debug\net8.0-windows\ARCHIVE\*.json

# Borrar logs viejos:
Remove-Item .\bin\Debug\net8.0-windows\logs\*.log
```

---

## ⚙️ Modos de Ejecución

```powershell
# UI Testing Dashboard (Recomendado para desarrollo)
.\CloverBridge.exe --ui

# Consola (Ver logs en terminal)
.\CloverBridge.exe --console

# Servicio (Background service)
.\CloverBridge.exe --service

# System Tray (Icono en bandeja)
.\CloverBridge.exe
```

---

## 🐛 Problemas Comunes

### ❌ "El archivo está en uso"
```powershell
Get-Process CloverBridge | Stop-Process -Force
```

### ❌ No conecta al terminal
```
1. Verificar IP en tab "Config"
2. Ping al terminal:
   ping 10.1.1.53
3. Verificar puerto 12345 abierto
4. Ver logs para detalles
```

### ❌ No aparecen logs
```powershell
# Verificar carpeta logs existe:
Test-Path .\bin\Debug\net8.0-windows\logs

# Crear manualmente si no existe:
New-Item -ItemType Directory -Path .\bin\Debug\net8.0-windows\logs
```

---

## 📞 Desarrollo

### Recompilar después de cambios
```powershell
# Detener app:
Get-Process CloverBridge | Stop-Process -Force

# Compilar:
dotnet build --no-restore

# Ejecutar:
.\bin\Debug\net8.0-windows\CloverBridge.exe --ui
```

### Ver errores de compilación
```powershell
dotnet build --verbosity detailed
```

### Compilar Release
```powershell
dotnet build --configuration Release

# Ejecutable en:
.\bin\Release\net8.0-windows\CloverBridge.exe
```

---

## 📊 Checklist de Verificación

Después de iniciar, verificar:

- [ ] Ventana de UI abre correctamente
- [ ] Status muestra "Conectando..." o "Desconectado"
- [ ] Tab "Config" muestra configuración
- [ ] Carpetas creadas: INBOX, OUTBOX, ARCHIVE, logs
- [ ] Archivo config.json existe
- [ ] Al enviar venta, se crea archivo .json en INBOX
- [ ] Logs muestran actividad en tab "Logs"

---

**¿Todo listo?** 🎉

Ejecuta:
```powershell
.\bin\Debug\net8.0-windows\CloverBridge.exe --ui
```

Y empieza a probar transacciones!

---

**Última actualización:** 15/01/2026
