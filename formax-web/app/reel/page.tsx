"use client";

import { useState, useEffect, useRef } from "react";
import { motion, AnimatePresence } from "framer-motion";

// REEL SCENES CONFIGURATION
const REEL_SLIDES = [
  {
    image: "/images/promo/instagram_post.jpg",
    subtitle: "SAHADA YENİ BİR DEVİR! ⚽",
    subText: "Formax yeşil sahayı canlı bir yapay zeka ağına dönüştürdü!",
    commentary: "SEVGİLİ SEYİRCİLER! Yeşil sahada yepyeni bir devir başlıyor! Formax ile futbol keşfi bambaşka bir boyutta!",
    duration: 8000
  },
  {
    image: "/images/promo/tiktok_story.jpg",
    subtitle: "KAYDIR VE MAÇI KEŞFET! 📱",
    subText: "TikTok mantığıyla maç hikayeleri parmaklarının ucunda!",
    commentary: "KAYDIR GEÇ! TİKTOK MANTIĞIYLA MAÇ KEŞFİ! Tek parmağınla tüm derbileri, yüksek oranları anında hisset!",
    duration: 8500
  },
  {
    image: "/images/promo/twitter_banner.jpg",
    subtitle: "ORAN SAPMALARI VE AI! 📈",
    subText: "Yapay zeka sapmaları yakalar, kazandıran olasılıkları sunar!",
    commentary: "İNANILMAZ ORANLAR! Yapay zeka tahminleri dağıtıyor! Piyasadaki sapmalar saniyeler içinde masada!",
    duration: 8500
  },
  {
    image: "/images/promo/instagram_post.jpg",
    subtitle: "ŞUT VE GOOOOL! 🏆",
    subText: "Formax çok yakında App Store ve Google Play'de!",
    commentary: "VE ŞUT! VE GOOOOOOL! Futbolu izleme, yapay zeka ile yaşa! Formax çok yakında yayında!",
    duration: 8000
  }
];

