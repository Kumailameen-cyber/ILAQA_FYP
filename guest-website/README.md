# ILAQA Guest Website

A simple Next.js frontend for the ILAQA Final Year Project.

## Start in VS Code
1. Install Node.js (version 20 or newer).
2. Open this folder in VS Code.
3. Open Terminal > New Terminal.
4. Run `npm install`.
5. Run `npm run dev`.
6. Visit http://localhost:3000.

## Only three main code files
- `app/page.js`: all pages, demo data, filtering, theme switch and navigation.
- `app/globals.css`: colors, layout and mobile styling.
- `app/layout.js`: common Next.js layout and page title.

## Karachi map photo
Place a map screenshot that you have permission to use in `public/karachi-map.jpg`.
The background will then appear automatically on the map sections.
Until then, a dark placeholder background is shown. The territory polygons are illustrative demo data, not actual geographic boundaries.

## What works
- Public Home, Explore Map, Leaderboard, Competitions, How It Works sections
- Dark/light mode saved in the browser
- Area and metric filters for leaderboard
- Territory search, status filter and selected details
- Competition status filter (no joining)
- Mobile navigation

## What is not connected yet
- Backend API, real territory data, real user data and live updates.
- No authentication is required for the guest website.

All names, scores and competitions in `page.js` are sample data for frontend demonstration.
