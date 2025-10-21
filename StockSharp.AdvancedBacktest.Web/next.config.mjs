/** @type {import('next').NextConfig} */
const nextConfig = {
  output: 'export',
  images: {
    unoptimized: true,
  },
  // Ensure trailing slashes for proper relative path resolution
  trailingSlash: true,
};

export default nextConfig;
