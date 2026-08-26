"use client";

import { usePathname } from "next/navigation";
import { AppChrome } from "@/components/layout/AppChrome";

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
          h-[100dvh]
          w-full
          max-w-[430px]
          overflow-hidden
          bg-[#070911]
          border-x
          border-white/10
          shadow-[0_0_80px_rgba(0,0,0,.45)]
        "
      >
        {/* AppChrome: kaydırılabilir ekran + push efekti + Side Drawer + Language Sheet */}
        <AppChrome>{children}</AppChrome>
      </div>
    </div>
  );
}
