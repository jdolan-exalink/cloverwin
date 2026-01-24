# Mapeo de Respuesta Clover - Implementación Completada

## Resumen de Cambios

Se implementó el mapeo correcto de la respuesta de Clover según el documento de integración proporcionado. El problema original era que el código buscaba `txState == "SUCCESS"` pero la estructura real de Clover usa `result == "SUCCESS"`.

## Estructura de Respuesta Clover Esperada

```json
{
  "id": "R4QZ7C9W12345",          // ID_TRANSACCION (vital para refunds)
  "orderId": "K123456789",        // ID de orden
  "device": {
    "id": "C030UQ12345678"
  },
  "result": "SUCCESS",            // INDICADOR DE ÉXITO → estado
  "createdTime": 1600000000000,
  "taxAmount": 0,
  "tipAmount": 0,
  "amount": 1500,                 // Monto en centavos
  "externalPaymentId": "FB-12225-12345678",  // NRO_DE_FACTURA
  "cardTransaction": {
    "cardType": "VISA",           // Para → tarjeta
    "last4": "4242",              // Para → tarjeta
    "authCode": "123456",         // NUMERO_DE_CUPON (autorización)
    "entryType": "EMV_CONTACT",
    "type": "AUTH",
    "state": "CLOSED"
  }
}
```

## Mapeo Implementado

| Tu Campo          | Ruta en JSON Clover                    | Implementación |
|-------------------|----------------------------------------|----------------|
| `estado`          | `result` (validar si es `"SUCCESS"`)   | ✅ Implementado |
| `id_transaccion`  | `id` (raíz)                            | ✅ Implementado |
| `numero_de_cupon` | `cardTransaction.authCode`             | ✅ Implementado |
| `tarjeta`         | `cardTransaction.cardType + " " + last4` | ✅ Implementado |
| `nro_de_factura`  | `externalPaymentId`                    | ✅ Implementado |

## Archivos Modificados

### 1. `Services/TransactionFileService.cs`

#### Método `ConvertToTransactionResponse` (Líneas 314-495)
- Ahora verifica `result == "SUCCESS"` (documento de integración)
- También mantiene compatibilidad con `txState == "SUCCESS"` (WebSocket alternativo)
- Si hay objeto `payment`, lo considera exitoso aunque no haya `result`
- Maneja payload como string JSON anidado (stringify)
- Extrae `externalPaymentId` desde la raíz y desde el objeto `payment`

#### Método `ExtractPaymentInfo` (Líneas 573-665)
- Extrae todos los campos del documento:
  - `id` → `CloverPaymentId`
  - `amount`, `tipAmount` (en centavos, convertido a dólares)
  - `orderId` (como objeto o string directo)
  - `externalPaymentId` → número de factura
  - `cardTransaction` → `authCode`, `cardType`, `last4`
  - `device`, `createdTime` (para logging)

#### Método `ExtractCardTransaction` (Nuevo, Líneas 556-571)
- Extrae todos los campos de `cardTransaction`:
  - `authCode` → número de cupón
  - `cardType` + `last4` → información de tarjeta
  - `entryType`, `type`, `referenceId`, `transactionNo`, `first6`

#### Método `ProcessPaymentResult` (Líneas 237-312)
- Mejor logging para debugging
- Maneja caso donde `Success=true` pero `Payment=null`
- Agrega detección de "reject" en razones de fallo
- Maneja "no payload received" como fallo específico

## Modelo de Respuesta Generada

Después de procesar una transacción exitosa, tu `TransactionFile` contendrá:

```json
{
  "transactionId": "guid-local",
  "invoiceNumber": "INV-2026-001",
  "status": "Successful",
  "paymentInfo": {
    "cloverPaymentId": "R4QZ7C9W12345",     // ← id_transaccion
    "cloverOrderId": "K123456789",
    "cardLast4": "4242",                     // ← tarjeta (parte 2)
    "cardBrand": "VISA",                     // ← tarjeta (parte 1)
    "authCode": "123456",                    // ← numero_de_cupon
    "totalAmount": 15.00,                    // Convertido de centavos
    "tip": 0
  }
}
```

## Cómo Obtener tu JSON Objetivo

Para generar el JSON que necesitas, puedes usar:

```csharp
// Después de ProcessPaymentResult, tu transación tendrá:
var respuestaFinal = new
{
    numero_de_cupon = transaction.PaymentInfo?.AuthCode ?? "",
    estado = transaction.Status == TransactionStatus.Successful ? "aprobado" : "fallo",
    tarjeta = transaction.PaymentInfo?.CardBrand != null 
        ? $"{transaction.PaymentInfo.CardBrand} {transaction.PaymentInfo.CardLast4}" 
        : "N/A",
    nro_de_factura = transaction.InvoiceNumber,
    id_transaccion = transaction.PaymentInfo?.CloverPaymentId ?? ""
};
```

## Verificación

Para verificar que el mapeo funciona:

1. Ejecuta una transacción real con el terminal Clover
2. Revisa los logs en `logs/` - verás mensajes como:
   - `ConvertToTransactionResponse: result = SUCCESS, success = True`
   - `ProcessPaymentResult: Card info - Brand=VISA, Last4=4242, AuthCode=123456`
3. El archivo en `OUTBOX/` contendrá todos los datos mapeados

## Notas Importantes

1. **`externalPaymentId`**: Solo aparece en la respuesta si lo enviaste en el request. Ya se envía en `payIntent.externalPaymentId`.

2. **Montos en centavos**: Clover envía montos en centavos (1500 = $15.00). El código convierte automáticamente dividiendo por 100.

3. **Compatibilidad**: El código mantiene compatibilidad con diferentes versiones del protocolo Clover (Network Pay Display API y WebSocket API).
