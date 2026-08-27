import { spawn } from 'node:child_process';

interface RunOptions {
  cwd?: string;
  env?: NodeJS.ProcessEnv;
}

/**
 * Construye el entorno de un proceso hijo preservando las variables custom
 * también cuando WSL lanza un ejecutable de Windows (por ejemplo dotnet.exe).
 *
 * WSL solo propaga de forma fiable esas variables al lado Windows cuando sus
 * nombres están declarados en WSLENV. En Linux/macOS nativos WSLENV no tiene
 * efecto, así que esta preparación es inocua fuera de WSL.
 */
function childEnv(extra?: NodeJS.ProcessEnv): NodeJS.ProcessEnv {
  if (!extra) {
    return process.env;
  }

  const env = { ...process.env, ...extra };
  const keys = Object.keys(extra);

  if (keys.length === 0) {
    return env;
  }

  const existing = (env.WSLENV ?? '').split(':').filter((entry) => entry.length > 0);
  const existingNames = new Set(existing.map((entry) => entry.split('/')[0]));
  const additions = keys.filter((key) => !existingNames.has(key));

  if (additions.length > 0) {
    env.WSLENV = [...existing, ...additions].join(':');
  }

  return env;
}

/** Ejecuta un comando y muestra su salida en vivo. Lanza si el código de salida no es 0. */
export function run(command: string, args: string[], options: RunOptions = {}): Promise<void> {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, {
      cwd: options.cwd,
      env: childEnv(options.env),
      stdio: 'inherit',
      shell: false,
    });

    child.on('error', reject);
    child.on('close', (code) => {
      if (code === 0) {
        resolve();
      } else {
        reject(new Error(`${command} ${args.join(' ')} salió con código ${code}`));
      }
    });
  });
}

/** Igual que {@link run}, pero devuelve la salida en vez de mostrarla. Para leer un valor, no para vigilar un proceso. */
export function runCapture(command: string, args: string[], options: RunOptions = {}): Promise<string> {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, {
      cwd: options.cwd,
      env: childEnv(options.env),
      shell: false,
    });

    let output = '';
    child.stdout.on('data', (chunk) => (output += chunk.toString()));
    child.stderr.on('data', (chunk) => (output += chunk.toString()));
    child.on('error', reject);
    child.on('close', (code) => {
      if (code === 0) {
        resolve(output.trim());
      } else {
        reject(new Error(`${command} ${args.join(' ')} salió con código ${code}: ${output}`));
      }
    });
  });
}

export function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
