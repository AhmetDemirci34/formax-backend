import { chromium } from 'playwright';
import { setTimeout as sleep } from 'timers/promises';

const BASE = 'http://localhost:3000';
const OUT  = 'C:\\Users\\dikim\\Desktop\\formax-screenshots';

const SECTIONS = [
  { id: 'home-feed', name: '1-home-feed',   scrollY: 0      },
  { id: 'scheduled', name: '2-scheduled',   scrollY: null   },
  { id: 'live',      name: '3-live',        scrollY: null   },
  { id: 'finished',  name: '4-finished',    scrollY: null   },
];

const browser = await chromium.launch({ channel: 'chrome', headless: true });
const ctx = await browser.newContext({ viewport: { width: 1280, height: 900 } });
const page = await ctx.newPage();

// Load the full demo page first
await page.goto(`${BASE}/demo`, { waitUntil: 'networkidle', timeout: 20000 });
await sleep(1500);

// Create output dir
import { mkdirSync } from 'fs';
mkdirSync(OUT, { recursive: true });

// Full overview (viewport)
await page.screenshot({ path: `${OUT}\\0-overview.png` });
console.log('✓ 0-overview.png');

// Each section — scroll to it and screenshot the phone frame
for (const s of SECTIONS) {
  // Scroll to section
  await page.evaluate((id) => {
    document.getElementById(id)?.scrollIntoView({ behavior: 'instant', block: 'start' });
  }, s.id);
  await sleep(600);

  // Full viewport shot at this scroll position
  await page.screenshot({ path: `${OUT}\\${s.name}.png` });
  console.log(`✓ ${s.name}.png`);

  // Now find and screenshot just the phone frame within the section
  const phoneEl = await page.$(`#${s.id} .overflow-y-auto`);
  if (phoneEl) {
    const box = await phoneEl.boundingBox();
    if (box) {
      // screenshot the parent phone frame (go up to the rounded container)
      const frameEl = await page.$(`#${s.id} .rounded-\\[2rem\\]`);
      if (frameEl) {
        await frameEl.screenshot({ path: `${OUT}\\${s.name}-phone.png` });
        console.log(`✓ ${s.name}-phone.png`);
      }
    }
  }
}

await browser.close();
console.log(`\nAll screenshots saved to ${OUT}`);
