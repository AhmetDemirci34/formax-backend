import { chromium } from "playwright";
const b = await chromium.launch({ channel: "chrome" });
const p = await b.newPage();
await p.setViewportSize({ width: 430, height: 780 });
await p.goto("http://localhost:3000/wireframe.html", { waitUntil: "networkidle" });

const shots = [
  { y: 0,    name: "C:\\Users\\dikim\\Desktop\\formax-wireframes\\1-card-v1-v2.png"   },
  { y: 900,  name: "C:\\Users\\dikim\\Desktop\\formax-wireframes\\2-header-v1-v2.png" },
  { y: 1700, name: "C:\\Users\\dikim\\Desktop\\formax-wireframes\\3-badge-countdown.png" },
];
for (const s of shots) {
  await p.evaluate(y => window.scrollTo(0, y), s.y);
  await new Promise(r => setTimeout(r, 300));
  await p.screenshot({ path: s.name });
  console.log("✓ " + s.name);
}
await b.close();
