const { chromium } = require("C:\\Users\\dikim\\AppData\\Roaming\\npm\\node_modules\\playwright-core");
(async () => {
  const b = await chromium.launch({ channel: "chrome" });
  const p = await b.newPage();
  await p.setViewportSize({ width: 430, height: 780 });
  await p.goto("http://localhost:3000/wireframe.html", { waitUntil: "networkidle" });

  const shots = [
    { y: 0,    name: "C:\\\\Users\\\\dikim\\\\Desktop\\\\formax-wireframes\\\\1-card-comparison.png" },
    { y: 920,  name: "C:\\\\Users\\\\dikim\\\\Desktop\\\\formax-wireframes\\\\2-header-comparison.png" },
    { y: 1750, name: "C:\\\\Users\\\\dikim\\\\Desktop\\\\formax-wireframes\\\\3-systems.png" },
  ];
  for (const s of shots) {
    await p.evaluate(y => window.scrollTo(0, y), s.y);
    await new Promise(r => setTimeout(r, 400));
    await p.screenshot({ path: s.name });
    console.log("done " + s.name);
  }
  await b.close();
})();
