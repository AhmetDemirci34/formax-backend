'use strict';

const path = require('path');
const pLimit = require('p-limit');
const chalk = require('chalk');

const config = require('./config');
const { scrapeAll } = require('./src/scraper');
const { checkStream } = require('./src/checker');
const { generateKanallarJson } = require('./src/generator');

// kanallar.json → Bot/../kanallar.json (proje kökü)
const OUTPUT_DIR = path.resolve(__dirname, '..');

async function main() {
  const startTime = Date.now();

  console.log(chalk.bold.cyan('╔══════════════════════════════════════╗'));
  console.log(chalk.bold.cyan('║      KotakTV Otomasyon Botu v1.0     ║'));
  console.log(chalk.bold.cyan('╚══════════════════════════════════════╝\n'));

  // ── Adım 1: Kaynakları tara ──────────────────────────────────────────────
  console.log(chalk.bold.yellow('[ 1/3 ] IPTV kaynakları indiriliyor...\n'));
  const allChannels = await scrapeAll(config.sources);

  if (allChannels.length === 0) {
    console.error(chalk.red('\n✗ Hiç kanal bulunamadı. Kaynak URL\'leri kontrol edin.'));
    process.exit(1);
  }

  console.log(chalk.green(`\n  Toplam benzersiz stream: ${chalk.bold(allChannels.length)}\n`));

  // ── Adım 2: Her stream'i sağlık kontrolünden geçir ──────────────────────
  const { timeoutMs, concurrency, validateContent, retries } = config.checker;
  console.log(chalk.bold.yellow(
    `[ 2/3 ] Stream'ler kontrol ediliyor` +
    chalk.gray(` (eş zamanlı: ${concurrency}, timeout: ${timeoutMs / 1000}s)...\n`)
  ));

  const limit = pLimit(concurrency);
  let checked = 0;
  let alive = 0;

  const results = await Promise.all(
    allChannels.map(ch =>
      limit(async () => {
        const result = await checkStream(ch.url, { timeoutMs, validateContent, retries });

        checked++;
        if (result.isAlive) {
          alive++;
          process.stdout.write(chalk.green('●'));
        } else {
          process.stdout.write(chalk.red('·'));
        }

        if (checked % 60 === 0) {
          const pct = ((checked / allChannels.length) * 100).toFixed(0);
          process.stdout.write(chalk.gray(` ${pct}%\n`));
        }

        return { ...ch, ...result };
      })
    )
  );

  // Satır bitişini temizle
  process.stdout.write('\n\n');

  const aliveStreams = results.filter(r => r.isAlive);
  const deadCount = allChannels.length - aliveStreams.length;

  console.log(chalk.green(`  ✓ Canlı   : ${chalk.bold(aliveStreams.length)}`));
  console.log(chalk.red(`  ✗ Ölü     : ${chalk.bold(deadCount)}`));
  console.log(chalk.gray(`  Oran      : ${((aliveStreams.length / allChannels.length) * 100).toFixed(1)}% çalışıyor\n`));

  if (aliveStreams.length === 0) {
    console.error(chalk.red('✗ Hiç çalışan stream bulunamadı. Çıkılıyor.'));
    process.exit(1);
  }

  // ── Adım 3: kanallar.json üret ───────────────────────────────────────────
  console.log(chalk.bold.yellow('[ 3/3 ] kanallar.json üretiliyor...'));
  const output = generateKanallarJson(aliveStreams, OUTPUT_DIR);

  const elapsed = ((Date.now() - startTime) / 1000).toFixed(1);
  const outFile = path.join(OUTPUT_DIR, 'kanallar.json');

  console.log('');
  console.log(chalk.bold.green('╔══════════════════════════════════════╗'));
  console.log(chalk.bold.green('║            Bot Tamamlandı! ✓         ║'));
  console.log(chalk.bold.green('╚══════════════════════════════════════╝'));
  console.log(chalk.gray(`  Toplam kanal : ${output._meta.total_channels}`));
  console.log(chalk.gray(`  Çıktı        : ${outFile}`));
  console.log(chalk.gray(`  Süre         : ${elapsed}s\n`));
}

main().catch(err => {
  console.error(chalk.red('\n✗ Kritik hata:'), err.message);
  process.exit(1);
});
