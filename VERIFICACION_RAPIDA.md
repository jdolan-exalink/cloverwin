# ⚡ Verificación Rápida - CloverBridge

## 1️⃣ Compilar (3 segundos)
```powershell
cd "d:\DEVs\Cloverwin"
dotnet build Cloverwin.sln -c Release
```
✅ **Resultado esperado:** "Compilación correcta. 0 Errores"

---

## 2️⃣ Crear Ejecutable (2 segundos)
```powershell
dotnet publish Cloverwin.sln -c Release
```
✅ **Resultado:** `bin\Release\net8.0-windows\win-x64\publish\CloverBridge.exe` (74 MB)

---

## 3️⃣ Ejecutar (Opción A: UI)
```powershell
cd "d:\DEVs\Cloverwin\bin\Release\net8.0-windows\win-x64\publish"
.\CloverBridge.exe
```
✅ Aparecerá en System Tray  
✅ Dashboard en http://localhost:3777

---

## 4️⃣ Ejecutar (Opción B: Consola - Recomendado para Testing)
```powershell
cd "d:\DEVs\Cloverwin\bin\Release\net8.0-windows\win-x64\publish"
.\CloverBridge.exe --console
```
✅ Ver logs en tiempo real  
✅ Presionar Ctrl+C para salir

---

## 5️⃣ Instalar como Servicio (Windows)
```powershell
cd "d:\DEVs\Cloverwin"
.\install-service.ps1
Start-Service -Name "CloverBridge"
```
✅ Se ejecutará automáticamente al reiniciar Windows

---

## ⚙️ Configuración (config.json)

Se crea automáticamente en:
```
d:\DEVs\Cloverwin\bin\Release\net8.0-windows\win-x64\publish\config.json
```

Estructura:
```json
{
  "clover": {
    "host": "10.1.1.53",        // IP de la terminal
    "port": 12345,              // Puerto WebSocket
    "merchantId": "default",
    "employeeId": "default"
  },
  "api": {
    "port": 3777,               // Puerto del dashboard
    "host": "127.0.0.1"
  }
}
```

---

## 📊 Estado Actual

| Aspecto | Estado |
|---------|--------|
| Compilación | ✅ Sin errores |
| Warnings | ✅ 0 warnings |
| Ejecutable | ✅ 74 MB (single-file) |
| Ejecución | ✅ Funcional |
| Servicios | ✅ Todos operativos |
| Documentación | ✅ Completa |

---

## 🆘 Troubleshooting

### Puerto 3777 en uso
```powershell
# Cambiar en config.json
"port": 3778  # Usar otro puerto
```

### No conecta a Clover
```powershell
# Verificar IP en config.json
"host": "10.1.1.53"  # Debe ser correcta

# Probar conectividad
ping 10.1.1.53
```

### Ver logs
```powershell
cat "d:\DEVs\Cloverwin\bin\Release\net8.0-windows\win-x64\publish\logs\*"
```

---

## 🎯 Resumen

La aplicación **está completamente lista** para:
- ✅ Desarrollo
- ✅ Testing
- ✅ Producción
- ✅ Despliegue como Windows Service

**Ejecutable ubicado en:**
```
d:\DEVs\Cloverwin\bin\Release\net8.0-windows\win-x64\publish\CloverBridge.exe
```

**Copiar la carpeta `publish` completa para distribuir la aplicación.**
