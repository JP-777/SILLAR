import { MAILPIT_URL } from './env.js';
import { sleep } from './shell.js';

interface MailpitSummary {
  ID?: string;
  Subject?: string;
}

interface MailpitList {
  messages?: MailpitSummary[];
}

export interface MailpitMessage {
  ID?: string;
  Subject?: string;
  Text?: string;
}

export async function waitMailpitReady(timeoutMs = 30_000): Promise<void> {
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    try {
      const response = await fetch(`${MAILPIT_URL}/api/v1/messages?limit=1`);

      if (response.ok) {
        return;
      }
    } catch {
      // El contenedor todavía está arrancando.
    }

    await sleep(250);
  }

  throw new Error(
    `Mailpit no respondió en ${MAILPIT_URL} dentro de ${timeoutMs / 1000} s.`,
  );
}

export async function waitMailpitMessage(
  recipient: string,
  subject: string,
  timeoutMs = 15_000,
): Promise<MailpitMessage> {
  const deadline = Date.now() + timeoutMs;
  const query = encodeURIComponent(`to:"${recipient}"`);

  while (Date.now() < deadline) {
    const response = await fetch(
      `${MAILPIT_URL}/api/v1/search?query=${query}`,
    );

    if (!response.ok) {
      throw new Error(
        `Mailpit devolvió ${response.status} al buscar el correo.`,
      );
    }

    const list = (await response.json()) as MailpitList;

    const summary = list.messages?.find(
      (message) => message.Subject === subject && message.ID,
    );

    if (summary?.ID) {
      const messageResponse = await fetch(
        `${MAILPIT_URL}/api/v1/message/${encodeURIComponent(summary.ID)}`,
      );

      if (!messageResponse.ok) {
        throw new Error(
          `Mailpit devolvió ${messageResponse.status} al leer ${summary.ID}.`,
        );
      }

      return (await messageResponse.json()) as MailpitMessage;
    }

    await sleep(250);
  }

  throw new Error(
    `Mailpit no recibió '${subject}' para '${recipient}' en ${timeoutMs / 1000} s.`,
  );
}
