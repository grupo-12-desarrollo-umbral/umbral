#!/usr/bin/env node
import { spawn, spawnSync } from 'node:child_process';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import os from 'node:os';
import process from 'node:process';

const VIRTUAL_ADAPTER_PATTERN =
  /docker|br-|veth|virbr|wsl|hyper-v|vethernet|tailscale|windscribe|vpn|openvpn|tap|tun|loopback|virtualbox|vmware/i;

function isUsableIpv4(ip) {
  if (!/^\d+\.\d+\.\d+\.\d+$/.test(ip)) return false;
  if (ip.startsWith('127.') || ip.startsWith('169.254.')) return false;
  return true;
}

function parseWindowsIpconfig(output) {
  const candidates = [];
  let adapter = null;

  function pushAdapter() {
    if (!adapter) return;
    if (adapter.ipv4 && adapter.hasGateway && !adapter.disconnected && !adapter.skip) {
      candidates.push(adapter.ipv4);
    }
  }

  for (const line of output.split(/\r?\n/)) {
    const header = line.match(/adapter (.+):$/i);
    if (header) {
      pushAdapter();
      const name = header[1].trim();
      adapter = {
        name,
        ipv4: null,
        hasGateway: false,
        disconnected: false,
        skip: VIRTUAL_ADAPTER_PATTERN.test(name),
      };
      continue;
    }

    if (!adapter) continue;

    if (/Media disconnected/i.test(line)) {
      adapter.disconnected = true;
      continue;
    }

    const ipv4 = line.match(/IPv4 Address[^:]*:\s*([0-9.]+)/i);
    if (ipv4 && isUsableIpv4(ipv4[1])) {
      adapter.ipv4 = ipv4[1];
      continue;
    }

    const gateway = line.match(/Default Gateway[^:]*:\s*(.+)$/i);
    if (gateway && gateway[1].trim()) {
      adapter.hasGateway = true;
    }
  }

  pushAdapter();
  return candidates[0] ?? null;
}

function windowsLanHost() {
  const result = spawnSync('cmd.exe', ['/c', 'ipconfig'], {
    encoding: 'utf8',
    windowsHide: true,
  });

  if (result.error || result.status !== 0 || !result.stdout) return null;
  return parseWindowsIpconfig(result.stdout);
}

function unixLanHost() {
  let interfaces;
  try {
    interfaces = os.networkInterfaces();
  } catch {
    return null;
  }

  for (const [name, addresses] of Object.entries(interfaces)) {
    if (VIRTUAL_ADAPTER_PATTERN.test(name)) continue;

    for (const address of addresses ?? []) {
      if (address.family === 'IPv4' && !address.internal && isUsableIpv4(address.address)) {
        return address.address;
      }
    }
  }

  return null;
}

function resolveHost() {
  const override = process.env.REACT_NATIVE_PACKAGER_HOSTNAME;
  if (override) return { host: override, source: 'REACT_NATIVE_PACKAGER_HOSTNAME' };

  const fromWindows = windowsLanHost();
  if (fromWindows) return { host: fromWindows, source: 'Windows LAN adapter' };

  const fromUnix = unixLanHost();
  if (fromUnix) return { host: fromUnix, source: 'local network interface' };

  return null;
}

const cliArgs = process.argv.slice(2);
const printOnly = cliArgs.includes('--print-host');
const expoArgs = cliArgs.filter((arg) => arg !== '--print-host');
const host = resolveHost();

if (printOnly) {
  console.log(host?.host ?? '');
  process.exit(host ? 0 : 1);
}

if (host) {
  process.env.REACT_NATIVE_PACKAGER_HOSTNAME = host.host;
  console.log(`[expo-start] REACT_NATIVE_PACKAGER_HOSTNAME=${host.host} (${host.source})`);
} else {
  console.warn(
    '[expo-start] No LAN host detected. If Expo prints an unreachable 172.x address, rerun with REACT_NATIVE_PACKAGER_HOSTNAME=<your LAN IP> npm start.'
  );
}

const hasHostMode = expoArgs.some((arg, index) => {
  if (arg === '--lan' || arg === '--localhost' || arg === '--tunnel') return true;
  if (arg === '--host' && expoArgs[index + 1]) return true;
  return arg.startsWith('--host=');
});

if (!hasHostMode) {
  expoArgs.push('--host', 'lan');
}

const scriptDir = dirname(fileURLToPath(import.meta.url));
const expoBin = join(scriptDir, '..', 'node_modules', '.bin', process.platform === 'win32' ? 'expo.cmd' : 'expo');
const child = spawn(expoBin, ['start', ...expoArgs], {
  env: process.env,
  stdio: 'inherit',
});

child.on('exit', (code, signal) => {
  if (signal) {
    process.kill(process.pid, signal);
    return;
  }

  process.exit(code ?? 1);
});
