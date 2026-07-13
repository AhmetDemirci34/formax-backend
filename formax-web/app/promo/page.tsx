"use client";

import { useState, useEffect, useRef } from "react";
import { motion, AnimatePresence } from "framer-motion";

// SVGs and Icons
const SparklesIcon = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-neon">
    <path d="m12 3-1.912 5.813a2 2 0 0 1-1.275 1.275L3 12l5.813 1.912a2 2 0 0 1 1.275 1.275L12 21l1.912-5.813a2 2 0 0 1 1.275-1.275L21 12l-5.813-1.912a2 2 0 0 1-1.275-1.275L12 3Z"/>
    <path d="m5 3 1 2.5L8.5 6 6 7 5 9.5 4 7 1.5 6 4 5.5z"/>
  </svg>
);

const ShieldAlertIcon = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-signal-red">
    <path d="M20 13c0 5-3.5 7.5-7.66 9.7a1 1 0 0 1-.68 0C7.5 20.5 4 18 4 13V6a1 1 0 0 1 .76-.97l8-2a1 1 0 0 1 .48 0l8 2c.57.14.76.76.76 1.3V13Z"/>
    <path d="M12 8v4"/>
    <path d="M12 16h.01"/>
  </svg>
);

const ActivityIcon = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-signal-blue animate-pulse">
    <path d="M22 12h-4l-3 9L9 3l-3 9H2"/>
  </svg>
);

const CheckCircleIcon = () => (
  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="text-neon shrink-0">
    <path d="M20 6 9 17l-5-5"/>
  </svg>
);

const RadioIcon = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="text-signal-red animate-pulse">
    <circle cx="12" cy="12" r="10"/>
    <path d="M12 18a6 6 0 1 0 0-12 6 6 0 0 0 0 12Z"/>
    <circle cx="12" cy="12" r="2"/>
  </svg>
);

const FlameIcon = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-signal-amber animate-bounce">
    <path d="M8.5 14.5A2.5 2.5 0 0 0 11 12c0-1.38-.5-2-1-3-1.072-2.143-.224-4.054 2-6 .5 2.5 2 4.9 4 6.5 2 1.6 3 3.5 3 5.5a7 7 0 1 1-14 0c0-1.153.433-2.294 1-3a2.5 2.5 0 0 0 2.5 2.5z"/>
  </svg>
);

const VolumeIcon = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5"/>
    <path d="M15.54 8.46a5 5 0 0 1 0 7.07"/>
    <path d="M19.07 4.93a10 10 0 0 1 0 14.14"/>
  </svg>
);

const VolumeMuteIcon = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5"/>
    <line x1="22" y1="9" x2="16" y2="15"/>
    <line x1="16" y1="9" x2="22" y2="15"/>
  </svg>
);

const NewspaperIcon = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-signal-yellow">
    <path d="M4 22h16a2 2 0 0 0 2-2V4a2 2 0 0 0-2-2H8a2 2 0 0 0-2 2v16a2 2 0 0 1-2 2Zm0 0a2 2 0 0 1-2-2v-9c0-1.1.9-2 2-2h2"/>
    <path d="M18 14h-8"/>
    <path d="M15 18h-5"/>
    <path d="M10 6h8v4h-8V6Z"/>
  </svg>
);

// SCENE CONFIGURATION WITH commentator (Ercan Taner) SHOUTS AND TIMINGS
const SCENES = [
  { 
    id: "intro", 
    duration: 7500,
    title: "Ercan Taner'le Başlangıç",
    speech: "SEVGİLİ SEYİRCİLER! EKRAN BAŞINA! Futbol keşfinde yapay zeka devrimi başlıyor! Karşınızda Formax! Hazır mısınız bu çılgınlığa?"
  },
  { 
    id: "pitch-3d", 
    duration: 8500,
    title: "Zeka Sahada",
    speech: "İŞTE SAHA! MAÇIN TAM KALBİ! Formax yeşil zemini canlı bir zeka ağına dönüştürüyor! Taktikler, pas yolları, hepsi burada canlanıyor! MÜTHİŞ BİR ATILIM!"
  },
  { 
    id: "tiktok-swipe", 
    duration: 9000,
    title: "Tiktok Kaydırma Deneyimi",
    speech: "KAYDIR GEÇ! TİKTOK MANTIĞIYLA MAÇ KEŞFİ! Tek parmağınla tüm futbol dünyasını yönet! Sağa kaydır, sola kaydır, maçı anında hisset! İŞTE KULLANICI DOSTU ARAYÜZ!"
  },
  { 
    id: "interest-engine", 
    duration: 9000,
    title: "Kullanıcıyı Tanıyan Zeka",
    speech: "DURUN BİR DAKİKA! FORMAX SİZİ TANIR! Interest Engine devrede! Sevdiğin takımı, takip ettiğin ligi ezberler, sana özel maçı cımbızla çeker!"
  },
  { 
    id: "radar-engine", 
    duration: 8500,
    title: "Radar Sinyalleri",
    speech: "RADAR ENGINE AYAKTA SEVGİLİ SEYİRCİLER! Sakatlıklar, hava durumu, ani taktik sapmalar... Hiçbir şey kaçmıyor! Sinyaller anında cebinizde!"
  },
  { 
    id: "market-deviation", 
    duration: 9000,
    title: "Sapma ve Tahmin Oranları",
    speech: "YAPAY ZEKA TAHMİN ORANLARI DAĞITIYOR! Piyasayı sarsan sapmalar, akıllı olasılıklar! Formax kazanma ihtimalini saniyeler içinde masaya yatırıyor!"
  },
  { 
    id: "news-aggregator", 
    duration: 8500,
    title: "Haber Dedup ve Cluster",
    speech: "HABER LABİRENTİ SON BULDU! Küresel tüm manşetler yapay zeka tarafından süzülüyor, gruplanıyor ve tek bir temiz özete dönüşüyor!"
  },
  { 
    id: "outro", 
    duration: 8000,
    title: "Şut ve Gol!",
    speech: "FUTBOLU SADECE İZLEMEYİN, YAŞAYIN! YAPAY ZEKA İLE KEŞFEDİN! ŞUT VE GOOOOL! FORMAX ÇOK YAKINDA YAYINDA!"
  }
];

