export type PendingSubscription =
  | { stage: 'Order'; priceId: string; key: string }
  | { stage: 'Payment'; orderId: string; cardId: string; key: string; technical: boolean };

// Only non-secret request identifiers are persisted; reload never starts a payment.
export function restoreSubscriptionRequest(storageKey: string): PendingSubscription | null {
  try {
    const raw = sessionStorage.getItem(storageKey);
    if (!raw) return null;
    const value: unknown = JSON.parse(raw);
    if (typeof value === 'object' && value !== null) {
      const row = value as Record<string, unknown>;
      const guid = (item: unknown) =>
        typeof item === 'string' &&
        /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(item);
      const keys = Object.keys(row).sort().join(',');
      if (
        row['stage'] === 'Order' &&
        keys === 'key,priceId,stage' &&
        guid(row['key']) &&
        guid(row['priceId'])
      )
        return value as PendingSubscription;
      if (
        row['stage'] === 'Payment' &&
        keys === 'cardId,key,orderId,stage,technical' &&
        guid(row['key']) &&
        guid(row['orderId']) &&
        guid(row['cardId']) &&
        typeof row['technical'] === 'boolean'
      )
        return value as PendingSubscription;
    }
    sessionStorage.removeItem(storageKey);
  } catch {
    try {
      sessionStorage.removeItem(storageKey);
    } catch {
      /* Storage may be unavailable. */
    }
  }
  return null;
}
