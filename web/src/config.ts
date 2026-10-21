// Base URL used to build registration links / QR codes. A phone scanning the QR
// cannot resolve "localhost", so set VITE_PUBLIC_BASE_URL to the machine's LAN
// address (see .env.example); otherwise fall back to the current origin.
export const publicBaseUrl: string =
  import.meta.env.VITE_PUBLIC_BASE_URL || window.location.origin
