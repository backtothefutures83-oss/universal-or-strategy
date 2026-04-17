/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', 'sans-serif'],
        mono: ['JetBrains Mono', 'monospace'],
      },
      colors: {
        platinum: {
          50: '#f4f6f8',
          100: '#e3e8ef',
          200: '#c7d2de',
          300: '#a2b6c9',
          400: '#7793af',
          500: '#5a7896',
          600: '#485f7a',
          700: '#3a4d64',
          800: '#324254',
          900: '#2d3847',
          950: '#1d2530',
        },
        blood: {
          500: '#ff2a2a',
          600: '#e60000',
          900: '#4a0000',
        }
      },
      backgroundImage: {
        'grid-pattern': "url(\"data:image/svg+xml,%3Csvg width='40' height='40' viewBox='0 0 40 40' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath d='M0 0h40v40H0V0zm1 1h38v38H1V1z' fill='%23334155' fill-opacity='0.1' fill-rule='evenodd'/%3E%3C/svg%3E\")",
      }
    },
  },
  plugins: [],
}