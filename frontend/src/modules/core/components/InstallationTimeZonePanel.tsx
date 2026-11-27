import { useEffect, useState, type FormEvent } from 'react';
import { http } from '../../../shared/http/client';
import { describe } from '../../../shared/errors/messages';
import { Alert, Button, Card, Field, Input } from '../../../shared/ui';

interface ZoneReading { zoneId: string | null; status: string; observedAt: string; }

export function InstallationTimeZonePanel({ canEdit }: { canEdit: boolean }) {
  const [reading, setReading] = useState<ZoneReading | null>(null);
  const [zone, setZone] = useState('');
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    void http.get<ZoneReading>('/admin/settings/installation-time-zone', { signal: controller.signal })
      .then(value => { setReading(value); setZone(value.zoneId ?? ''); })
      .catch(error => {
        if (controller.signal.aborted) return;
        const result = describe(error, 'consultar la zona horaria');
        if (result.kind !== 'silent') setFailure(result.message);
      });
    return () => controller.abort();
  }, []);

  async function save(event: FormEvent) {
    event.preventDefault(); setBusy(true); setFailure(null);
    try {
      setReading(await http.put<ZoneReading>('/admin/settings/installation-time-zone', { zoneId: zone }));
    } catch (error) {
      const result = describe(error, 'acreditar la zona horaria');
      if (result.kind !== 'silent') setFailure(result.message);
    } finally { setBusy(false); }
  }

  return <Card title="Zona horaria del negocio" subtitle="Se usa para interpretar los períodos de los reportes.">
    <p role="status">{reading
      ? `${reading.zoneId ?? 'Sin zona disponible'} · ${reading.status === 'Verified' ? 'Verificada' : 'Sin verificación vigente'}`
      : failure ? 'No se pudo consultar la zona.' : 'Consultando la zona…'}</p>
    {failure && <Alert tone="danger">{failure}</Alert>}
    {canEdit ? <form onSubmit={save}>
      <Field label="Zona IANA" hint="Por ejemplo: America/Lima. Si se actualizaron las reglas horarias, acredita de nuevo la misma zona.">
        <Input value={zone} onChange={event => setZone(event.target.value)} required disabled={busy} />
      </Field>
      <Button type="submit" disabled={busy || !zone}>{busy ? 'Acreditando…' : 'Acreditar zona'}</Button>
    </form> : <p>El superadministrador puede acreditar o cambiar la zona.</p>}
  </Card>;
}