export default function PromoVideoPage() {
  const [currentSceneIndex, setCurrentSceneIndex] = useState(0);
  const [isPlaying, setIsPlaying] = useState(false);
  const [hasStarted, setHasStarted] = useState(false);
  const [cleanMode, setCleanMode] = useState(true);
  const [progress, setProgress] = useState(0);
  const [musicVolume, setMusicVolume] = useState(0.20);
  const [isMuted, setIsMuted] = useState(false);
  const [trVoice, setTrVoice] = useState<SpeechSynthesisVoice | null>(null);
  const [equalizerHeights, setEqualizerHeights] = useState<number[]>([10, 20, 15, 30, 25, 40, 10, 5]);

  const audioRef = useRef<HTMLAudioElement | null>(null);
  const timerRef = useRef<NodeJS.Timeout | null>(null);
  const progressIntervalRef = useRef<NodeJS.Timeout | null>(null);
  const eqIntervalRef = useRef<NodeJS.Timeout | null>(null);

  // Equalizer visual animation during voiceover speech
  useEffect(() => {
    if (typeof window === "undefined") return;

    if (isPlaying && hasStarted && !isMuted) {
      eqIntervalRef.current = setInterval(() => {
        setEqualizerHeights(
          Array.from({ length: 12 }, () => Math.floor(Math.random() * 38) + 8)
        );
      }, 100);
    } else {
      setEqualizerHeights(Array.from({ length: 12 }, () => 6));
      if (eqIntervalRef.current) clearInterval(eqIntervalRef.current);
    }

    return () => {
      if (eqIntervalRef.current) clearInterval(eqIntervalRef.current);
    };
  }, [isPlaying, hasStarted, isMuted]);

  // Initialize Speech Synthesis Voices
  useEffect(() => {
    if (typeof window === "undefined" || !window.speechSynthesis) return;

    const loadVoices = () => {
      const voices = window.speechSynthesis.getVoices();
      
      // 1. Prioritize Microsoft Edge Natural voices (Ahmet = male, Dilara = female)
      let selected = voices.find(
        (v) =>
          v.lang.includes("tr") &&
          v.name.toLowerCase().includes("natural") &&
          (v.name.toLowerCase().includes("ahmet") || v.name.toLowerCase().includes("dilara"))
      );
      
      // 2. Prioritize any natural/azure voice
      if (!selected) {
        selected = voices.find(
          (v) => v.lang.includes("tr") && v.name.toLowerCase().includes("natural")
        );
      }
      
      // 3. Prioritize Google Cloud voices
      if (!selected) {
        selected = voices.find(
          (v) => v.lang.includes("tr") && v.name.toLowerCase().includes("google")
        );
      }
      
      // 4. Prioritize Microsoft Tolga or other standard voices
      if (!selected) {
        selected = voices.find(
          (v) =>
            v.lang.includes("tr") &&
            (v.name.toLowerCase().includes("tolga") ||
              v.name.toLowerCase().includes("microsoft") ||
              v.name.toLowerCase().includes("seda"))
        );
      }
      
      // 5. Ultimate fallback
      const fallbackTr = voices.find((v) => v.lang.includes("tr"));
      setTrVoice(selected || fallbackTr || null);
    };

    loadVoices();
    if (window.speechSynthesis.onvoiceschanged !== undefined) {
      window.speechSynthesis.onvoiceschanged = loadVoices;
    }
  }, []);

  // Commentator text-to-speech speaker (Ercan Taner style shouting)
  const speakCommentator = (text: string) => {
    if (typeof window === "undefined" || !window.speechSynthesis || isMuted) return;

    window.speechSynthesis.cancel();
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = "tr-TR";

    if (trVoice) {
      utterance.voice = trVoice;
    }

    // High energy sports commentator configuration
    const isMale = trVoice?.name.toLowerCase().includes("ahmet") || 
                   trVoice?.name.toLowerCase().includes("tolga") || 
                   trVoice?.name.toLowerCase().includes("male");
                   
    utterance.pitch = isMale ? 0.98 : 1.12;  // Deeper power for male commentator, enthusiastic pitch for female
    utterance.rate = 1.08;   // Rapid commentator delivery
    utterance.volume = 1.0;  // Full volume

    window.speechSynthesis.speak(utterance);
  };

  const handleStartExperience = () => {
    setHasStarted(true);
    setIsPlaying(true);
    
    if (audioRef.current) {
      audioRef.current.volume = musicVolume;
      audioRef.current.muted = false;
      audioRef.current.play().catch(e => console.log("Audio play error:", e));
    }

    // Speak initial intro scene
    speakCommentator(SCENES[0].speech);
  };

  // Autoplay loop stepper
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

    const currentScene = SCENES[currentSceneIndex];
    const duration = currentScene.duration;
    const start = Date.now();

    setProgress(0);
    speakCommentator(currentScene.speech);

    // Progress Bar updater
    progressIntervalRef.current = setInterval(() => {
      const elapsed = Date.now() - start;
      const pct = Math.min((elapsed / duration) * 100, 100);
      setProgress(pct);
    }, 45);

    // Timeline stepper
    timerRef.current = setTimeout(() => {
      setCurrentSceneIndex((prev) => (prev + 1) % SCENES.length);
    }, duration);

    return () => {
      if (timerRef.current) clearTimeout(timerRef.current);
      if (progressIntervalRef.current) clearInterval(progressIntervalRef.current);
    };
  }, [currentSceneIndex, isPlaying, hasStarted]);

  useEffect(() => {
    if (audioRef.current) {
      audioRef.current.volume = isMuted ? 0 : musicVolume;
    }
  }, [musicVolume, isMuted]);

  // Hotkeys configuration
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === " ") {
        e.preventDefault();
        setIsPlaying((prev) => !prev);
      } else if (e.key === "c" || e.key === "C") {
        setCleanMode((prev) => !prev);
      } else if (e.key === "ArrowRight") {
        nextScene();
      } else if (e.key === "ArrowLeft") {
        prevScene();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [hasStarted]);

  const nextScene = () => {
    setCurrentSceneIndex((prev) => (prev + 1) % SCENES.length);
  };

  const prevScene = () => {
    setCurrentSceneIndex((prev) => (prev - 1 + SCENES.length) % SCENES.length);
  };

  return (
    <div className="w-screen h-screen overflow-hidden bg-[#07070C] text-[#f0f2f8] flex flex-col items-center justify-center relative font-sans select-none">
      
      {/* High-energy background electronic track SoundHelix-Song-4 (World top-10 style beat) */}
      <audio 
        ref={audioRef}
        src="https://www.soundhelix.com/examples/mp3/SoundHelix-Song-4.mp3"
        loop
        preload="auto"
      />

      {/* Cinematic Stadium backdrop */}
      <div className="absolute inset-0 fx-stadium opacity-90 transition-all duration-1000" />
      
      {/* Color aura overlays */}
      <div className="absolute inset-0 pointer-events-none mix-blend-screen transition-all duration-1000"
        style={{
          background: 
            currentSceneIndex === 0 ? "radial-gradient(circle at 50% 50%, rgba(46, 230, 110, 0.08) 0%, transparent 60%)" :
            currentSceneIndex === 1 ? "radial-gradient(circle at 50% 80%, rgba(46, 230, 110, 0.15) 0%, transparent 60%)" :
            currentSceneIndex === 2 ? "radial-gradient(circle at 30% 60%, rgba(77, 166, 255, 0.1) 0%, transparent 60%)" :
            currentSceneIndex === 3 ? "radial-gradient(circle at 40% 40%, rgba(46, 230, 110, 0.08) 0%, transparent 50%)" :
            currentSceneIndex === 4 ? "radial-gradient(circle at 80% 50%, rgba(168, 85, 247, 0.08) 0%, transparent 50%)" :
            currentSceneIndex === 5 ? "radial-gradient(circle at 50% 50%, rgba(245, 166, 35, 0.07) 0%, transparent 55%)" :
            currentSceneIndex === 6 ? "radial-gradient(circle at 20% 40%, rgba(168, 85, 247, 0.06) 0%, transparent 50%)" :
            "radial-gradient(circle at 50% 50%, rgba(46, 230, 110, 0.16) 0%, transparent 70%)"
        }}
      />

      {/* Grid overlay */}
      <div className="absolute inset-0 opacity-[0.03] pointer-events-none" 
        style={{
          backgroundImage: `linear-gradient(rgba(255,255,255,0.15) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.15) 1px, transparent 1px)`,
          backgroundSize: "60px 60px"
        }}
      />

      {/* ── INTERACTIVE PLAY PORTAL ── */}
      {!hasStarted && (
        <div className="absolute inset-0 z-50 bg-[#07070C]/97 backdrop-blur-md flex flex-col items-center justify-center p-6 text-center">
          <motion.div 
            initial={{ scale: 0.9, opacity: 0 }}
            animate={{ scale: 1, opacity: 1 }}
            transition={{ duration: 0.8 }}
            className="max-w-md p-8 rounded-3xl bg-[#14141C] border border-white/[0.08] flex flex-col items-center justify-center shadow-2xl relative overflow-hidden fx-glass"
          >
            <div className="absolute top-0 left-0 w-full h-1.5 bg-gradient-to-r from-neon to-signal-blue" />
            <span className="text-xs font-black text-neon tracking-widest uppercase mb-3 px-2 py-0.5 rounded bg-neon/10 border border-neon/20">FORMAX PROMO</span>
            <h1 className="text-3xl font-black text-[#f0f2f8] mb-4">Ercan Taner Anlatımıyla Reklam Başlıyor!</h1>
            <p className="text-sm text-[#8b91a8] leading-relaxed mb-6">
              Yüksek heyecan, saha içi taktiksel 3D simülasyon, TikTok kaydırma mantığı ve arka planda liste başı hit beati eşliğinde reklam deneyimi.
            </p>

            <div className="w-full p-4 rounded-xl bg-white/[0.02] border border-white/[0.04] text-left text-xs mb-6 space-y-2">
              <div className="flex justify-between text-[#8b91a8]">
                <span>Aktif Ses Motoru:</span>
                <span className="font-bold text-neon truncate max-w-[180px]">{trVoice ? trVoice.name : "Sistem Varsayılanı"}</span>
              </div>
              <p className="text-[10px] text-[#555c74] leading-relaxed">
                {trVoice?.name.toLowerCase().includes("natural") ? 
                  "✨ Harika! Doğal Azure/Edge İnsan Sesi devrede." : 
                  "💡 Robotik sesleri engellemek ve gerçek insan sesini (Microsoft Ahmet/Dilara Natural veya Google) duymak için bu sayfayı Microsoft Edge veya Google Chrome ile açmanız önemle önerilir."
                }
              </p>
            </div>
            
            <button 
              onClick={handleStartExperience}
              className="px-8 py-4 rounded-full bg-neon text-[#07070C] font-black text-lg hover:bg-neon-deep transition-all duration-300 shadow-[0_0_35px_rgba(46,230,110,0.5)] scale-100 hover:scale-105 active:scale-95"
            >
              ▶️ REKLAMI BAŞLAT
            </button>
            
            <span className="text-[10px] text-[#555c74] mt-6">
              *Ercan Taner seslendirme stili ve hit ritim beati tetiklenecektir.
            </span>
          </motion.div>
        </div>
      )}
      {/* MAIN CONTAINER */}
      <div className="w-full max-w-6xl h-full flex flex-col justify-between items-center py-10 px-6 z-10">
        
        {/* HEADER AREA */}
        <div className={`w-full flex justify-between items-center transition-all duration-300 ${cleanMode ? 'opacity-0 hover:opacity-100' : 'opacity-100'}`}>
          <div className="flex items-center gap-3">
            <span className="text-2xl font-black tracking-wider text-neon fx-icon-glow-green">FORMAX</span>
            <span className="text-[10px] border border-white/10 px-2 py-0.5 rounded text-[#8b91a8]">CINEMATIC v2</span>
          </div>

          {/* Equalizer Visualizer (Canlı Ses Dalgası) */}
          <div className="flex items-end gap-1.5 h-10 px-4 py-1.5 rounded-xl bg-white/[0.02] border border-white/[0.04]">
            <span className="text-[10px] font-black text-neon mr-1">SPORTS AUDIO:</span>
            {equalizerHeights.map((h, i) => (
              <motion.div
                key={i}
                className="w-1 bg-neon rounded-full"
                animate={{ height: h }}
                transition={{ type: "spring", stiffness: 300, damping: 15 }}
                style={{ height: `${h}px` }}
              />
            ))}
          </div>

          <div className="text-xs text-[#8b91a8] flex items-center gap-4">
            <button 
              onClick={() => setCleanMode(!cleanMode)} 
              className="px-3 py-1 rounded-full bg-white/5 border border-white/10 text-white hover:bg-white/10 transition-colors"
            >
              Kılavuz UI: {cleanMode ? "Gizli" : "Açık"} ('C')
            </button>
          </div>
        </div>

        {/* WORKSPACE AREA */}
        <div className="w-full flex-1 flex items-center justify-center relative py-6">
          <AnimatePresence mode="wait">
            
            {/* SCENE 0: INTRO */}
            {currentSceneIndex === 0 && (
              <motion.div
                key="intro"
                initial={{ opacity: 0, scale: 0.95 }}
                animate={{ opacity: 1, scale: 1 }}
                exit={{ opacity: 0, scale: 1.05 }}
                transition={{ duration: 0.8 }}
                className="flex flex-col items-center text-center max-w-3xl"
              >
                <motion.div 
                  initial={{ opacity: 0, scale: 0.4 }}
                  animate={{ opacity: 1, scale: 1 }}
                  transition={{ delay: 0.2, type: "spring", stiffness: 100 }}
                  className="w-28 h-28 rounded-full border border-neon/30 flex items-center justify-center mb-8 relative fx-glow-green bg-black/40"
                >
                  <motion.div
                    animate={{ rotate: 360 }}
                    transition={{ repeat: Infinity, duration: 10, ease: "linear" }}
                    className="absolute inset-1.5 border-t border-r-2 border-neon rounded-full"
                  />
                  <span className="text-4xl font-black tracking-tighter text-neon">FX</span>
                </motion.div>
                
                <motion.h1 
                  initial={{ opacity: 0, y: 30 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: 0.4, duration: 0.8 }}
                  className="text-6xl md:text-9xl font-black tracking-tighter mb-4 text-[#f0f2f8] bg-clip-text text-transparent bg-gradient-to-b from-[#f0f2f8] to-[#8b91a8]"
                >
                  FORMAX
                </motion.h1>
                
                <motion.p
                  initial={{ opacity: 0, y: 20 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: 0.7, duration: 0.8 }}
                  className="text-xl md:text-3xl font-light text-[#8b91a8] max-w-xl leading-relaxed"
                >
                  Futbol Keşfinde <span className="text-neon font-black fx-icon-glow-green">Yapay Zeka</span> Devrimi.
                </motion.p>
              </motion.div>
            )}

            {/* SCENE 1: 3D FIELD (Saha içinde Formax) */}
            {currentSceneIndex === 1 && (
              <motion.div
                key="pitch-3d"
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
                transition={{ duration: 0.8 }}
                className="w-full max-w-5xl flex flex-col lg:flex-row items-center gap-12"
              >
                <div className="flex-1 text-left max-w-md">
                  <span className="text-xs font-bold text-neon uppercase tracking-wider flex items-center gap-2 mb-3">
                    <span className="w-2.5 h-2.5 rounded-full bg-neon animate-ping" />
                    CANLI HÜCRESEL ZEKA
                  </span>
                  <h2 className="text-4xl md:text-5xl font-black tracking-tight mb-6 text-[#f0f2f8]">
                    Zeka Sahada Başlar.
                  </h2>
                  <p className="text-[#8b91a8] leading-relaxed mb-4 text-base">
                    Formax yeşil sahayı yaşayan bir zeka katmanına dönüştürür. Her oyuncu hareketi, pas kanalı ve taktik yerleşim, yapay zeka ile anında anlamlandırılır.
                  </p>
                </div>

                {/* 3D Isometric Pitch */}
                <div className="flex-1 flex justify-center items-center h-[350px] relative overflow-visible perspective-[1200px]">
                  <motion.div 
                    initial={{ rotateX: 65, rotateZ: -45, scale: 0.6, y: 100 }}
                    animate={{ rotateX: 55, rotateZ: -25, scale: 0.95, y: 0 }}
                    transition={{ duration: 1.5, ease: [0.16, 1, 0.3, 1] }}
                    className="w-[420px] h-[280px] bg-gradient-to-b from-[#113a1a] to-[#0a2310] border-4 border-white/20 rounded-xl relative shadow-[0_50px_100px_rgba(0,0,0,0.8)] overflow-hidden"
                  >
                    {/* Pitch markings */}
                    <div className="absolute inset-0 border border-white/10 m-2" />
                    <div className="absolute top-1/2 left-0 w-full h-[1px] bg-white/20 -translate-y-1/2" />
                    <div className="absolute top-1/2 left-1/2 w-28 h-28 border border-white/20 rounded-full -translate-x-1/2 -translate-y-1/2" />
                    <div className="absolute top-1/2 left-1/2 w-2 h-2 bg-white/30 rounded-full -translate-x-1/2 -translate-y-1/2" />
                    <div className="absolute top-0 left-1/2 -translate-x-1/2 w-44 h-16 border border-white/15 border-t-0" />
                    <div className="absolute bottom-0 left-1/2 -translate-x-1/2 w-44 h-16 border border-white/15 border-b-0" />

                    <div className="absolute inset-0 bg-[radial-gradient(circle_at_center,rgba(46,230,110,0.15)_0%,transparent_60%)]" />

                    {/* Glowing Passing Routes */}
                    <svg className="absolute inset-0 w-full h-full pointer-events-none opacity-40">
                      <motion.path 
                        d="M 120 70 L 210 140 L 300 70 M 210 140 L 210 220" 
                        fill="none" 
                        stroke="#2EE66E" 
                        strokeWidth="2" 
                        strokeDasharray="4 4"
                        animate={{ strokeDashoffset: [0, -20] }}
                        transition={{ repeat: Infinity, duration: 2, ease: "linear" }}
                      />
                    </svg>

                    {/* Glowing player nodes */}
                    <div className="absolute top-[70px] left-[120px] w-4 h-4 rounded-full bg-signal-blue border border-white/50 flex items-center justify-center shadow-[0_0_12px_#4DA6FF]" />
                    <div className="absolute top-[70px] left-[300px] w-4 h-4 rounded-full bg-signal-blue border border-white/50 flex items-center justify-center shadow-[0_0_12px_#4DA6FF]" />
                    <div className="absolute top-[140px] left-[210px] w-5 h-5 rounded-full bg-neon border border-white flex items-center justify-center shadow-[0_0_15px_#2EE66E] font-black text-[9px] text-[#07070C]">
                      FX
                    </div>
                    <div className="absolute top-[220px] left-[210px] w-4 h-4 rounded-full bg-signal-red border border-white/50 flex items-center justify-center shadow-[0_0_12px_#F5454D]" />

                    {/* Rising Radar Aura */}
                    <div className="absolute top-[100px] left-[170px] w-20 h-20 border-2 border-neon/20 rounded-full animate-ping pointer-events-none" />
                  </motion.div>

                  {/* Holographic Text label */}
                  <motion.div 
                    initial={{ opacity: 0, y: 20 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: 0.8 }}
                    className="absolute bottom-4 bg-[#14141C]/90 border border-neon/30 px-4 py-2 rounded-xl text-xs font-bold text-neon fx-glow-green"
                  >
                    FORMAX INTEL ENGINE v3.0
                  </motion.div>
                </div>
              </motion.div>
            )}

            {/* SCENE 2: TIKTOK SWIPE DISCOVERY */}
            {currentSceneIndex === 2 && (
              <motion.div
                key="tiktok-swipe"
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
                transition={{ duration: 0.6 }}
                className="flex flex-col lg:flex-row items-center justify-between w-full max-w-5xl gap-12"
              >
                <div className="flex-1 flex flex-col items-start text-left max-w-md">
                  <div className="flex items-center gap-2 mb-4">
                    <span className="w-2.5 h-2.5 rounded-full bg-neon animate-pulse" />
                    <span className="text-xs font-bold text-neon uppercase tracking-wider">TİKTOK MANTIĞI</span>
                  </div>
                  <h2 className="text-4xl font-black tracking-tight mb-6 text-[#f0f2f8]">
                    Kaydırarak <br />Maç Keşfet!
                  </h2>
                  <p className="text-[#8b91a8] leading-relaxed mb-6">
                    Boğucu menülerle vakit kaybetmeyin. Tıpkı TikTok'ta video izler gibi, maçlar arasındaki yapay zeka hikayelerini kaydırarak keşfedin. İlginizi çeken maçı sağa kaydırıp radarınıza ekleyin!
                  </p>
                </div>

                {/* Swipe Cards Stack Visual */}
                <div className="w-[325px] h-[360px] relative flex items-center justify-center">
                  {/* Skip Card Left */}
                  <motion.div 
                    animate={{ x: [-80, -100, -80], rotate: [-10, -15, -10], opacity: 0.4 }}
                    transition={{ repeat: Infinity, duration: 3, ease: "easeInOut" }}
                    className="absolute w-[200px] h-[280px] rounded-2xl bg-white/[0.01] border border-white/[0.04] p-4 flex flex-col justify-between select-none"
                  >
                    <div className="w-8 h-8 rounded-full border border-signal-red/20 flex items-center justify-center text-signal-red">✕</div>
                    <div className="h-4 w-24 bg-white/5 rounded" />
                  </motion.div>

                  {/* Accept Card Right */}
                  <motion.div 
                    animate={{ x: [80, 100, 80], rotate: [10, 15, 10], opacity: 0.4 }}
                    transition={{ repeat: Infinity, duration: 3, ease: "easeInOut" }}
                    className="absolute w-[200px] h-[280px] rounded-2xl bg-white/[0.01] border border-white/[0.04] p-4 flex flex-col justify-between items-end select-none"
                  >
                    <div className="w-8 h-8 rounded-full border border-neon/20 flex items-center justify-center text-neon">✓</div>
                    <div className="h-4 w-24 bg-white/5 rounded" />
                  </motion.div>

                  {/* Main Active Card */}
                  <motion.div 
                    animate={{ y: [0, -6, 0], rotate: [0, -1, 0] }}
                    transition={{ repeat: Infinity, duration: 4, ease: "easeInOut" }}
                    className="absolute w-[220px] h-[300px] rounded-2xl bg-[#14141C] border border-white/[0.08] p-5 flex flex-col justify-between shadow-2xl z-20 fx-glass"
                  >
                    <div className="flex justify-between items-center">
                      <span className="text-[9px] px-2 py-0.5 bg-neon/10 border border-neon/30 text-neon rounded-full font-bold">ÖNE ÇIKAN HİKAYE</span>
                      <span className="text-[9px] text-[#555c74]">Derbi</span>
                    </div>

                    <div className="flex flex-col gap-2 my-4">
                      <span className="text-xs font-bold text-[#f0f2f8]">İlgi Skoru: %92</span>
                      <p className="text-[11px] text-[#8b91a8] leading-relaxed">
                        Real Madrid son 5 maçta deplasmanda gol yemedi. Barcelona ise evinde son 12 maçta en az 2 gol attı. Kilit çözülüyor!
                      </p>
                    </div>

                    <div className="flex justify-between items-center text-[10px] text-[#555c74]">
                      <span>Sola: Pas Geç</span>
                      <span className="text-neon font-bold">Sağa: Radara Al</span>
                    </div>
                  </motion.div>
                </div>
              </motion.div>
            )}

            {/* SCENE 3: INTEREST ENGINE (Kullanıcı Tanıyan) */}
            {currentSceneIndex === 3 && (
              <motion.div
                key="interest-engine"
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
                transition={{ duration: 0.6 }}
                className="flex flex-col lg:flex-row-reverse items-center justify-between w-full max-w-5xl gap-12"
              >
                <div className="flex-1 flex flex-col items-start text-left max-w-md">
                  <div className="flex items-center gap-2 mb-4">
                    <span className="w-2.5 h-2.5 rounded-full bg-signal-purple" />
                    <span className="text-xs font-bold text-signal-purple uppercase tracking-wider">DİNAMİK PROFİLLEME</span>
                  </div>
                  <h2 className="text-4xl font-bold tracking-tight mb-6 text-[#f0f2f8]">
                    Sizi Tanıyan, <br />Sizinle Öğrenen AI.
                  </h2>
                  <p className="text-[#8b91a8] leading-relaxed mb-6">
                    Backend'deki **Interest Engine** sayesinde Formax, kaydırma ve tıklama alışkanlıklarınızı analiz eder. Favori liginiz, takip ettiğiniz takım veya gol eğilimlerinize göre ana vitrini tamamen kişiselleştirir.
                  </p>
                </div>

                {/* User Interest Profiler Mock */}
                <div className="w-[340px] p-5 rounded-3xl bg-[#14141C] border border-white/[0.08] fx-glass flex flex-col gap-4">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-full bg-neon/10 border border-neon flex items-center justify-center text-neon font-black">AI</div>
                    <div>
                      <h4 className="text-xs font-bold text-[#f0f2f8]">Öğrenen İlgi Modeli</h4>
                      <p className="text-[10px] text-[#555c74]">UserId #10842 · Güncellendi</p>
                    </div>
                  </div>

                  <div className="flex flex-col gap-2.5 mt-2">
                    {[
                      { name: "Takım İlgisi (Real Madrid)", val: "88%", color: "bg-neon" },
                      { name: "Lig İlgisi (La Liga)", val: "72%", color: "bg-signal-blue" },
                      { name: "İçerik İlgisi (Gol Pozisyonları)", val: "94%", color: "bg-signal-purple" }
                    ].map((interest, idx) => (
                      <div key={idx} className="flex flex-col gap-1">
                        <div className="flex justify-between text-[10px] font-bold">
                          <span className="text-[#8b91a8]">{interest.name}</span>
                          <span className="text-[#f0f2f8]">{interest.val}</span>
                        </div>
                        <div className="w-full h-1.5 bg-white/5 rounded-full overflow-hidden">
                          <motion.div 
                            initial={{ width: 0 }}
                            animate={{ width: interest.val }}
                            transition={{ delay: 0.3 + idx * 0.2, duration: 1 }}
                            className={`h-full ${interest.color}`}
                          />
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              </motion.div>
            )}

            {/* SCENE 4: RADAR ENGINE */}
            {currentSceneIndex === 4 && (
              <motion.div
                key="radar-engine"
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
                transition={{ duration: 0.6 }}
                className="flex flex-col lg:flex-row items-center justify-between w-full max-w-5xl gap-12"
              >
                <div className="flex-1 flex flex-col items-start text-left max-w-md">
                  <div className="flex items-center gap-2 mb-4">
                    <ActivityIcon />
                    <span className="text-xs font-bold text-signal-blue uppercase tracking-wider">RADAR ENGINE</span>
                  </div>
                  <h2 className="text-4xl font-bold tracking-tight mb-6 text-[#f0f2f8]">
                    Anlık Sinyaller, <br />Net Kanıtlar.
                  </h2>
                  <p className="text-[#8b91a8] leading-relaxed mb-6">
                    Radar Engine sahadaki dinamikleri (taktik değişiklikler, sakatlıklar, hava koşulları, piyasa hareketleri) anlık olarak süzgeçten geçirir ve önem derecesine göre ekranı şekillendirir.
                  </p>
                </div>

                {/* Radar Signals Stack */}
                <div className="w-[340px] flex flex-col gap-3">
                  {[
                    { type: "⚡ TAKTİK ANALİZ", value: "Real Madrid 4-3-3'e döndü. Hücum kanat ağırlığı sol kanatta %72.", color: "text-[#4DA6FF] border-[#4DA6FF]/20 bg-[#4DA6FF]/5" },
                    { type: "🌧️ HAVA DURUMU", value: "Şiddetli yağış başladı. Zemin kaygan. Uzaktan şut denemeleri artabilir.", color: "text-[#A855F7] border-[#A855F7]/20 bg-[#A855F7]/5" },
                    { type: "📈 PİYASA HAREKETİ", value: "Ev sahibi oranında ani düşüş: %3.4. Yüksek giriş yapılıyor.", color: "text-[#F5A623] border-[#F5A623]/20 bg-[#F5A623]/5" }
                  ].map((signal, idx) => (
                    <motion.div
                      key={idx}
                      initial={{ opacity: 0, x: -50 }}
                      animate={{ opacity: 1, x: 0 }}
                      transition={{ delay: 0.3 + idx * 0.2, type: "spring", stiffness: 80 }}
                      className={`p-4 rounded-xl border flex flex-col gap-1.5 ${signal.color} backdrop-blur-md`}
                    >
                      <div className="flex items-center justify-between">
                        <span className="text-[10px] font-bold tracking-wider">{signal.type}</span>
                        <span className="w-1.5 h-1.5 rounded-full bg-current animate-ping" />
                      </div>
                      <p className="text-[12px] text-[#8b91a8] leading-relaxed">
                        {signal.value}
                      </p>
                    </motion.div>
                  ))}
                </div>
              </motion.div>
            )}

            {/* SCENE 5: TAHMİN ORANLARI VE SAPMA */}
            {currentSceneIndex === 5 && (
              <motion.div
                key="market-deviation"
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
                transition={{ duration: 0.6 }}
                className="flex flex-col lg:flex-row-reverse items-center justify-between w-full max-w-5xl gap-12"
              >
                <div className="flex-1 flex flex-col items-start text-left max-w-md">
                  <div className="flex items-center gap-2 mb-4">
                    <span className="w-2.5 h-2.5 rounded-full bg-signal-yellow animate-ping" />
                    <span className="text-xs font-bold text-signal-yellow uppercase tracking-wider">OLASILIK & SAPMA ANALİZİ</span>
                  </div>
                  <h2 className="text-4xl font-bold tracking-tight mb-6 text-[#f0f2f8]">
                    Yapay Zekanın <br />Olasılık Hesapları.
                  </h2>
                  <p className="text-[#8b91a8] leading-relaxed mb-6">
                    Formax sadece oran vermez. Bahis sitelerinin açtığı oranlar ile yapay zekanın hesapladığı gerçek ihtimalleri karşılaştırır. Aradaki "değerli sapmaları" anında işaretler!
                  </p>
                </div>

                {/* Probability & Deviation Visualizer Card */}
                <div className="w-[340px] p-5 rounded-3xl bg-[#14141C] border border-white/[0.08] fx-glass flex flex-col gap-4">
                  <div className="flex justify-between items-center pb-2 border-b border-white/[0.06]">
                    <span className="text-xs font-bold text-[#f0f2f8]">Real Madrid - Barcelona</span>
                    <span className="text-[9px] px-2 py-0.5 rounded bg-signal-red/10 border border-signal-red/30 text-signal-red font-bold">Kritik Sapma Sinyali</span>
                  </div>

                  <div className="flex flex-col gap-3 py-1">
                    {/* Market vs Formax */}
                    <div className="flex justify-between items-center p-3 rounded-xl bg-white/[0.02] border border-white/[0.04]">
                      <div>
                        <div className="text-[9px] text-[#555c74] font-bold">PİYASA ORANI</div>
                        <div className="text-base font-black text-[#8b91a8]">2.20 (Olasılık: %45)</div>
                      </div>
                      <div className="text-right">
                        <div className="text-[9px] text-[#555c74] font-bold">FORMAX TAHMİNİ</div>
                        <div className="text-base font-black text-neon">1.72 (Olasılık: %58)</div>
                      </div>
                    </div>

                    {/* Deviation Alert Widget */}
                    <div className="p-3.5 rounded-xl bg-neon/5 border border-neon/30 flex items-center justify-between shadow-[0_0_15px_rgba(46,230,110,0.15)]">
                      <div className="flex items-center gap-2">
                        <span className="text-sm">🔥</span>
                        <div className="flex flex-col">
                          <span className="text-xs font-black text-neon">DEĞERLİ SAPMA BULUNDU!</span>
                          <span className="text-[9px] text-[#8b91a8]">+%13 daha yüksek kazanma olasılığı</span>
                        </div>
                      </div>
                      <span className="text-xs font-black text-[#07070C] bg-neon px-2.5 py-1 rounded-full">+13%</span>
                    </div>
                  </div>
                </div>
              </motion.div>
            )}

            {/* SCENE 6: NEWS AGGREGATOR (Haber Dedup ve Kümeleme) */}
            {currentSceneIndex === 6 && (
              <motion.div
                key="news-aggregator"
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
                transition={{ duration: 0.6 }}
                className="flex flex-col lg:flex-row items-center justify-between w-full max-w-5xl gap-12"
              >
                <div className="flex-1 flex flex-col items-start text-left max-w-md">
                  <div className="flex items-center gap-2 mb-4">
                    <NewspaperIcon />
                    <span className="text-xs font-bold text-signal-yellow uppercase tracking-wider">HABER İSTİHBARATI</span>
                  </div>
                  <h2 className="text-4xl font-bold tracking-tight mb-6 text-[#f0f2f8]">
                    Haber Kirliliğine <br />Yapay Zeka Filtresi.
                  </h2>
                  <p className="text-[#8b91a8] leading-relaxed mb-6">
                    Dünya basınından toplanan binlerce spor haberi ve manşet, backend'de **deduplication** ve **clustering** (kümeleme) algoritmalarıyla süzülür. Aynı haberi defalarca okumazsınız. Sadece tek ve net bir AI haber özeti görürsünüz.
                  </p>
                </div>

                {/* News Clustering Animation visual */}
                <div className="w-[340px] flex flex-col gap-3 relative">
                  {/* News bubble clustering flow */}
                  <div className="flex gap-2 justify-center mb-1 overflow-hidden opacity-60">
                    <span className="text-[10px] px-2.5 py-1 rounded-full bg-white/5 border border-white/10 text-[#8b91a8] truncate max-w-[120px]">"Mbappe sakatlandı mı?"</span>
                    <span className="text-[10px] px-2.5 py-1 rounded-full bg-white/5 border border-white/10 text-[#8b91a8] truncate max-w-[120px]">"Mbappe şoku!"</span>
                    <span className="text-[10px] px-2.5 py-1 rounded-full bg-white/5 border border-white/10 text-[#8b91a8] truncate max-w-[120px]">"Kylian antrenmanı yarıda bıraktı"</span>
                  </div>

                  {/* AI Merger arrow */}
                  <div className="flex justify-center items-center h-4 text-neon font-black">
                    ↓ Yapay Zeka Kümeleme (Clustering) ↓
                  </div>

                  {/* Clean AI News card result */}
                  <motion.div 
                    initial={{ y: 20, opacity: 0 }}
                    animate={{ y: 0, opacity: 1 }}
                    transition={{ delay: 0.5 }}
                    className="p-4.5 rounded-2xl bg-[#14141C] border border-neon/30 fx-glass flex flex-col gap-2.5 shadow-[0_0_20px_rgba(46,230,110,0.1)]"
                  >
                    <div className="flex justify-between items-center">
                      <span className="text-[10px] font-bold text-signal-yellow uppercase">TEKİLLEŞTİRİLMİŞ HABER ÖZETİ</span>
                      <span className="text-[9px] px-1.5 py-0.5 rounded bg-signal-yellow/10 text-signal-yellow font-bold">14 Farklı Kaynak</span>
                    </div>
                    <h4 className="text-xs font-bold text-[#f0f2f8]">Kylian Mbappe Uyluk Sakatlığı Nedeniyle Kadrodan Çıkarıldı</h4>
                    <p className="text-[11px] text-[#8b91a8] leading-relaxed">
                      "Real Madrid sağlık heyeti, Mbappe'nin sol uyluğunda birinci derece kas zorlanması tespit etti. Yaklaşık 2 hafta sahalardan uzak kalacak ve hafta sonu oynanacak derbide yer alamayacak."
                    </p>
                  </motion.div>
                </div>
              </motion.div>
            )}

            {/* SCENE 7: OUTRO */}
            {currentSceneIndex === 7 && (
              <motion.div
                key="outro"
                initial={{ opacity: 0, scale: 0.95 }}
                animate={{ opacity: 1, scale: 1 }}
                exit={{ opacity: 0, scale: 1.05 }}
                transition={{ duration: 0.8 }}
                className="flex flex-col items-center text-center max-w-3xl"
              >
                <motion.div 
                  initial={{ opacity: 0, scale: 0.8 }}
                  animate={{ opacity: 1, scale: 1 }}
                  transition={{ delay: 0.2 }}
                  className="w-24 h-24 rounded-full border border-neon flex items-center justify-center mb-8 fx-glow-green bg-black/50"
                >
                  <span className="text-3xl font-black text-neon">FX</span>
                </motion.div>

                <motion.h2 
                  initial={{ opacity: 0, y: 20 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: 0.4, duration: 0.8 }}
                  className="text-6xl md:text-8xl font-black tracking-tighter mb-6 text-[#f0f2f8]"
                >
                  FORMAX
                </motion.h2>

                <motion.p
                  initial={{ opacity: 0, y: 15 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: 0.7, duration: 0.8 }}
                  className="text-xl md:text-2xl text-[#8b91a8] mb-10 max-w-lg leading-relaxed font-light"
                >
                  Futbolu sadece izlemeyin, yapay zeka ile keşfedin. <br />
                  <span className="text-[#f0f2f8] font-bold">Çok yakında yayındayız.</span>
                </motion.p>

                <motion.button
                  initial={{ opacity: 0, y: 15 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: 1, duration: 0.8 }}
                  className="px-10 py-5 rounded-full bg-neon text-[#07070C] font-black text-lg hover:bg-neon-deep transition-all duration-300 shadow-[0_0_35px_rgba(46,230,110,0.5)] scale-100 hover:scale-105"
                >
                  Keşfetmeye Başla
                </motion.button>
              </motion.div>
            )}

          </AnimatePresence>
        </div>

        {/* PROGRESS BAR AND TIMELINE */}
        <div className="w-full flex flex-col gap-6">
          <div className="w-full h-1 bg-white/[0.04] rounded-full overflow-hidden relative">
            <div 
              className="h-full bg-neon transition-all duration-75"
              style={{ width: `${progress}%` }}
            />
          </div>

          {/* Controls menu (hidden in cleanMode unless hovered) */}
          <div className={`w-full flex flex-col md:flex-row justify-between items-center gap-4 transition-opacity duration-300 ${cleanMode ? 'opacity-0 hover:opacity-100' : 'opacity-100'}`}>
            
            {/* Player Controls */}
            <div className="flex items-center gap-3">
              <button 
                onClick={prevScene} 
                className="w-10 h-10 rounded-full border border-white/10 flex items-center justify-center text-white hover:bg-white/5 transition-colors"
                title="Önceki Sahne (Sol Ok)"
              >
                ←
              </button>
              <button 
                onClick={() => setIsPlaying(!isPlaying)} 
                className="px-4 py-2 rounded-full border border-white/10 flex items-center gap-2 text-sm text-white hover:bg-white/5 transition-colors font-bold"
                title="Oynat / Duraklat (Boşluk)"
              >
                {isPlaying ? "⏸️ DURAKLAT" : "▶️ OYNAT"}
              </button>
              <button 
                onClick={nextScene} 
                className="w-10 h-10 rounded-full border border-white/10 flex items-center justify-center text-white hover:bg-white/5 transition-colors"
                title="Sonraki Sahne (Sağ Ok)"
              >
                →
              </button>
            </div>

            {/* Slide Index dots */}
            <div className="flex gap-2">
              {SCENES.map((scene, idx) => (
                <button
                  key={scene.id}
                  onClick={() => {
                    setCurrentSceneIndex(idx);
                    setProgress(0);
                  }}
                  className={`px-2.5 py-1 rounded text-[10px] font-black transition-all ${currentSceneIndex === idx ? 'bg-neon text-[#07070C]' : 'bg-white/5 text-[#555c74] hover:bg-white/10'}`}
                  title={scene.title}
                >
                  {idx + 1}
                </button>
              ))}
            </div>

            {/* Audio configuration & Clean mode hint */}
            <div className="flex items-center gap-4">
              <button 
                onClick={() => setIsMuted(!isMuted)} 
                className="flex items-center gap-1.5 text-xs text-[#8b91a8] hover:text-[#f0f2f8] transition-colors font-bold"
              >
                {isMuted ? <VolumeMuteIcon /> : <VolumeIcon />}
                {isMuted ? "SESİ AÇ" : "SESİ SUSTUR"}
              </button>
              
              <span className="text-[#555c74] text-xs">
                {cleanMode ? "Görünüm temiz (kayıt için). Fareyle buraya gelerek kontrol edebilirsiniz." : "Temiz mod için 'C' tuşuna basın."}
              </span>
            </div>
            
          </div>
        </div>

      </div>
    </div>
  );
}
