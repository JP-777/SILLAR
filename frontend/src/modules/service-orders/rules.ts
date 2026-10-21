import type {
  CreateServiceOrderRequest,
  ServiceOrderDetail,
  ServiceOrderStatus,
  UpdateServiceOrderRequest,
} from './contracts';

export const SERVICE_ORDER_STATES: readonly ServiceOrderStatus[] = [
  'received', 'in_progress', 'ready', 'completed', 'cancelled',
];

export const SERVICE_ORDER_LABELS: Readonly<Record<ServiceOrderStatus, string>> = {
  received: 'Recibida',
  in_progress: 'En proceso',
  ready: 'Lista',
  completed: 'Completada',
  cancelled: 'Cancelada',
};

const TRANSITIONS: Readonly<Record<ServiceOrderStatus, readonly ServiceOrderStatus[]>> = {
  received: ['in_progress', 'cancelled'],
  in_progress: ['ready', 'cancelled'],
  ready: ['completed', 'in_progress', 'cancelled'],
  completed: [],
  cancelled: [],
};

/** Recomendación de interfaz. El servidor M05b siempre vuelve a validar. */
export function permittedTargets(status: ServiceOrderStatus, canCancel: boolean): readonly ServiceOrderStatus[] {
  return TRANSITIONS[status].filter(target => target !== 'cancelled' || canCancel);
}

export function mayEdit(status: ServiceOrderStatus): boolean {
  return status === 'received' || status === 'in_progress';
}

export function isTerminal(status: ServiceOrderStatus): boolean {
  return status === 'completed' || status === 'cancelled';
}

/** El servidor impide cambiar responsables de órdenes cerradas: no ofrecer una acción inútil. */
export function mayAssign(order: Pick<ServiceOrderDetail, 'currentStatus' | 'currentAssignee'>, currentAdminUserId: number): boolean {
  return !isTerminal(order.currentStatus) && order.currentAssignee?.adminUserId !== currentAdminUserId;
}

export function mayRelease(order: Pick<ServiceOrderDetail, 'currentStatus' | 'currentAssignee'>, currentAdminUserId: number): boolean {
  return !isTerminal(order.currentStatus) && order.currentAssignee?.adminUserId === currentAdminUserId;
}

export function checkCreate(request: CreateServiceOrderRequest, nowMs: number = Date.now()): string[] {
  const errors: string[] = [];
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(request.idempotencyKey)) {
    errors.push('La clave de idempotencia es inválida.');
  }
  if (!request.customerId && !request.customerName?.trim()) errors.push('Indica un contacto o selecciona un cliente.');
  if (!request.customerId && !request.customerPhone?.trim() && !request.customerEmail?.trim()) {
    errors.push('El contacto manual necesita teléfono o correo.');
  }
  if (request.items.length === 0) errors.push('Agrega al menos una línea.');
  request.items.forEach((item, index) => {
    if (!Number.isSafeInteger(item.serviceId) || item.serviceId <= 0) errors.push(`Línea ${index + 1}: selecciona un servicio válido.`);
    if (!item.requestedDetails?.trim()) errors.push(`Línea ${index + 1}: describe las características del encargo.`);
    if (!Number.isFinite(item.quantity) || item.quantity <= 0) errors.push(`Línea ${index + 1}: cantidad debe ser positiva.`);
    if (item.agreedUnitPrice !== null && (!Number.isFinite(item.agreedUnitPrice) || item.agreedUnitPrice < 0)) {
      errors.push(`Línea ${index + 1}: el precio no puede ser negativo.`);
    }
  });
  if (request.promisedAt !== null) {
    const promised = Date.parse(request.promisedAt);
    const received = request.receivedAt === null ? nowMs : Date.parse(request.receivedAt);
    if (!Number.isFinite(promised) || !Number.isFinite(received) || promised < received) {
      errors.push('La fecha comprometida es inválida o anterior a la recepción.');
    }
  }
  return errors;
}

export function updateFromDetail(detail: ServiceOrderDetail): UpdateServiceOrderRequest {
  return {
    expectedUpdatedAt: detail.updatedAt,
    customerId: detail.customerId,
    customerName: detail.customerName,
    customerPhone: detail.customerPhone,
    customerEmail: detail.customerEmail,
    receivedNotes: detail.receivedNotes,
    promisedAt: detail.promisedAt,
    items: detail.items.map(item => ({
      serviceOrderItemId: item.serviceOrderItemId,
      serviceId: null,
      requestedDetails: item.requestedDetails,
      quantity: item.quantity,
      agreedUnitPrice: item.agreedUnitPrice,
    })),
  };
}

/** Resultado cero es monto pactado válido; null es precio todavía pendiente. */
export function agreedTotal(items: readonly { quantity: number; agreedUnitPrice: number | null }[]): number | null {
  if (items.some(item => item.agreedUnitPrice === null)) return null;
  return items.reduce((sum, item) => sum + item.quantity * (item.agreedUnitPrice ?? 0), 0);
}

/** Valida contra la última vitrina publicada cargada; el backend vuelve a comprobar. */
export function unpublishedServiceLines(
  items: readonly Pick<CreateServiceOrderRequest['items'][number], 'serviceId'>[],
  publishedServiceIds: ReadonlySet<number>,
): number[] {
  return items.flatMap((item, index) =>
    publishedServiceIds.has(item.serviceId) ? [] : [index + 1],
  );
}
