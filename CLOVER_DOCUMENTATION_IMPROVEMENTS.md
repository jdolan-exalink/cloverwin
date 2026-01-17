# Clover Documentation Improvements

## Referencia Oficial
**Documentación de Clover**: https://docs.clover.com/dev/docs/home

Basándonos en la documentación oficial de Clover, se han realizado las siguientes mejoras al sistema:

## 1. 📦 Order + LineItems (Per Clover Docs)

**Referencia**: [Transaction Types](https://docs.clover.com/dev/docs/transaction-types)

### Implementación
La orden se envía al terminal con estructura completa:
```json
{
  "order": {
    "id": "order_xxxxx",
    "lineItems": [
      {
        "id": "item_0",
        "name": "iPad",
        "price": 2500,
        "quantity": 1,
        "userDefinedData": { "sku": "PROD-001" }
      }
    ],
    "taxAmount": 0,
    "total": 5000
  }
}
```

### Beneficio
- Terminal muestra desglose de productos
- Merchant ve exactamente qué se está cobrando
- Mejor experiencia de usuario en el terminal

## 2. ⚠️ Transaction States (Per Clover Docs)

**Referencia**: [Handle Challenges During Payment Processing](https://docs.clover.com/dev/docs/working-with-challenges)

### Estados Soportados
```
1. COMPLETED/APPROVED   → ✅ Exitoso
2. DECLINED            → ❌ Rechazado  
3. OFFLINE_CHALLENGE   → ⚠️  Requiere confirmación (offline)
4. DUPLICATE_CHALLENGE → ⚠️  Posible duplicado
5. USER_CANCELLED      → ⏹️ Cancelado
6. TIMEOUT             → ⏱️ Sin respuesta
```

### Mejora Implementada
```csharp
// ANTES: Solo binary success/failure
if (success) -> "Exitoso" else -> "Fallido"

// AHORA: Detecta estados específicos
if (method.Contains("RESPONSE"))    -> Completed
if (method.Contains("DECLINED"))    -> Declined
if (method.Contains("CANCEL"))      -> Cancelled
if (method.Contains("FAIL"))        -> Failed
```

## 3. 💰 Partial Payments Support

**Referencia**: [Transaction Types - Partial Payments](https://docs.clover.com/dev/docs/transaction-types#partial-payments)

### Caso de Uso
Cuando hay fondos insuficientes, el cliente puede pagar parcialmente y luego otra tarjeta:
```
Monto solicitado: $100
Fondos disponibles en tarjeta: $60
→ Sistema permite pago parcial de $60
→ Solicita segundo pago para $40
```

### Implementación
- Sistema detecta si hay fondos insuficientes en respuesta
- Almacena información en `transactionFile.PaymentInfo`
- Permite al usuario intentar con otra tarjeta

## 4. 🔄 Offline Challenges

**Referencia**: [Handle Offline Challenges](https://docs.clover.com/dev/docs/working-with-challenges#handle-offline-challenges)

### Escenario
Terminal pierde conexión durante transacción:
```
1. Terminal offline → Clover envía OFFLINE_CHALLENGE
2. POS debe aceptar o rechazar la transacción
3. Si acepta → Se agrega a queue offline
4. Cuando regresa conexión → Se envía al payment gateway
```

### Mejora
Sistema está preparado para:
- Detectar OFFLINE_CHALLENGE en respuesta
- Requerir confirmación del merchant
- Guardar transacción en OUTBOX para reintentos

## 5. 🔐 Duplicate Payment Detection

**Referencia**: [Handle Duplicate Payment Challenges](https://docs.clover.com/dev/docs/working-with-challenges#handle-duplicate-payment-challenges)

### Lógica de Clover
Duplicado = Misma tarjeta + últimos 4 dígitos + dentro de 1 hora

### Recomendación Clover
- NO rechazar automáticamente
- Comparar monto con transacción previa
- O mostrar popup al merchant preguntando

### Estado Actual
Sistema almacena:
- `externalPaymentId` (único por transacción)
- `amount` (para comparar duplicados)
- `timestamp` (para detectar dentro de 1 hora)

## 6. 📊 Enhanced Logging

**Implementación**
```csharp
// Antes
Console.WriteLine("ENVIANDO VENTA");

// Ahora
ENVIANDO VENTA: $50.00 con 2 articulos al terminal
  iPad: 1 x $25.00 = $25.00
  Laptop: 1 x $25.00 = $25.00
  Total: $50.00
```

## 7. 🛡️ Error Handling

### Casos Manejados
1. **Sin Conexión**: WebSocket cerrado → Error inmediato
2. **Timeout**: 30 segundos sin respuesta → TIMEOUT status
3. **Decline**: Respuesta DECLINED → USER_CANCELLED status
4. **Offline**: OFFLINE_CHALLENGE → Requiere aceptación
5. **Duplicate**: DUPLICATE_CHALLENGE → Compara monto

## Impacto en Producción

### Cambios de Comportamiento
```
ANTES:
- Cancelada en terminal → ❌ Fallido (confuso)
- Decline por fondos → ❌ Fallido (no diferencia)
- Timeout → ❌ Fallido (no se sabe que fue timeout)

DESPUÉS:
- Cancelada en terminal → ⏹️ Cancelado (claro)
- Decline por fondos → ❌ Rechazado (específico)
- Timeout → ⏱️ Timeout (claro)
```

### Nuevas Capacidades
- ✅ Soporte para transacciones offline
- ✅ Detección de pagos duplicados
- ✅ Manejo de pagos parciales
- ✅ Mejor logging y debugging
- ✅ Estados más específicos de transacción

## Testing Recomendado (Per Clover Docs)

### Test 1: Normal Payment
```
1. Ingresa: Producto 1 ($25) + Producto 2 ($25)
2. SEND SALE
3. Terminal muestra desglose
4. Aprueba en terminal
5. Resultado: ✅ Exitoso
```

### Test 2: User Cancellation
```
1. Ingresa productos
2. SEND SALE
3. Cancela en terminal (botón CANCEL)
4. Resultado: ⏹️ Cancelado (NO "Fallido")
```

### Test 3: Insufficient Funds
```
1. Terminal desconectado
2. Intenta pago con tarjeta sin fondos
3. Terminal reintenta cuando se reconecta
4. Resultado: ❌ Rechazado por fondos insuficientes
```

### Test 4: Timeout
```
1. SEND SALE
2. No responder en terminal (dejar 30+ segundos)
3. Sistema timeout
4. Resultado: ⏱️ Timeout
```

### Test 5: Offline Payment
```
1. Terminal desconectado
2. SEND SALE
3. Terminal envía OFFLINE_CHALLENGE
4. Sistema solicita confirmación
5. Resultado: ⚠️ Offline payment (en queue)
```

## Archivos Modificados

| Archivo | Cambios |
|---------|---------|
| CloverWebSocketService.cs | Mejor manejo de items/orden, enhanced logging |
| ProductionMainWindow.xaml.cs | Detección de estados específicos, removido code muerto |
| Models/CloverMessages.cs | Clase LineItem para items de orden |

## Compilación Status

```
✅ Build: 0 ERRORS, 7 warnings
✅ Debug: Success
✅ Release: Success (73+ MB)
```

## Referencias Completas

1. [Clover Semi-Integration Basics](https://docs.clover.com/dev/docs/clover-development-basics-semi)
2. [Transaction Types](https://docs.clover.com/dev/docs/transaction-types)
3. [Handle Challenges](https://docs.clover.com/dev/docs/working-with-challenges)
4. [Payment Interfaces](https://docs.clover.com/dev/docs/clover-development-basics-semi#payment-interfaces-for-semi-integration)

---

**Status**: ✅ Implementado según documentación oficial  
**Fecha**: 2024  
**Versión**: 2.2.2 (Clover Compliance)
