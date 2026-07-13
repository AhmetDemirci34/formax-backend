import type { Metadata } from "next";
import "./globals.css";
import { Providers } from "@/context/Providers";
import { LayoutWrapper } from "@/components/layout/LayoutWrapper";

export const metadata: Metadata = {
  title: "FORMAX — Football Intelligence",
  description: "AI-first football intelligence platform",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="tr" className="h-full">
      <body className="min-h-screen bg-[#05070D]">
        <Providers>
          <LayoutWrapper>
            {children}
          </LayoutWrapper>
        </Providers>
      </body>
    </html>
  );
}

