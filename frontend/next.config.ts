import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Pin the Turbopack root to this dir so it doesn't infer the wrong workspace
  // root from a stray lockfile higher up the tree (e.g. ~/package-lock.json).
  turbopack: {
    root: import.meta.dirname,
  },
  output: "standalone",
};

export default nextConfig;