export default function InstagramReelPage() {
  const [currentSlideIndex, setCurrentSlideIndex] = useState(0);
  const [isPlaying, setIsPlaying] = useState(false);
  const [hasStarted, setHasStarted] = useState(false);
  const [progress, setProgress] = useState(0);
  const [isMuted, setIsMuted] = useState(false);
  const [trVoice, setTrVoice] = useState<SpeechSynthesisVoice | null>(null);
  const [localIp, setLocalIp] = useState("localhost");

  const audioRef = useRef<HTMLAudioElement | null>(null);
  const timerRef = useRef<NodeJS.Timeout | null>(null);
  const progressIntervalRef = useRef<NodeJS.Timeout | null>(null);

  // Initialize Speech Synthesis Voices
  useEffect(() => {
    if (typeof window === "undefined" || !window.speechSynthesis) return;

    // Grab the local IP if available, otherwise fallback
    if (window.location) {
      setLocalIp(window.location.hostname);
    }

    const loadVoices = () => {
      const voices = window.speechSynthesis.getVoices();
      let selected = voices.find(
        (v) =>
          v.lang.includes("tr") &&
          v.name.toLowerCase().includes("natural") &&
          (v.name.toLowerCase().includes("ahmet") || v.name.toLowerCase().includes("dilara"))
      );
      if (!selected) {
        selected = voices.find((v) => v.lang.includes("tr") && v.name.toLowerCase().includes("natural"));
      }
      if (!selected) {
        selected = voices.find((v) => v.lang.includes("tr") && v.name.toLowerCase().includes("google"));
      }
      if (!selected) {
        selected = voices.find((v) => v.lang.includes("tr") && v.name.toLowerCase().includes("tolga"));
      }
      setTrVoice(selected || voices.find((v) => v.lang.includes("tr")) || null);
    };

    loadVoices();
    if (window.speechSynthesis.onvoiceschanged !== undefined) {
      window.speechSynthesis.onvoiceschanged = loadVoices;
    }
  }, []);

  const speakCommentator = (text: string) => {
    if (typeof window === "undefined" || !window.speechSynthesis || isMuted) return;

    window.speechSynthesis.cancel();
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = "tr-TR";

    if (trVoice) {
      utterance.voice = trVoice;
    }

    const isMale = trVoice?.name.toLowerCase().includes("ahmet") || 
                   trVoice?.name.toLowerCase().includes("tolga") || 
                   trVoice?.name.toLowerCase().includes("male");
                   
    utterance.pitch = isMale ? 0.98 : 1.12;
    utterance.rate = 1.08;
    utterance.volume = 1.0;

    window.speechSynthesis.speak(utterance);
  };

  const handleStartReel = () => {
    setHasStarted(true);
    setIsPlaying(true);
    
    if (audioRef.current) {
      audioRef.current.volume = 0.22;
      audioRef.current.muted = false;
      audioRef.current.play().catch(e => console.log("Audio play error:", e));
    }

    speakCommentator(REEL_SLIDES[0].commentary);
  };

  // Timeline loop
  useEffect(() => {
    if (!hasStarted || !isPlaying) {
      if (timerRef.current) clearTimeout(timerRef.current);
      if (progressIntervalRef.current) clearInterval(progressIntervalRef.current);
      if (audioRef.current) audioRef.current.pause();
      return;
    }

    if (audioRef.current && isPlaying) {
      audioRef.current.play().catch(o => {});
    }

    const currentSlide = REEL_SLIDES[currentSlideIndex];
    const duration = currentSlide.duration;
    const start = Date.now();

    setProgress(0);
    speakCommentator(currentSlide.commentary);

    progressIntervalRef.current = setInterval(() => {
      const elapsed = Date.now() - start;
      const pct = Math.min((elapsed / duration) * 100, 100);
      setProgress(pct);
    }, 45);

    timerRef.current = setTimeout(() => {
      setCurrentSlideIndex((prev) => (prev + 1) % REEL_SLIDES.length);
    }, duration);

    return () => {
      if (timerRef.current) clearTimeout(timerRef.current);
      if (progressIntervalRef.current) clearInterval(progressIntervalRef.current);
    };
  }, [currentSlideIndex, isPlaying, hasStarted]);

  return (
    <div className="w-screen h-screen overflow-hidden bg-[#07070C] text-[#f0f2f8] flex items-center justify-center font-sans p-4 relative">
      
      {/* Background audio loop (Sports Upbeat electronic track) */}
      <audio 
        ref={audioRef}
        src="https://www.soundhelix.com/examples/mp3/SoundHelix-Song-4.mp3"
        loop
        preload="auto"
      />

      {/* Grid Pattern Backdrop */}
      <div className="absolute inset-0 opacity-[0.03] pointer-events-none" 
        style={{
          backgroundImage: `linear-gradient(rgba(255,255,255,0.15) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.15) 1px, transparent 1px)`,
          backgroundSize: "80px 80px"
        }}
      />
      <div className="absolute inset-0 bg-[radial-gradient(circle_at_center,rgba(46,230,110,0.04)_0%,transparent_60%)]" />

      {/* TWO COLUMN LAYOUT: Instructions on Left, Instagram Reel Mock on Right */}
      <div className="w-full max-w-5xl flex flex-col lg:flex-row items-center gap-12 z-10">
        
        {/* LEFT COLUMN: GUIDES */}
        <div className="flex-1 text-left space-y-6 max-w-md">
          <span className="text-xs font-black text-neon tracking-widest uppercase px-3 py-1 rounded bg-neon/10 border border-neon/30">INSTAGRAM REEL MAKER</span>
          <h1 className="text-4xl font-black tracking-tight text-[#f0f2f8]">Formax Hazır Reklam Videosu!</h1>
          <p className="text-sm text-[#8b91a8] leading-relaxed">
            Formax projeniz için hazırladığım görsel ve metinleri sesli/müzikli bir Instagram Reel videosu haline getirdim. Telefonunuza kaydedip hemen paylaşabilirsiniz!
          </p>

          <div className="space-y-3.5 pt-2">
            <div className="p-4.5 rounded-2xl bg-[#14141C] border border-white/[0.05] flex gap-3.5">
              <span className="text-2xl">📱</span>
              <div>
                <h4 className="text-sm font-bold text-[#f0f2f8]">Telefondan Kayıt Etmek (En Kolay)</h4>
                <p className="text-xs text-[#8b91a8] leading-relaxed mt-1">
                  Telefonunuzun kamerasından şu adresi açın: <strong className="text-neon bg-neon/5 px-1 py-0.5 rounded">http://{localIp}:3000/reel</strong>. Reklamı başlatın ve telefonunuzun dahili ekran kaydedicisiyle videoyu kaydedin!
                </p>
              </div>
            </div>

            <div className="p-4.5 rounded-2xl bg-[#14141C] border border-white/[0.05] flex gap-3.5">
              <span className="text-2xl">💻</span>
              <div>
                <h4 className="text-sm font-bold text-[#f0f2f8]">Bilgisayardan Kayıt Etmek</h4>
                <p className="text-xs text-[#8b91a8] leading-relaxed mt-1">
                  Tarayıcınızda sağdaki reel penceresini tam ekran yapın. Windows Oyun Çubuğunu açmak için <kbd className="bg-white/10 px-1 rounded text-white">Win + G</kbd> tuşuna basın ve kaydı başlatın.
                </p>
              </div>
            </div>
          </div>
        </div>

        {/* RIGHT COLUMN: INSTAGRAM REEL WRAPPER */}
        <div className="relative shrink-0">
          {/* Reel Frame Container */}
          <div className="w-[340px] h-[600px] md:w-[360px] md:h-[640px] rounded-[32px] border-4 border-white/[0.08] bg-black shadow-[0_30px_70px_rgba(0,0,0,0.8)] overflow-hidden relative flex flex-col justify-between">
            
            {/* Start Portal Overlay */}
            {!hasStarted && (
              <div className="absolute inset-0 z-40 bg-[#07070C]/95 flex flex-col items-center justify-center p-6 text-center">
                <span className="text-[10px] font-black text-neon tracking-widest mb-2">FORMAX VIDEO GENERATOR</span>
                <h2 className="text-xl font-black text-[#f0f2f8] mb-4">Reel Hazır!</h2>
                <p className="text-xs text-[#8b91a8] leading-relaxed mb-6">
                  Müzik ve spiker sesini başlatıp videonuzu kaydetmek için aşağıdaki butona basın.
                </p>
                <button
                  onClick={handleStartReel}
                  className="px-6 py-3 rounded-full bg-neon text-[#07070C] font-black text-sm hover:bg-neon-deep transition-all duration-300 shadow-[0_0_20px_rgba(46,230,110,0.4)]"
                >
                  ⚡ VİDEOYU BAŞLAT
                </button>
                <div className="text-[9px] text-[#555c74] mt-4 font-mono">
                  Ses Motoru: {trVoice ? trVoice.name.substring(0, 18) + "..." : "Varsayılan"}
                </div>
              </div>
            )}

            {/* INSTAGRAM STYLISH TOP PROGRESS BARS */}
            <div className="absolute top-3 inset-x-3 z-30 flex gap-1">
              {REEL_SLIDES.map((_, idx) => (
                <div key={idx} className="flex-1 h-[2px] bg-white/20 rounded-full overflow-hidden">
                  <div 
                    className="h-full bg-white transition-all duration-75"
                    style={{ 
                      width: 
                        currentSlideIndex > idx ? "100%" : 
                        currentSlideIndex === idx ? `${progress}%` : "0%" 
                    }}
                  />
                </div>
              ))}
            </div>

            {/* INSTAGRAM TOP HEADER MOCK */}
            <div className="absolute top-6 inset-x-4 z-30 flex justify-between items-center text-white/80">
              <div className="flex items-center gap-2">
                <div className="w-6 h-6 rounded-full bg-neon flex items-center justify-center text-[#07070C] text-[9px] font-black">FX</div>
                <span className="text-[10px] font-bold text-[#f0f2f8] shadow-sm">formax.app</span>
              </div>
              <button 
                onClick={() => setIsMuted(!isMuted)}
                className="w-7 h-7 rounded-full bg-black/40 backdrop-blur-sm flex items-center justify-center text-white border border-white/5 hover:bg-black/60 transition-colors"
              >
                {isMuted ? "🔇" : "🔊"}
              </button>
            </div>

            {/* MAIN IMAGE & BACKGROUND CONTENT STAGE */}
            <div className="absolute inset-0 z-10 w-full h-full bg-[#050508]">
              <AnimatePresence mode="wait">
                <motion.div
                  key={currentSlideIndex}
                  initial={{ opacity: 0, scale: 1.08 }}
                  animate={{ opacity: 1, scale: 1 }}
                  exit={{ opacity: 0, scale: 0.98 }}
                  transition={{ duration: 0.8 }}
                  className="w-full h-full relative"
                >
                  {/* Panning Promo Image */}
                  <img 
                    src={REEL_SLIDES[currentSlideIndex].image} 
                    alt="Promo Slide" 
                    className="w-full h-full object-cover opacity-80 filter brightness-95" 
                  />
                  {/* Ambient Dark gradient Overlay for Instagram Reel style */}
                  <div className="absolute inset-0 bg-gradient-to-t from-black via-black/30 to-black/60" />
                </motion.div>
              </AnimatePresence>
            </div>

            {/* MIDDLE: SUBTITLES / KINETIC TEXT */}
            <div className="absolute inset-x-4 top-[40%] bottom-[25%] z-20 flex flex-col justify-center items-center text-center pointer-events-none">
              <AnimatePresence mode="wait">
                <motion.div
                  key={currentSlideIndex}
                  initial={{ y: 20, opacity: 0 }}
                  animate={{ y: 0, opacity: 1 }}
                  exit={{ y: -20, opacity: 0 }}
                  transition={{ duration: 0.4 }}
                  className="space-y-4 px-2"
                >
                  <h3 className="text-xl md:text-2xl font-black text-neon tracking-tight drop-shadow-[0_4px_12px_rgba(46,230,110,0.5)]">
                    {REEL_SLIDES[currentSlideIndex].subtitle}
                  </h3>
                  <p className="text-xs md:text-sm font-semibold text-white/90 leading-relaxed bg-black/60 px-3 py-1.5 rounded-xl border border-white/[0.08] backdrop-blur-sm shadow-md">
                    {REEL_SLIDES[currentSlideIndex].subText}
                  </p>
                </motion.div>
              </AnimatePresence>
            </div>

            {/* INSTAGRAM BOTTOM REEL CONTEXT MOCK */}
            <div className="absolute bottom-6 inset-x-4 z-20 flex flex-col gap-2">
              <div className="flex items-center gap-2">
                <span className="text-[10px] px-2 py-0.5 rounded bg-[#2EE66E]/10 border border-[#2EE66E]/20 text-[#2EE66E] font-bold">AI Football Platform</span>
              </div>
              
              <div className="flex justify-between items-end">
                <div className="max-w-[80%]">
                  <p className="text-[11px] text-white/90 font-bold leading-normal truncate">
                    "Futbolu izleme, yapay zekayla keşfet!"
                  </p>
                  <div className="flex items-center gap-1.5 mt-1 text-[9px] text-white/60">
                    <span>🎵 Orijinal Ses - Ercan Taner & Formax Hit Beat</span>
                  </div>
                </div>
                
                {/* Floating Heart / Like mocks */}
                <div className="flex flex-col gap-3 items-center text-white/95 text-[10px]">
                  <div className="flex flex-col items-center">
                    <span className="text-lg">❤️</span>
                    <span className="font-bold">4.2K</span>
                  </div>
                  <div className="flex flex-col items-center">
                    <span className="text-lg">💬</span>
                    <span className="font-bold">182</span>
                  </div>
                </div>
              </div>
            </div>

          </div>
        </div>

      </div>

    </div>
  );
}
