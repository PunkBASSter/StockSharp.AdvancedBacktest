# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Initial Setup
- **Install Dependencies**: `npm install`
- **Create Next.js Project**: `npx create-next-app@latest . --typescript --tailwind --eslint --app --no-src-dir`

### Development
- **Start Development Server**: `npm run dev`
- **Build Production**: `npm run build`
- **Start Production Server**: `npm start`
- **Run Linting**: `npm run lint`
- **Fix Linting Issues**: `npm run lint --fix`

### Testing
- **Run Tests**: `npm test`
- **Run Tests in Watch Mode**: `npm run test:watch`
- **Run Tests with Coverage**: `npm run test:coverage`

## Project Architecture

This is a Next.js web application for visualizing StockSharp backtesting results. The app renders trading data from JSON files produced by backtest runs.

### Core Technologies
- **Framework**: Next.js with TypeScript
- **Charting**: TradingView Lightweight Charts for candlestick charts and equity curves
- **Styling**: Tailwind CSS
- **Data Source**: Static JSON files from backtest runs

### Key Features
- Interactive candlestick chart visualization
- Equity curve rendering
- Trading performance metrics display
- Static site generation for fast loading
- Responsive design for various screen sizes

### Data Flow
1. Backtest runs from the main .NET application generate JSON output files
2. Web app reads these JSON files as static data sources
3. TradingView Lightweight Charts renders the financial data
4. Interactive components allow users to explore results

### File Structure (Planned)
- `app/`: Next.js App Router pages and layouts
- `components/`: Reusable React components
  - `charts/`: Chart-specific components
  - `metrics/`: Trading metrics display components
- `lib/`: Utility functions and data processing
- `types/`: TypeScript type definitions for trading data
- `public/data/`: Static JSON files from backtest runs
- `styles/`: Global styles and Tailwind configuration

### Development Notes
- Use TypeScript for all components and utilities
- Follow Next.js App Router conventions
- Implement proper error boundaries for chart rendering
- Ensure responsive design across desktop and mobile
- Optimize for static export when possible for faster deployment