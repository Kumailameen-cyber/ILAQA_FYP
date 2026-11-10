
"use client";

import { useState } from "react";

const runners = [
  { name: "Abdul Rehman", area: "Malir Cantt", territory: 12.84, distance: 186.2, streak: 12 },
  { name: "Kumail A.", area: "Nazimabad", territory: 11.2, distance: 174.8, streak: 9 },
  { name: "Abdul Wasay", area: "DHA", territory: 9.76, distance: 160.3, streak: 16 },
  { name: "Mustafa Imran", area: "Clifton", territory: 8.94, distance: 142.1, streak: 7 },
  { name: "Noor F.", area: "Gulshan", territory: 8.41, distance: 138.4, streak: 11 },
];

const competitions = [
  {
    name: "Clifton Coastline",
    type: "ZONE RACE",
    date: "24 OCT - 02 NOV",
    runners: 84,
    description: "Claim the most territory along the coastline.",
  },
  {
    name: "Campus 250 Relay",
    type: "DISTANCE RACE",
    date: "05 NOV - 12 NOV",
    runners: 48,
    description: "Every kilometre counts. Race your university rivals.",
  },
  {
    name: "Karachi Sprint Week",
    type: "CITY CHALLENGE",
    date: "12 SEP - 19 SEP",
    runners: 62,
    description: "A city-wide challenge to cover the most distance.",
  },
];

const navigation = [
  { id: "home", label: "Home" },
  { id: "map", label: "Explore Map" },
  { id: "leaderboard", label: "Leaderboard" },
  { id: "competitions", label: "Competitions" },
  { id: "how-it-works", label: "How It Works" },
];

function Arrow() {
  return <span aria-hidden="true">↗</span>;
}

function PodiumCard({ runner, rank }) {
  return (
    <article className={`podium-card podium-${rank}`}>
      {rank === 1 && <span className="crown">♛</span>}
      <div className="podium-rank">
        {String(rank).padStart(2, "0")} /{" "}
        {rank === 1 ? "CHAMPION" : rank === 2 ? "SILVER" : "BRONZE"}
      </div>
      <div className="runner-avatar">
        {runner.name
          .split(" ")
          .map((word) => word[0])
          .join("")
          .slice(0, 2)}
      </div>
      <h3>{runner.name}</h3>
      <p>{runner.area}</p>
      <strong>
        {runner.territory.toFixed(2)} <small>km²</small>
      </strong>
    </article>
  );
}

