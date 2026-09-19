function valoresDeClave(texto, clave) {
  const valores = [];

  for (const rawLine of texto.split(/\r?\n/)) {
    const line = rawLine.trim();

    if (line.length === 0 || line.startsWith('#')) {
      continue;
    }

    const separator = line.indexOf('=');
    if (separator <= 0) {
      continue;
    }

    if (line.slice(0, separator).trim() !== clave) {
      continue;
    }

    let value = line.slice(separator + 1).trim();

    if (
      value.length >= 2 &&
      (value[0] === '"' || value[0] === "'") &&
      value[value.length - 1] === value[0]
    ) {
      value = value.slice(1, -1);
    }

    valores.push(value);
  }

  return valores;
}

function valorUnico(texto, clave) {
  const valores = valoresDeClave(texto, clave);

  if (valores.length !== 1) {
    throw new Error(
      `.env.example debe definir exactamente una vez ${clave}.`,
    );
  }

  return valores[0];
}

function passwordsDeCadena(cadena) {
  const encontrados = [];

  for (const componente of cadena.split(';')) {
    const separator = componente.indexOf('=');

    if (separator <= 0) {
      continue;
    }

    const clave = componente.slice(0, separator).trim();

    if (clave.toLowerCase() !== 'password') {
      continue;
    }

    encontrados.push(componente.slice(separator + 1).trim());
  }

  return encontrados;
}

export function contrasenaPostgresCoherente(ejemplo) {
  const postgres = valorUnico(ejemplo, 'POSTGRES_PASSWORD');
  const cadena = valorUnico(ejemplo, 'ConnectionStrings__Default');
  const passwords = passwordsDeCadena(cadena);

  if (passwords.length !== 1) {
    throw new Error(
      '.env.example debe contener exactamente un componente Password= en ConnectionStrings__Default.',
    );
  }

  if (postgres !== passwords[0]) {
    throw new Error(
      '.env.example es incoherente: POSTGRES_PASSWORD y Password= de ConnectionStrings__Default no coinciden.',
    );
  }

  return postgres;
}
