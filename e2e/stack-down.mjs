#!/usr/bin/env node
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { identidadDeLaWorktree } from '../scripts/identidad.mjs';

/**
 * Baja el stack e2e **de este árbol**, con su volumen.
 *
 *     pnpm stack:down
 *
 * **Por qué es un guion y no una línea en `package.json`.** Ahí ponía
 * `docker compose -p sillar_e2e …`, con el nombre de proyecto escrito a mano,
 * y un `package.json` no puede derivar nada. Después de que la identidad pase a
 * calcularse, esa línea no bajaba el stack de este árbol: bajaba uno que no
 * existe, o —peor— el de otra worktree que todavía se llamara así.
 *
 * **Y aquí la derivación quita una guarda en vez de necesitarla.** `composeDown()`
 * comprueba de quién es el stack antes de destruirlo, porque un nombre de
 * proyecto compartido permitía apuntar al vecino. Esto no lo comprueba y no le
 * hace falta: solo sabe construir el nombre de SU árbol, así que no tiene forma
 * de nombrar el de otro. Una identidad derivada convierte una barrera en una
 * imposibilidad, que es mejor.
 */

const AQUI = path.dirname(fileURLToPath(import.meta.url));
const RAIZ = path.resolve(AQUI, '..');
const { e2e } = identidadDeLaWorktree(RAIZ);

const r = spawnSync(
  'docker',
  ['compose', '-p', e2e.proyecto, '--env-file', path.join(AQUI, '.env.e2e'),
   '-f', path.join(RAIZ, 'docker-compose.yml'), 'down', '-v'],
  {
    cwd: RAIZ,
    stdio: 'inherit',
    env: {
      ...process.env,
      COMPOSE_PROJECT_NAME: e2e.proyecto,
      POSTGRES_DB: e2e.base,
      POSTGRES_PORT: String(e2e.puertoDb),
      API_PORT: String(e2e.puertoApi),
      FRONTEND_PORT: String(e2e.puertoFrontend),
    },
  },
);

process.exit(r.status ?? 1);
