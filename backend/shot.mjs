import { chromium } from '/home/samu/.nvm/versions/node/v22.18.0/lib/node_modules/playwright-core/index.js';
const url = "http://localhost:8080/realms/umbral/protocol/openid-connect/auth?client_id=umbral-web&response_type=code&scope=openid&redirect_uri=http://localhost:3000/callback&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256";
const b = await chromium.launch();
const p = await b.newPage({ viewport: { width: 1100, height: 900 }, deviceScaleFactor: 2 });
await p.goto(url, { waitUntil: "networkidle" });
await p.screenshot({ path: process.argv[2], fullPage: true });
await b.close();
console.log("ok");