export default function Home() {
  const [theme, setTheme] = useState("dark");
  const [menuOpen, setMenuOpen] = useState(false);
  const [area, setArea] = useState("All Areas");

  const areas = [
    "All Areas",
    ...new Set(runners.map((runner) => runner.area)),
  ];

  const filteredRunners =
    area === "All Areas"
      ? runners
      : runners.filter((runner) => runner.area === area);

  function navigate(id) {
    setMenuOpen(false);
    document.getElementById(id)?.scrollIntoView({
      behavior: "smooth",
    });
  }

  return (
    <div className="site" data-theme={theme}>
      <header className="site-header">
        <div className="container header-inner">
          <button
            className="brand"
            onClick={() => navigate("home")}
          >
            <img src="/ilaqa-logo.jpg" alt="ILAQA logo" />
            <span>
              ILAQA<span className="accent">.</span>
            </span>
          </button>

          <nav className={`nav-links ${menuOpen ? "open" : ""}`}>
            {navigation.map((item) => (
              <button
                key={item.id}
                onClick={() => navigate(item.id)}
              >
                {item.label}
              </button>
            ))}
          </nav>

          <div className="header-actions">
            <button
              className="theme-toggle"
              onClick={() =>
                setTheme(theme === "dark" ? "light" : "dark")
              }
              aria-label="Toggle theme"
            >
              {theme === "dark" ? "☾" : "☀"}
            </button>

            <button
              className="menu-toggle"
              onClick={() => setMenuOpen(!menuOpen)}
              aria-label="Toggle navigation"
            >
              ☰
            </button>
          </div>
        </div>
      </header>

      <main>
        {/* HERO */}
        <section className="hero" id="home">
          <div className="hero-photo" />
          <div className="hero-shade" />

          <div className="container hero-content">
            <div className="hero-copy">
              <div className="eyebrow">
                THE WORLD IS YOUR ARENA
                <span className="eyebrow-line" />
              </div>

              <h1>
                <span>RUN YOUR CITY.</span>
                <span>
                  OWN YOUR <em>ILAQA.</em>
                </span>
              </h1>

              <p>
                Every street is a challenge. Every loop is a chance
                to claim your ground. Turn your everyday run into
                a real-world game of territory, rivalry, and glory.
              </p>

              <div className="hero-buttons">
                <button
                  className="btn btn-primary"
                  onClick={() => navigate("map")}
                >
                  EXPLORE TERRITORIES <Arrow />
                </button>

                <button
                  className="btn btn-outline"
                  onClick={() => navigate("how-it-works")}
                >
                  ▷ HOW IT WORKS
                </button>
              </div>
            </div>

            <div className="hero-steps">
              <div className="hero-step">
                <div className="hero-step-icon">◎</div>
                <div>
                  <h3><em>01</em> RUN</h3>
                  <p>
                    Take on the streets and rack up distance
                    with every run.
                  </p>
                </div>
              </div>

              <div className="hero-step">
                <div className="hero-step-icon">⬡</div>
                <div>
                  <h3><em>02</em> CLOSE THE LOOP</h3>
                  <p>
                    Complete a closed route to create a valid
                    territory on the map.
                  </p>
                </div>
              </div>

              <div className="hero-step">
                <div className="hero-step-icon">◇</div>
                <div>
                  <h3><em>03</em> CLAIM YOUR AREA</h3>
                  <p>
                    Capture the area, grow your footprint,
                    and compete to dominate the city.
                  </p>
                </div>
              </div>
            </div>

            <button
              className="scroll-indicator"
              onClick={() => navigate("leaderboard")}
            >
              SCROLL TO EXPLORE
              <span>↓</span>
            </button>
          </div>
        </section>

        {/* MOVING TICKER */}
        <div className="ticker">
          <div className="ticker-track">
            {Array.from({ length: 4 }, (_, index) => (
              <span key={index}>
                RUN <b>//</b> CAPTURE <b>//</b> DEFEND <b>//</b>{" "}
                CONQUER <b>//</b>
              </span>
            ))}
          </div>
        </div>

        {/* LEADERBOARD */}
        <section className="section elite-section" id="leaderboard">
          <div className="container">
            <div className="section-heading">
              <div>
                <div className="section-eyebrow">
                  THE CITY'S FINEST
                </div>
                <h2>
                  THE <em>ELITE.</em>
                </h2>
                <p>
                  The streets remember who owns them.
                  Make your mark.
                </p>
              </div>
            </div>

            <div className="elite-layout">
              <div className="podium">
                <PodiumCard runner={runners[1]} rank={2} />
                <PodiumCard runner={runners[0]} rank={1} />
                <PodiumCard runner={runners[2]} rank={3} />
              </div>

              <div className="rankings-panel">
                <div className="rankings-heading">
                  <h3>TOP RUNNERS ↗</h3>

                  <select
                    value={area}
                    onChange={(event) => setArea(event.target.value)}
                    aria-label="Filter runners by area"
                  >
                    {areas.map((item) => (
                      <option key={item}>{item}</option>
                    ))}
                  </select>
                </div>

                <div className="table-scroll">
                  <table>
                    <thead>
                      <tr>
                        <th>RANK</th>
                        <th>RUNNER</th>
                        <th>AREA</th>
                        <th>TERRITORY</th>
                      </tr>
                    </thead>
                    <tbody>
                      {filteredRunners.map((runner) => (
                        <tr key={runner.name}>
                          <td>
                            #{String(runners.indexOf(runner) + 1).padStart(2, "0")}
                          </td>
                          <td className="runner-name">
                            {runner.name}
                          </td>
                          <td>{runner.area}</td>
                          <td className="accent">
                            {runner.territory.toFixed(2)} km²
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* MAP */}
        <section className="section map-section" id="map">
          <div className="container map-layout">
            <div>
              <div className="section-eyebrow">
                EVERY BLOCK IS A BATTLEGROUND
              </div>
              <h2>
                YOUR CITY.
                <br />
                <em>YOUR TERRITORY.</em>
              </h2>
              <p>
                Explore Karachi, discover running routes,
                and see where the competition is heating up.
                Every closed loop is an opportunity to claim
                new ground.
              </p>
            </div>

            <div className="map-card">
              <div className="map-card-top">
                KARACHI / TERRITORY PREVIEW
                <span>● DEMO VIEW</span>
              </div>

              <img
                src="/Map%20.jpg"
                alt="Karachi territory map preview"
              />

              <div className="map-overlay">
                EXPLORE THE STREETS <Arrow />
              </div>
            </div>
          </div>
        </section>

        {/* COMPETITIONS */}
        <section
          className="section competition-section"
          id="competitions"
        >
          <div className="container">
            <div className="section-eyebrow">
              RIVALRY MAKES LEGENDS
            </div>
            <h2>
              ENTER THE <em>ARENA.</em>
            </h2>
            <p className="section-description">
              Challenge your limits, represent your neighbourhood,
              and rise through the rankings.
            </p>

            <div className="competition-grid">
              {competitions.map((competition, index) => (
                <article
                  className="competition-card"
                  key={competition.name}
                >
                  <div className="competition-number">
                    0{index + 1} / {competition.type}
                  </div>
                  <h3>{competition.name}</h3>
                  <p>{competition.description}</p>
                  <div className="competition-meta">
                    <span>{competition.date}</span>
                    <span>{competition.runners} RUNNERS</span>
                  </div>
                </article>
              ))}
            </div>
          </div>
        </section>

        {/* HOW IT WORKS */}
        <section className="section how-section" id="how-it-works">
          <div className="container">
            <div className="section-eyebrow">
              THE RULES OF THE STREETS
            </div>
            <h2>
              HOW TO <em>PLAY.</em>
            </h2>

            <div className="steps">
              {[
                {
                  number: "01",
                  icon: "↗",
                  title: "START YOUR RUN",
                  text: "Open the ILAQA mobile app and start tracking your run.",
                },
                {
                  number: "02",
                  icon: "◎",
                  title: "CLOSE THE LOOP",
                  text: "Run a closed loop around a real-world block or park.",
                },
                {
                  number: "03",
                  icon: "⚑",
                  title: "CLAIM YOUR ILAQA",
                  text: "A valid loop becomes claimed territory. Stay active to defend it.",
                },
              ].map((step) => (
                <article key={step.number}>
                  <span>{step.number}</span>
                  <div className="step-icon">{step.icon}</div>
                  <div>
                    <h3>{step.title}</h3>
                    <p>{step.text}</p>
                  </div>
                  <b>↗</b>
                </article>
              ))}
            </div>
          </div>
        </section>
      </main>

      <footer className="footer">
        <div className="container footer-inner">
          <strong>
            ILAQA<span className="accent">.</span>
          </strong>
          <span>THE CITY IS YOUR PLAYGROUND.</span>
          <span>© {new Date().getFullYear()} ILAQA</span>
        </div>
      </footer>
    </div>
  );
}
