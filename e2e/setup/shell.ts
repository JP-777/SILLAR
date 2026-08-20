import { spawn } from 'node:child_process';

interface RunOptions {
  cwd?: string;
  env?: NodeJS.ProcessEnv;
}

/** Ejecuta un comando y muestra su salida en vivo. Lanza si el código de salida no es 0. */
export function run(command: string, args: string[], options: RunOptions = {}): Promise<void> {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, {
      cwd: options.cwd,
      env: options.env ? { ...process.env, ...options.env } : process.env,
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
      env: options.env ? { ...process.env, ...options.env } : process.env,
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
