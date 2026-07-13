import axios from "axios";

// All requests go through Next.js rewrites → http://localhost:5063
// No base URL needed — /api/* is proxied automatically
const apiClient = axios.create({
  baseURL: "",
  headers: { "Content-Type": "application/json" },
  timeout: 10_000,
});

// Attach JWT token if present
apiClient.interceptors.request.use((config) => {
  if (typeof window !== "undefined") {
    const token = localStorage.getItem("formax_token");
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
  }
  return config;
});

// Redirect to login on 401
apiClient.interceptors.response.use(
  (res) => res,
  (err) => {
    if (err.response?.status === 401 && typeof window !== "undefined") {
      localStorage.removeItem("formax_token");
      // Soft redirect — don't break SSR
      window.location.href = "/auth/login";
    }
    return Promise.reject(err);
  }
);

export default apiClient;
