"use client";

import { usePathname } from "next/navigation";
import { BottomNav } from "@/components/ui/BottomNav";

interface LayoutWrapperProps {
  children: React.ReactNode;
}

export function LayoutWrapper({ children }: LayoutWrapperProps) {
  const pathname = usePathname();
  
  // Routes that should be rendered in full screen without the mobile frame and bottom navigation
  const isWidePage = pathname === "/promo" || pathname === "/demo";

  if (isWidePage) {
    return <>{children}</>;
  }

  return (
    <div className="flex min-h-screen justify-center">
      <div
        className="
          relative
          w-full
          max-w-[430px]
          min-h-screen
          overflow-hidden
          bg-[#070911]
          border-x
          border-white/10
          shadow-[0_0_80px_rgba(0,0,0,.45)]
        "
      >
        {children}
        <BottomNav />
      </div>
    </div>
  );
}
