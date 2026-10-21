import type { ServiceOrderDetail, UpdateServiceOrderLine, UpdateServiceOrderRequest } from './contracts';

/** UI-only identity, never transmitted to the API. Keeps focus stable after reordering lines. */
export type EditLine = UpdateServiceOrderLine & { uiKey: string };
export type EditDraft = Omit<UpdateServiceOrderRequest, 'items'> & { items: EditLine[] };

/** Attach durable keys to persisted lines; new keys are generated when adding new lines. */
export function withDraftKeys(request: UpdateServiceOrderRequest): EditDraft {
  return {
    ...request,
    items: request.items.map((line, index) => ({
      ...line,
      uiKey: line.serviceOrderItemId ?? `new-initial-${index}`,
    })),
  };
}

/** Remove every UI-only property so the payload matches the ASP.NET record exactly. */
export function toUpdateRequest(form: EditDraft, promisedAt: string | null): UpdateServiceOrderRequest {
  return {
    expectedUpdatedAt: form.expectedUpdatedAt,
    customerId: form.customerId, customerName: form.customerName,
    customerPhone: form.customerPhone, customerEmail: form.customerEmail,
    receivedNotes: form.receivedNotes, promisedAt,
    items: form.items.map(({ serviceOrderItemId, serviceId, requestedDetails, quantity, agreedUnitPrice }) => ({
      serviceOrderItemId, serviceId, requestedDetails, quantity, agreedUnitPrice,
    })),
  };
}

/** M05b protege snapshots: líneas existentes jamás se eliminan por PUT. */
export function newServiceOrderLine(serviceId: number): UpdateServiceOrderLine {
  return { serviceOrderItemId: null, serviceId, requestedDetails: '', quantity: 1, agreedUnitPrice: null };
}

export function moveLine<T>(lines: readonly T[], from: number, direction: -1 | 1): T[] {
  const to = from + direction;
  if (from < 0 || to < 0 || from >= lines.length || to >= lines.length) return [...lines];
  const next = [...lines];
  [next[from], next[to]] = [next[to], next[from]];
  return next;
}

/** Solo una línea aún no guardada puede descartarse; un snapshot durable permanece. */
export function removeUnsavedLine<T extends Pick<UpdateServiceOrderLine, 'serviceOrderItemId'>>(lines: readonly T[], index: number): T[] {
  const line = lines[index];
  if (!line || line.serviceOrderItemId !== null) return [...lines];
  return lines.filter((_, i) => i !== index);
}

export function checkUpdate(request: UpdateServiceOrderRequest, original: ServiceOrderDetail): string[] {
  const problems: string[] = [];
  if (request.expectedUpdatedAt !== original.updatedAt) problems.push('La versión de esta orden cambió. Recarga el detalle.');
  if (request.customerId === null && !request.customerName?.trim()) problems.push('Debes indicar un contacto.');
  if (request.customerId === null && !request.customerPhone?.trim() && !request.customerEmail?.trim()) {
    problems.push('El contacto manual necesita teléfono o correo.');
  }
  if (request.items.length === 0) problems.push('La orden requiere una o varias líneas.');
  const ids = original.items.map(i => i.serviceOrderItemId);
  const persisted = request.items.filter(i => i.serviceOrderItemId !== null).map(i => i.serviceOrderItemId);
  if (persisted.length !== ids.length || new Set(persisted).size !== ids.length || ids.some(id => !persisted.includes(id))) {
    problems.push('No es posible eliminar ni duplicar líneas históricas.');
  }
  request.items.forEach((line, index) => {
    if (line.serviceOrderItemId !== null && line.serviceId !== null) problems.push(`Línea ${index+1}: no cambies el servicio histórico.`);
    if (line.serviceOrderItemId === null && (!Number.isSafeInteger(line.serviceId) || (line.serviceId ?? 0) <= 0)) {
      problems.push(`Línea ${index+1}: elige un servicio publicado.`);
    }
    if (!line.requestedDetails?.trim()) problems.push(`Línea ${index+1}: describe las características del encargo.`);
    if (!Number.isFinite(line.quantity) || line.quantity <= 0) problems.push(`Línea ${index+1}: cantidad inválida.`);
    if (line.agreedUnitPrice !== null && (!Number.isFinite(line.agreedUnitPrice) || line.agreedUnitPrice < 0)) {
      problems.push(`Línea ${index+1}: precio inválido.`);
    }
  });
  if (request.promisedAt !== null && (Number.isNaN(Date.parse(request.promisedAt)) || Date.parse(request.promisedAt) < Date.parse(original.receivedAt))) {
    problems.push('Fecha comprometida anterior a la recepción o inválida.');
  }
  return problems;
}


/** Si el operador no cambió el campo datetime-local, mantener el instante original
 * (incluidos segundos y milisegundos que el control visual no enseña).
 * Evita que editar únicamente las notas altere accidentalmente el compromiso.
 */
export function resolvePromisedAt(
  original: string | null,
  initialLocalValue: string,
  currentLocalValue: string,
  convertLocal: (value: string) => string | null,
): string | null {
  return currentLocalValue === initialLocalValue
    ? original
    : convertLocal(currentLocalValue);
}

/** Vitrina de M05a: no exigir que una línea histórica siga publicada,
 * porque su fotografía es inmutable. Solo verificar las líneas nuevas.
 */
export function unpublishedNewServiceLines(
  items: readonly Pick<UpdateServiceOrderLine, 'serviceOrderItemId' | 'serviceId'>[],
  publishedIds: ReadonlySet<number>,
): number[] {
  return items.flatMap((line, index) =>
    line.serviceOrderItemId === null && (line.serviceId === null || !publishedIds.has(line.serviceId))
      ? [index + 1]
      : [],
  );
}
