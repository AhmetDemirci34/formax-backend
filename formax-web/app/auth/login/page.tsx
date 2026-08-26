"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { login } from "@/lib/api/auth";
import { useAuth } from "@/context/AuthContext";

export default function LoginPage() {
  const router = useRouter();
  const { setAuth } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const res = await login({ email, password });
      setAuth(res);
      router.push("/");
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        "Giriş yapılamadı.";
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-bg-base flex items-center justify-center px-4">
      <div className="w-full max-w-sm">
        <div className="text-center mb-8">
          <div className="text-accent font-black text-2xl tracking-tight mb-1">FORMAX</div>
          <div className="text-text-muted text-sm">Football Intelligence</div>
        </div>

        <form
          onSubmit={handleSubmit}
          className="bg-bg-card rounded-2xl border border-border p-6 space-y-4"
        >
          <h1 className="text-text-primary font-semibold text-lg">Giriş Yap</h1>

          {error && (
            <div className="bg-formax-red/10 border border-formax-red/30 rounded-lg px-3 py-2 text-sm text-formax-red">
              {error}
            </div>
          )}

          <div className="space-y-1">
            <label className="text-xs font-medium text-text-muted">E-posta</label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              placeholder="ornek@formax.com"
              className="w-full bg-bg-elevated border border-border rounded-lg px-3 py-2.5 text-sm text-text-primary placeholder-text-muted focus:border-accent focus:outline-none transition-colors"
            />
          </div>

          <div className="space-y-1">
            <label className="text-xs font-medium text-text-muted">Şifre</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              placeholder="••••••••"
              className="w-full bg-bg-elevated border border-border rounded-lg px-3 py-2.5 text-sm text-text-primary placeholder-text-muted focus:border-accent focus:outline-none transition-colors"
            />
          </div>

          <button
            type="submit"
            disabled={loading}
            className="w-full bg-accent hover:bg-accent-dim text-white font-semibold py-2.5 rounded-lg text-sm transition-colors disabled:opacity-60"
          >
            {loading ? "Giriş yapılıyor..." : "Giriş Yap"}
          </button>

          <p className="text-center text-sm text-text-muted">
            Hesabın yok mu?{" "}
            <Link href="/auth/register" className="text-accent hover:underline">
              Kayıt ol
            </Link>
          </p>
        </form>
      </div>
    </div>
  );
}
