import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { contrasenaPostgresCoherente } from '../coherencia-env.mjs';

test('la .env.example versionada mantiene coherentes sus dos representaciones', () => {
  const ejemplo = readFileSync(
    new URL('../../.env.example', import.meta.url),
    'utf8',
  );

  assert.doesNotThrow(() => contrasenaPostgresCoherente(ejemplo));
});

test('acepta una plantilla cuyas dos representaciones de contraseña coinciden', () => {
  const ejemplo = [
    'POSTGRES_PASSWORD=secreto_coherente',
    'ConnectionStrings__Default=Host=localhost;Database=sillar;Username=postgres;Password=secreto_coherente',
  ].join('\n');

  assert.equal(
    contrasenaPostgresCoherente(ejemplo),
    'secreto_coherente',
  );
});

test('rechaza una plantilla incoherente sin revelar ninguno de los valores', () => {
  const postgres = 'secreto_postgres_uno';
  const cadena = 'secreto_cadena_dos';

  const ejemplo = [
    `POSTGRES_PASSWORD=${postgres}`,
    `ConnectionStrings__Default=Host=localhost;Database=sillar;Username=postgres;Password=${cadena}`,
  ].join('\n');

  assert.throws(
    () => contrasenaPostgresCoherente(ejemplo),
    (error) => {
      assert.match(error.message, /no coinciden/i);
      assert.doesNotMatch(error.message, new RegExp(postgres));
      assert.doesNotMatch(error.message, new RegExp(cadena));
      return true;
    },
  );
});

test('falla cerrado si falta Password= en la cadena', () => {
  const ejemplo = [
    'POSTGRES_PASSWORD=secreto',
    'ConnectionStrings__Default=Host=localhost;Database=sillar;Username=postgres',
  ].join('\n');

  assert.throws(
    () => contrasenaPostgresCoherente(ejemplo),
    /exactamente un componente Password=/,
  );
});

test('falla cerrado si POSTGRES_PASSWORD no es único', () => {
  const ejemplo = [
    'POSTGRES_PASSWORD=uno',
    'POSTGRES_PASSWORD=dos',
    'ConnectionStrings__Default=Host=localhost;Password=uno',
  ].join('\n');

  assert.throws(
    () => contrasenaPostgresCoherente(ejemplo),
    /exactamente una vez POSTGRES_PASSWORD/,
  );
});
