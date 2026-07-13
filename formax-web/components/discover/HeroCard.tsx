"use client";

import { motion } from "framer-motion";
import type { RecommendationCardDto } from "@/types/api";
import { TeamCrest } from "@/components/ui/TeamCrest";
import { TargetIcon } from "./icons";
import { awayName, homeName, leagueLabel, matchTime } from "./cardSignals";
import { HeroBackground } from "./HeroBackground";


// Discovery Hero — stadyum atmosferi · lig/saat (üst) · büyük logolar + VS + isimler.
export function HeroCard({ card }: { card: RecommendationCardDto }) {
  const home = homeName(card);
  const away = awayName(card);
  const league = leagueLabel(card);
  const time = matchTime(card);


  return (
  <motion.article

    
    
    initial={{ opacity: 0, y: 12 }}
    animate={{ opacity: 1, y: 0 }}
    transition={{ duration: 0.45 }}
    className="
      relative
      isolate
      overflow-hidden
      rounded-[24px]
      border border-white/5
      bg-[#080A12]
      shadow-[0_24px_70px_rgba(0,0,0,.45)]
      backdrop-blur-xl
      h-[285px]
    "
  >
    {/* Premium stadyum atmosferi (gerçek webp + sis/huzme/vignette) */}
    <HeroBackground />

    <div className="relative h-full px-8 pt-6 pb-8">

        {/* League */}
{(league || time) && (
  <div className="flex flex-col items-center gap-2">

    {league && (
      <div
        className="
        inline-flex
        items-center
        gap-2
        rounded-full
        border border-white/10
        bg-white/5
        px-4
        py-1.5
        backdrop-blur-md
      "
      >
        <TargetIcon size={14} className="text-white/60" />

        <span className="text-sm font-medium tracking-wide text-white/85">
          {league}
        </span>
      </div>
    )}

    {time && (
      time.live ? (

          <div
          className="
          rounded-full
          border border-red-500/25
          bg-red-500/15
          px-4
          py-1
         "
         >
          <span className="text-xs font-bold uppercase tracking-[0.2em] text-red-300">
            CANLI
          </span>
        </div>

       ) : (

        <span className="text-sm font-semibold text-white/70">
          {time.text}
        </span>

       )
      )}

     </div>
    )}

        {/* Teams */}

  <div className="mt-6 grid grid-cols-[1fr_auto_1fr] items-center px-2">

  <div className="flex justify-start">
    <motion.div
      whileHover={{ scale: 1.04 }}
      transition={{ duration: .25 }}
      className="
        relative
        flex
        h-[96px]
        w-[96px]
        items-center
        justify-center
        rounded-full
      "
    >

      <div
  className="absolute inset-2 rounded-full bg-black/30 blur-2xl "
/>

      <div className="absolute inset-0 rounded-full border border-white/10" />

      <TeamCrest
        name={home}
        logoUrl={card.homeTeam?.logoUrl}
        size={86}
      />


    </motion.div>
  </div>

  <div className="mx-2">

    <motion.div

      animate={{
        opacity:[.6,1,.6],
        scale:[1,1.05,1]
      }}

      transition={{
        duration:3,
        repeat:Infinity
      }}

      className="
        text-[30px]
        font-black
        tracking-tight
        text-[#C77DFF]
        drop-shadow-[0_0_45px_rgba(199,125,255,.95)]
      "

    >
      VS
    </motion.div>

  </div>

  <div className="flex justify-end">

    <motion.div

      whileHover={{ scale:1.04 }}

      transition={{ duration:.25 }}

      className="
        relative
        flex
        h-[96px]
        w-[96px]
        items-center
        justify-center
        rounded-full
      "

    >

      <div className="absolute inset-0 rounded-full bg-white/5 blur-xl" />

      <div className="absolute inset-0 rounded-full border border-white/5" />

      <TeamCrest
        name={away}
        logoUrl={card.awayTeam?.logoUrl}
        size={88}
      />

    </motion.div>

  </div>

</div>

{/* Team Names */}

<div className="mt-5 grid grid-cols-2 gap-10 px-2">

  <h2
    className="
        ml-[-40px] mr-auto
        max-w-[190px]
        text-center
        text-[19px]
        font-extrabold
        uppercase
        leading-[0.88]
        text-white
        whitespace-normal
        break-words
    "
  >
    {home}
  </h2>

  <h2
    className="
      ml-auto mr-[-30px]
      max-w-[190px]
      text-center
      text-[19px]
      font-extrabold
      uppercase
      leading-[0.88]
      text-white
      whitespace-normal
      break-words
    "
  >
    {away}
  </h2>

</div>

</div>
  </motion.article>
);
}

