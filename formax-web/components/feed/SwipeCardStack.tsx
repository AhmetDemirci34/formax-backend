"use client";

import { useRef } from "react";
import { useRouter } from "next/navigation";
import {
  motion, AnimatePresence,
  useMotionValue, useTransform,
} from "framer-motion";
import type { RecommendationCardDto } from "@/types/api";
import type { SwipeDirection } from "@/types/api";

const SWIPE_THRESHOLD = 90;

interface Props {
  activeCard: RecommendationCardDto;
  leftPeek?: RecommendationCardDto | null;
  rightPeek?: RecommendationCardDto | null;
  onSwipe: (direction: SwipeDirection) => void;
  onDetailOpen: () => void;
  children: (card: RecommendationCardDto) => React.ReactNode;
  renderPeek: (card: RecommendationCardDto, side: "left" | "right") => React.ReactNode;
}

export function SwipeCardStack({
  activeCard, leftPeek, rightPeek, onSwipe, onDetailOpen, children, renderPeek,
}: Props) {
  const router = useRouter();
  const x = useMotionValue(0);

  // Daha sakin/premium his: yumuşak dönüş + ince overlay.
  const rotate = useTransform(x, [-240, 0, 240], [-3.5, 0, 3.5]);
  const leftOverlayOpacity = useTransform(x, [0, -130], [0, 0.12]);
  const rightOverlayOpacity = useTransform(x, [0, 130], [0, 0.12]);

  const isDraggingRef = useRef(false);
  const dragDeltaRef = useRef(0);

  function handleDragStart() {
    isDraggingRef.current = true;
    dragDeltaRef.current = 0;
  }
  function handleDrag(_: unknown, info: { offset: { x: number } }) {
    dragDeltaRef.current = Math.abs(info.offset.x);
  }
  function handleDragEnd(_: unknown, info: { offset: { x: number } }) {
    if (Math.abs(info.offset.x) > SWIPE_THRESHOLD) {
      onSwipe(info.offset.x < 0 ? "left" : "right");
    }
    setTimeout(() => {
      isDraggingRef.current = false;
      dragDeltaRef.current = 0;
    }, 0);
  }
  function handleTap() {
    if (isDraggingRef.current || dragDeltaRef.current > 8) return;
    onDetailOpen();
    router.push(`/match/${activeCard.matchId}`);
  }

  return (
    <div className="relative w-full">
      {/* Sol peek */}
      {leftPeek && (
        <div className="absolute top-8 bottom-8 left-0 w-[86%] -translate-x-[74%] scale-[0.92] opacity-60 -rotate-[3deg] z-0 pointer-events-none">
          {renderPeek(leftPeek, "left")}
        </div>
      )}
      {/* Sağ peek */}
      {rightPeek && (
        <div className="absolute top-8 bottom-8 right-0 w-[86%] translate-x-[74%] scale-[0.92] opacity-60 rotate-[3deg] z-0 pointer-events-none">
          {renderPeek(rightPeek, "right")}
        </div>
      )}

      {/* Aktif kart */}
      <AnimatePresence mode="wait">
        <motion.div
          key={activeCard.matchId}
          className="relative overflow-hidden rounded-3xl z-10"
          style={{ x, rotate, cursor: "grab", boxShadow: "0 10px 30px rgba(0,0,0,0.55)" }}
          drag="x"
          dragElastic={0.15}
          dragConstraints={{ left: 0, right: 0 }}
          onDragStart={handleDragStart}
          onDrag={handleDrag}
          onDragEnd={handleDragEnd}
          onClick={handleTap}
          initial={{ opacity: 0, scale: 0.98 }}
          animate={{ opacity: 1, scale: 1 }}
          exit={{ opacity: 0, transition: { duration: 0.18 } }}
          transition={{ type: "spring", stiffness: 240, damping: 30 }}
          whileDrag={{ cursor: "grabbing" }}
        >
          {children(activeCard)}

          {/* Swipe overlay'leri */}
          <motion.div
            className="absolute inset-0 pointer-events-none rounded-3xl"
            style={{ opacity: leftOverlayOpacity, background: "rgba(239,68,68,1)" }}
          />
          <motion.div
            className="absolute inset-0 pointer-events-none rounded-3xl"
            style={{ opacity: rightOverlayOpacity, background: "rgba(52,210,122,1)" }}
          />
        </motion.div>
      </AnimatePresence>
    </div>
  );
}
