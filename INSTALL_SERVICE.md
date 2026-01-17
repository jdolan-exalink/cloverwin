# CloverBridge - Instalación como Servicio de Windows

## Instalar el servicio

```powershell
# Como administrador
sc.exe create CloverBridge binPath="C:\Path\To\CloverBridge.exe --service" start=auto
sc.exe description CloverBridge "Bridge entre ERP y terminal Clover"
```

## Iniciar el servicio

```powershell
sc.exe start CloverBridge
```

## Detener el servicio

```powershell
sc.exe stop CloverBridge
```

## Desinstalar el servicio

```powershell
sc.exe delete CloverBridge
```

## Verificar estado

```powershell
sc.exe query CloverBridge
```

## Usando PowerShell (alternativa)

```powershell
# Instalar
New-Service -Name "CloverBridge" -BinaryPathName "C:\Path\To\CloverBridge.exe --service" -StartupType Automatic -Description "Bridge entre ERP y terminal Clover"

# Iniciar
Start-Service CloverBridge

# Detener
Stop-Service CloverBridge

# Desinstalar
Remove-Service CloverBridge
```
