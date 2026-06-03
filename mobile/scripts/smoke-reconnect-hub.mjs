import { writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const signalR = require('@microsoft/signalr');

const {
  HubConnectionBuilder,
  HttpTransportType,
  LogLevel,
} = signalR;

function requiredEnv(name) {
  const value = process.env[name]?.trim();
  if (!value) {
    throw new Error(`Missing required environment variable: ${name}`);
  }
  return value;
}

function parseTransport(value) {
  const normalized = (value ?? 'websockets').trim().toLowerCase();
  switch (normalized) {
    case 'websockets':
    case 'websocket':
    case 'ws':
      return {
        label: 'WebSockets',
        value: HttpTransportType.WebSockets,
      };
    case 'longpolling':
    case 'long-polling':
    case 'lp':
      return {
        label: 'LongPolling',
        value: HttpTransportType.LongPolling,
      };
    default:
      throw new Error(
        "TRANSPORT must be one of: websockets, long-polling.",
      );
  }
}

function toHubUrl(gatewayBaseUrl) {
  const normalized = gatewayBaseUrl.endsWith('/')
    ? gatewayBaseUrl
    : `${gatewayBaseUrl}/`;
  return new URL('hubs/sessions', normalized).toString();
}

function toErrorPayload(error) {
  if (!(error instanceof Error)) {
    return { name: 'UnknownError', message: String(error) };
  }

  return {
    name: error.name,
    message: error.message,
  };
}

function usage() {
  return [
    'Usage:',
    '  GW=http://localhost:8000 TOKEN=... LIVE_SESSION_ID=... TEAM_ID=... \\',
    '  DISPLAY_NAME=participant npm run smoke:reconnect:hub',
    '',
    'Optional environment variables:',
    '  JOIN_TOKEN        reconnect/join token; defaults to null',
    '  TRANSPORT         websockets (default) | long-polling',
    '  TRANSCRIPT_FILE   write the JSON transcript to a file',
  ].join('\n');
}

async function main() {
  if (process.argv.includes('--help')) {
    console.log(usage());
    return;
  }

  const gatewayBaseUrl = requiredEnv('GW');
  const accessToken = requiredEnv('TOKEN');
  const liveSessionId = requiredEnv('LIVE_SESSION_ID');
  const teamId = requiredEnv('TEAM_ID');
  const displayName = requiredEnv('DISPLAY_NAME');
  const joinToken = process.env.JOIN_TOKEN?.trim() || null;
  const transcriptFile = process.env.TRANSCRIPT_FILE?.trim() || null;
  const transport = parseTransport(process.env.TRANSPORT);
  const hubUrl = toHubUrl(gatewayBaseUrl);
  const transcript = {
    hubUrl,
    liveSessionId,
    teamId,
    displayName,
    tokenProvided: joinToken !== null,
    transport: transport.label,
    startedAt: new Date().toISOString(),
  };

  const connection = new HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: () => accessToken,
      transport: transport.value,
    })
    .configureLogging(LogLevel.Warning)
    .build();

  try {
    await connection.start();
    transcript.connectionState = 'Connected';

    const result = await connection.invoke(
      'ReconnectAsync',
      liveSessionId,
      {
        teamId,
        displayName,
        token: joinToken,
      },
    );

    transcript.outcome = 'success';
    transcript.result = result;
    transcript.finishedAt = new Date().toISOString();
    console.log(JSON.stringify(transcript, null, 2));
  } catch (error) {
    transcript.outcome = 'error';
    transcript.error = toErrorPayload(error);
    transcript.finishedAt = new Date().toISOString();
    console.log(JSON.stringify(transcript, null, 2));
    process.exitCode = 1;
  } finally {
    try {
      await connection.stop();
    } catch {
      // Ignore stop failures during smoke runs.
    }

    if (transcriptFile) {
      await writeFile(transcriptFile, `${JSON.stringify(transcript, null, 2)}\n`);
    }
  }
}

await main();
