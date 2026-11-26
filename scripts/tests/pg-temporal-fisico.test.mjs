// P1-POST-G6-R10 · las decisiones puras de la precondición física de CommerceBridge.
//     node --test scripts/tests/pg-temporal-fisico.test.mjs
import test from 'node:test';
import assert from 'node:assert/strict';
import { cadenaAdministrativa, credencial, idDeNombre, nombreValido, textoDeIdentidad } from '../pg-temporal-fisico.mjs';

test('la cadena es administrativa, completa y con el puerto escrito', () => {
  assert.equal(
    cadenaAdministrativa({ puerto: 32768, usuario: 'fisico_ab12', clave: 'Abc123' }),
    'Host=127.0.0.1;Port=32768;Database=postgres;Username=fisico_ab12;Password=Abc123;Timeout=5',
  );
});

test('la cadena rechaza el puerto 5432, puertos fuera de rango y credenciales con separadores', () => {
  for (const puerto of [5432, 80, 70000, Number.NaN]) {
    assert.throws(() => cadenaAdministrativa({ puerto, usuario: 'u', clave: 'c' }));
  }
  for (const clave of ['con;punto', 'con espacio', "con'comilla", '']) {
    assert.throws(() => cadenaAdministrativa({ puerto: 40000, usuario: 'u', clave }));
  }
});

test('la credencial es alfanumérica y de la longitud pedida', () => {
  for (let i = 0; i < 50; i++) assert.match(credencial(32), /^[A-Za-z0-9]{32}$/);
});

test('solo se reconoce como propio el nombre exacto', () => {
  assert.equal(nombreValido('sillar_verify_fisico_1793690811215_2251823'), true);
  assert.equal(idDeNombre('sillar_verify_fisico_1793690811215_2251823'), '1793690811215_2251823');
  for (const otro of ['sillar_db', 'sillar_verify_1793690811215_2251823', 'sillar_verify_fisico_x_1', 'sillar_verify_fisico_1_2_3']) {
    assert.equal(nombreValido(otro), false, otro);
    assert.equal(idDeNombre(otro), null, otro);
  }
});

test('la identidad se escribe sin contraseña y en el formato de LeerIdentidad', () => {
  const texto = textoDeIdentidad({ contenedor: 'a'.repeat(64), systemIdentifier: '7703843376221958183', clusterName: 'verify_fisico_1', puerto: 32768 });
  assert.equal(texto, `contenedor=${'a'.repeat(64)}\nsystem_identifier=7703843376221958183\ncluster_name=verify_fisico_1\npuerto=32768\n`);
  assert.doesNotMatch(texto, /password|clave/i);
  assert.throws(() => textoDeIdentidad({ contenedor: 'x', systemIdentifier: '1', clusterName: 'c', puerto: 1 }));
  assert.throws(() => textoDeIdentidad({ contenedor: 'a'.repeat(64), systemIdentifier: '12a', clusterName: 'c', puerto: 1 }));
});
