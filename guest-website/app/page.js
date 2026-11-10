
'use client';

import { useState, useEffect } from 'react';

import {
  MapPin,
  Trophy,
  ShieldCheck,
  Footprints,
  Route,
  Flag,
  Sun,
  Moon,
  ArrowUpRight,
  Menu,
  X,
  Target,
  Lock,
  Flame,
  Search
} from 'lucide-react';

const runners = [
  {
    name: 'Abdul Rehman',
    handle: '@Abdul_runs',
    area: 'Malir Cantt',
    territory: 12.84,
    distance: 186.2,
    claims: 25,
    streak: 12
  },
  {
    name: 'Kumail A.',
    handle: '@Ameenruns',
    area: 'Nazimabad',
    territory: 11.20,
    distance: 174.8,
    claims: 21,
    streak: 9
  },
  {
    name: 'Abdul Wasay',
    handle: '@Wasay.moves',
    area: 'DHA',
    territory: 9.76,
    distance: 160.3,
    claims: 18,
    streak: 16
  },
  {
    name: 'Mustafa Imran',
    handle: '@mustafa.athlete',
    area: 'Clifton',
    territory: 8.94,
    distance: 142.1,
    claims: 16,
    streak: 7
  },
  {
    name: 'Noor F.',
    handle: '@noorf',
    area: 'Gulshan',
    territory: 8.41,
    distance: 138.4,
    claims: 14,
    streak: 11
  }
];

const territories = [
  {
    name: 'Malir Gaarison',
    owner: 'Abdul Rehman',
    area: 'Malir Cantt',
    size: 0.42,
    status: 'Shielded',
    color: '#bbff32',
    points: '15,50 33,43 39,65 30,82 13,74'
  },
  {
    name: 'Al-Farooq Ground',
    owner: 'Kumail A',
    area: 'Nazimabad',
    size: 0.38,
    status: 'Shielded',
    color: '#35d5c0',
    points: '47,20 68,12 75,35 65,51 49,43'
  },
  {
    name: 'Bukhari Park',
    owner: 'Abdul Wasay',
    area: 'DHA',
    size: 0.24,
    status: 'Vulnerable',
    color: '#ffb450',
    points: '78,57 93,51 97,73 87,84 75,77'
  },
  {
    name: 'Garden Loop',
    owner: 'Noor F.',
    area: 'Gulshan',
    size: 0.31,
    status: 'Shielded',
    color: '#8b9dff',
    points: '48,69 63,63 68,85 56,96 44,85'
  }
];

const competitions = [
  {
    name: 'Clifton Coastline',
    type: 'Zone Race',
    date: 'Oct 24 – Nov 02',
    runners: 84,
    status: 'Upcoming',
    description: 'Claim the most territory within the coastline zone.'
  },
  {
    name: 'Campus 250 Relay',
    type: 'Distance Race',
    date: 'Nov 05 – Nov 12',
    runners: 48,
    status: 'Upcoming',
    description: 'Every kilometer counts. Race your university rivals.'
  },
  {
    name: 'Karachi Sprint Week',
    type: 'Distance Race',
    date: 'Sep 12 – Sep 19',
    runners: 62,
    status: 'Completed',
    description: 'A city-wide challenge to cover the most distance.'
  }
];

const pages = [
  'Home',
  'Explore Map',
  'Leaderboard',
  'Competitions',
  'How It Works'
];

function TerritoryMap({ selected, setSelected }) {
  return (
    <div className="map-canvas">
      <div className="map-background" />

      <div className="map-caption">
        KARACHI · MAP PREVIEW
      </div>

      <svg
        className="territory-overlay"
        viewBox="0 0 100 100"
        preserveAspectRatio="none"
        role="img"
        aria-label="Illustrative territory boundaries over Karachi map preview"
      >
        {territories.map((territory, index) => (
          <polygon
            key={territory.name}
            points={territory.points}
            fill={territory.color}
            fillOpacity={selected === index ? 0.32 : 0.16}
            stroke={territory.color}
            strokeWidth={selected === index ? 0.8 : 0.48}
            vectorEffect="non-scaling-stroke"
            onClick={() => setSelected(index)}
            className="territory-polygon"
          />
        ))}
      </svg>

      <span className="map-label one">
        CLIFTON
      </span>

      <span className="map-label two">
        KARACHI
      </span>

      <span className="map-label three">
        SEAVIEW
      </span>

      <span className="map-bottom">
        ● PUBLIC TERRITORY OVERVIEW · DEMO
      </span>
    </div>
  );
}

function LeaderTable({ area, sort, setArea, setSort }) {
  const filtered = runners
    .filter((runner) => {
      return area === 'All Areas' || runner.area === area;
    })
    .sort((a, b) => {
      return b[sort] - a[sort];
    });

  return (
    <div className="panel leaderboard-panel">
      <div className="panel-top">
        <div>
          <span className="eyebrow">
            CITY RANKINGS
          </span>

          <h3>
            TOP RUNNERS
            <ArrowUpRight size={22} />
          </h3>
        </div>

        <div className="filters">
          <select
            aria-label="Filter runners by area"
            value={area}
            onChange={(event) => setArea(event.target.value)}
          >
            {['All Areas', 'Clifton', 'DHA', 'Gulshan', 'Nazimabad', 'Malir Cantt'].map((item) => (
              <option key={item}>
                {item}
              </option>
            ))}
          </select>

          {[
            ['territory', 'Territory'],
            ['distance', 'Distance'],
            // ['claims', 'Claims'],
            ['streak', 'Streak']
          ].map(([key, label]) => (
            <button
              key={key}
              className={sort === key ? 'active' : ''}
              onClick={() => setSort(key)}
            >
              {label}
            </button>
          ))}
        </div>
      </div>

      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th>RANK / RUNNER</th>
              <th>AREA</th>
              <th>AREA CLAIMED</th>
              <th>DISTANCE</th>
              <th>CLAIMS</th>
              <th>STREAK</th>
            </tr>
          </thead>

          <tbody>
            {filtered.map((runner, index) => (
              <tr key={runner.name}>
                <td>
                  <span className="rank">
                    {String(index + 1).padStart(2, '0')}
                  </span>

                  <span className="avatar">
                    {runner.name
                      .split(' ')
                      .map((part) => part[0])
                      .join('')}
                  </span>

                  <span>
                    <strong>
                      {runner.name}
                    </strong>

                    <small>
                      {runner.handle}
                    </small>
                  </span>
                </td>

                <td>{runner.area}</td>

                <td>
                  {runner.territory.toFixed(2)} km²
                </td>

                <td>
                  {runner.distance.toFixed(1)} km
                </td>

                <td>
                  {runner.claims}
                </td>

                <td className="lime">
                  {runner.streak} days
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {filtered.length === 0 && (
        <p>No runners in this area yet.</p>
      )}
    </div>
  );
}

export default function Page() {
  const [page, setPage] = useState('Home');
  const [dark, setDark] = useState(true);
  const [menu, setMenu] = useState(false);
  const [selected, setSelected] = useState(0);
  const [area, setArea] = useState('All Areas');
  const [sort, setSort] = useState('territory');
  const [status, setStatus] = useState('All');
  const [search, setSearch] = useState('');
  const [competitionFilter, setCompetitionFilter] = useState('All');

  useEffect(() => {
    const saved = localStorage.getItem('ilaqa-theme');

    if (saved) {
      setDark(saved === 'dark');
    }
  }, []);

  useEffect(() => {
    document.documentElement.dataset.theme = dark
      ? 'dark'
      : 'light';

    localStorage.setItem(
      'ilaqa-theme',
      dark ? 'dark' : 'light'
    );
  }, [dark]);

  function go(next) {
    setPage(next);
    setMenu(false);

    window.scrollTo({
      top: 0,
      behavior: 'smooth'
    });
  }

  const selectedTerritory = territories[selected];

  return (
    <>
      <div className="top-strip">
        ● THE CITY IS YOUR PLAYGROUND

        <span>/</span>

        KARACHI, PAKISTAN

        {/* <span className="top-right">
          FYP PREVIEW EXPERIENCE ↗
        </span> */}
      </div>

      <header className="header">
        <div className="container nav">
          <button
            className="brand"
            onClick={() => go('Home')}
          >
          <span className="brand-icon"> 
            <img
             src="/ilaqa-logo.jpg"
             alt="ILAQA Logo"
            className="logo-image"
           />
          </span>

            ILAQA<span className="lime">.</span>
          </button>

          <nav className={menu ? 'nav-links open' : 'nav-links'}>
            {pages.map((item) => (
              <button
                key={item}
                onClick={() => go(item)}
                className={page === item ? 'current' : ''}
              >
                {item}
              </button>
            ))}
          </nav>

          <div className="nav-actions">
            <button
              className="theme-toggle"
              aria-label={
                dark
                  ? 'Switch to light mode'
                  : 'Switch to dark mode'
              }
              onClick={() => setDark(!dark)}
            >
              {dark ? (
                <Sun size={21} />
              ) : (
                <Moon size={21} />
              )}
            </button>

            <button
              className="mobile-menu"
              onClick={() => setMenu(!menu)}
              aria-label="Toggle menu"
            >
              {menu ? <X /> : <Menu />}
            </button>
          </div>
        </div>
      </header>

      <main>
        {page === 'Home' && (
          <>
            <section className="hero container">
              <div>
                <span className="eyebrow dot">
                  THE WORLD IS YOUR ARENA
                </span>

                <h1>
                  RUN YOUR
                  <br />
                  CITY.
                  <br />

                  <span className="lime">
                    OWN YOUR
                    <br />
                    <i>ILAQA.</i>
                  </span>
                </h1>

                <p>
                  Every street is a challenge. Every loop is a
                  chance to claim your ground. Turn your everyday
                  run into a real-world game of territory, rivalry,
                  and glory.
                </p>

                <div className="button-row">
                  <button
                    className="btn primary"
                    onClick={() => go('Explore Map')}
                  >
                    EXPLORE THE MAP
                    <ArrowUpRight size={18} />
                  </button>

                  <button
                    className="btn outline"
                    onClick={() => go('How It Works')}
                  >
                    HOW IT WORKS
                  </button>
                </div>
              </div>

              <div className="hero-map">
                <TerritoryMap
                  selected={selected}
                  setSelected={setSelected}
                />

                <div className="floating-stat">
                  ◎ TERRITORY CAPTURED

                  <strong>+0.42 KM²</strong>
                </div>
              </div>
            </section>

            <section className="section container">
              <span className="eyebrow dot">
                THE GAME HAS CHANGED
              </span>

              <h2>NOT JUST A RUN. A TAKEOVER.</h2>

              <p className="section-intro">
                Your city becomes the playing field.
                The more you move, the more you own.
              </p>

              <div className="three-grid">
                {[
                  {
                    n: '01 / CLAIM',
                    icon: <MapPin />,
                    title: 'OWN THE STREETS',
                    body: 'Complete a valid closed loop around a real-world block or park to capture its territory.'
                  },
                  {
                    n: '02 / COMPETE',
                    icon: <Trophy />,
                    title: 'CLIMB THE RANKS',
                    body: 'Race other runners, conquer the leaderboards and make your mark across the city.'
                  },
                  {
                    n: '03 / DEFEND',
                    icon: <ShieldCheck />,
                    title: 'DEFEND YOUR ILAQA',
                    body: 'Stay active to keep your territory protected. Go inactive and rivals can take over.'
                  }
                ].map((card) => (
                  <article
                    className="feature-card"
                    key={card.n}
                  >
                    <span className="eyebrow">
                      {card.n}
                    </span>

                    <div className="feature-icon">
                      {card.icon}
                    </div>

                    <h3>{card.title}</h3>

                    <p>{card.body}</p>
                  </article>
                ))}
              </div>
            </section>

            <section className="section map-feature">
              <div className="container map-feature-inner">
                <div>
                  <span className="eyebrow dot">
                    THE BATTLEFIELD
                  </span>

                  <h2>
                    THE MAP
                    <br />
                    IS <span className="lime">ALIVE.</span>
                  </h2>

                  <p>
                    Explore the territories runners are claiming
                    across Karachi. Every shape tells a story of
                    a run, a challenge, and a victory.
                  </p>

                  <div className="legend">
                    <span>🟢 SHIELDED TERRITORY</span>
                    <span>🟠 VULNERABLE TERRITORY</span>
                    <span>🔵 ACTIVE CLAN AREA</span>
                  </div>

                  <button
                    className="btn primary"
                    onClick={() => go('Explore Map')}
                  >
                    OPEN TERRITORY MAP
                    <ArrowUpRight size={18} />
                  </button>
                </div>

                <TerritoryMap
                  selected={selected}
                  setSelected={setSelected}
                />
              </div>
            </section>

            <section className="section container">
              <div className="section-heading">
                <div>
                  <span className="eyebrow dot">
                    THE CITY'S FINEST
                  </span>

                  <h2>THE ONES TO BEAT.</h2>

                  <p>
                    Big miles. Bigger territories.
                    See who's setting the pace.
                  </p>
                </div>

                <button
                  className="btn outline"
                  onClick={() => go('Leaderboard')}
                >
                  FULL LEADERBOARD
                  <ArrowUpRight size={18} />
                </button>
              </div>

              <LeaderTable
                area={area}
                sort={sort}
                setArea={setArea}
                setSort={setSort}
              />
            </section>

            <section className="cta">
              <div className="container cta-inner">
                <div>
                  <span className="eyebrow">
                    YOUR CITY IS WAITING
                  </span>

                  <h2>
                    STOP SCROLLING.
                    <br />
                    <span>START CLAIMING.</span>
                  </h2>

                  <p>
                    Make every kilometer count.
                    Your next run could change the map.
                  </p>
                </div>

                <button
                  className="btn black"
                  onClick={() => go('How It Works')}
                >
                  DISCOVER ILAQA
                  <ArrowUpRight size={18} />
                </button>
              </div>
            </section>
          </>
        )}

        {page === 'Explore Map' && (
          <section className="section container page-section">
            <span className="eyebrow dot">
              THE BATTLEFIELD / KARACHI
            </span>

            <h1 className="page-title">
              EXPLORE <span className="lime">ILAQA.</span>
            </h1>

            <p className="section-intro">
              Discover the territory being claimed across the city.
              Select a highlighted zone for public details.
            </p>

            <div className="map-controls">
              <div className="search">
                <Search size={17} />

                <input
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Search territories..."
                />
              </div>

              <div className="filters">
                {['All', 'Shielded', 'Vulnerable'].map((item) => (
                  <button
                    key={item}
                    className={status === item ? 'active' : ''}
                    onClick={() => setStatus(item)}
                  >
                    {item}
                  </button>
                ))}
              </div>
            </div>

            <div className="explore-layout">
              <TerritoryMap
                selected={selected}
                setSelected={setSelected}
              />

              <aside>
                <div className="panel detail-panel">
                  <span className="eyebrow">
                    SELECTED TERRITORY
                  </span>

                  <h3>
                    <span className="lime">●</span>
                    {' '}
                    {selectedTerritory.name}
                  </h3>

                  <p>
                    Owned by {selectedTerritory.owner}
                  </p>

                  <div className="stat-grid">
                    <div>
                      <strong>
                        {selectedTerritory.size.toFixed(2)} km²
                      </strong>

                      <small>AREA CLAIMED</small>
                    </div>

                    <div>
                      <strong className="lime">
                        {selectedTerritory.status}
                      </strong>

                      <small>PROTECTION</small>
                    </div>
                  </div>

                  <p className="privacy-small">
                    <Lock size={15} />
                    Public territory boundary only.
                    No live GPS is displayed.
                  </p>
                </div>

                <div className="panel territory-list">
                  <span className="eyebrow">
                    NEARBY TERRITORIES
                  </span>

                  {territories
                    .map((territory, index) => ({
                      territory,
                      index
                    }))
                    .filter(({ territory }) => {
                      const matchesStatus =
                        status === 'All' ||
                        territory.status === status;

                      const matchesSearch =
                        `${territory.name} ${territory.area}`
                          .toLowerCase()
                          .includes(search.toLowerCase());

                      return matchesStatus && matchesSearch;
                    })
                    .map(({ territory, index }) => (
                      <button
                        key={territory.name}
                        className={
                          selected === index ? 'selected' : ''
                        }
                        onClick={() => setSelected(index)}
                      >
                        <span style={{ color: territory.color }}>
                          ●
                        </span>

                        <span>
                          <strong>
                            {territory.name}
                          </strong>

                          <small>
                            {territory.area} ·{' '}
                            {territory.size.toFixed(2)} km²
                          </small>
                        </span>

                        <ArrowUpRight size={15} />
                      </button>
                    ))}
                </div>
              </aside>
            </div>

            <div className="privacy-banner">
              <Lock size={20} />

              <div>
                <strong>PRIVACY FIRST. ALWAYS.</strong>

                <p>
                  Only public territory polygons and statistics
                  appear here. Real-time GPS coordinates and
                  private routes are never exposed.
                </p>
              </div>
            </div>

            <p className="demo-note">
              Map is a visual demo placeholder. Add a licensed
              Karachi map screenshot as{' '}
              <code>public/karachi-map.jpg</code> to display
              a real map background. Territory positions
              are illustrative.
            </p>
          </section>
        )}

        {page === 'Leaderboard' && (
          <section className="section container page-section">
            <span className="eyebrow dot">
              RUNNERS / RANKINGS
            </span>

            <h1 className="page-title">
              THE <span className="lime">LEADERBOARD.</span>
            </h1>

            <p className="section-intro">
              See who's taking over the city.
              Filter by Karachi area and ranking category.
            </p>

            <div className="podium">
              {[
                runners[1],
                runners[0],
                runners[2]
              ].map((runner, index) => (
                <div
                  key={runner.name}
                  className={
                    'podium-card ' +
                    (index === 1 ? 'winner' : '')
                  }
                >
                  <span className="podium-rank">
                    #
                    {index === 1 ? 1 : index === 0 ? 2 : 3}
                  </span>

                  {index === 1 && (
                    <Trophy className="lime" />
                  )}

                  <div className="avatar large">
                    {runner.name
                      .split(' ')
                      .map((part) => part[0])
                      .join('')}
                  </div>

                  <strong>
                    {runner.name}
                  </strong>

                  <small>
                    {runner.handle}
                  </small>

                  <b>
                    {runner.territory.toFixed(2)}{' '}
                    <small>km²</small>
                  </b>
                </div>
              ))}
            </div>

            <LeaderTable
              area={area}
              sort={sort}
              setArea={setArea}
              setSort={setSort}
            />
          </section>
        )}

        {page === 'Competitions' && (
          <section className="section container page-section">
            <span className="eyebrow dot">
              THE CITY CHALLENGES
            </span>

            <h1 className="page-title">
              RACE FOR <span className="lime">GLORY.</span>
            </h1>

            <p className="section-intro">
              Every competition is another chance to prove who
              owns the streets. Competitions can only be joined
              through the ILAQA mobile app.
            </p>

            <div className="filters competition-filters">
              {['All', 'Upcoming', 'Completed'].map((item) => (
                <button
                  key={item}
                  className={
                    competitionFilter === item ? 'active' : ''
                  }
                  onClick={() => setCompetitionFilter(item)}
                >
                  {item}
                </button>
              ))}
            </div>

            <div className="competition-grid">
              {competitions
                .filter((competition) => {
                  return (
                    competitionFilter === 'All' ||
                    competition.status === competitionFilter
                  );
                })
                .map((competition, index) => (
                  <article
                    className="competition-card"
                    key={competition.name}
                  >
                    <div className="competition-art">
                      <span>
                        {competition.type.toUpperCase()}
                      </span>

                      <span>
                        {competition.status.toUpperCase()}
                      </span>

                      <Target
                        size={95}
                        strokeWidth={1.1}
                      />

                      <b>
                        0{index + 1}
                      </b>
                    </div>

                    <div className="competition-body">
                      <span className="eyebrow">
                        ◷ {competition.date.toUpperCase()}
                      </span>

                      <h3>
                        {competition.name}
                      </h3>

                      <p>
                        {competition.description}
                      </p>

                      <div className="competition-meta">
                        <span>
                          ♙ {competition.runners} RUNNERS
                        </span>

                        <span>
                          {competition.status.toUpperCase()}
                        </span>
                      </div>

                      <div className="view-only">
                        VIEW ONLY · JOIN IN THE MOBILE APP
                      </div>
                    </div>
                  </article>
                ))}
            </div>
          </section>
        )}

        {page === 'How It Works' && (
          <section className="section container page-section">
            <span className="eyebrow dot">
              THE RULES OF THE STREETS
            </span>

            <h1 className="page-title">
              HOW TO <span className="lime">PLAY.</span>
            </h1>

            <p className="section-intro">
              Three simple moves. One city-wide game.
            </p>

            <div className="steps">
              {[
                {
                  n: '01',
                  icon: <Footprints />,
                  title: 'START YOUR RUN',
                  text: 'Open the ILAQA mobile app and start tracking. Your phone records your running route privately.'
                },
                {
                  n: '02',
                  icon: <Route />,
                  title: 'CLOSE THE LOOP',
                  text: 'Run a real enclosed loop around a block or park. Our backend checks the route before accepting a claim.'
                },
                {
                  n: '03',
                  icon: <Flag />,
                  title: 'CLAIM YOUR ILAQA',
                  text: 'Valid loops become territory polygons. Stay active to keep your three-day ownership shield.'
                }
              ].map((step) => (
                <div
                  className="step"
                  key={step.n}
                >
                  <span>
                    {step.n}
                  </span>

                  <div className="step-icon">
                    {step.icon}
                  </div>

                  <div>
                    <h3>
                      {step.title}
                    </h3>

                    <p>
                      {step.text}
                    </p>
                  </div>

                  <ArrowUpRight className="lime" />
                </div>
              ))}
            </div>

            <div className="three-grid info-grid">
              {[
                {
                  icon: <ShieldCheck />,
                  title: 'FAIR PLAY, ALWAYS.',
                  body: 'Suspicious GPS jumps, unrealistic speeds and step mismatches are checked to help keep competitions fair.'
                },
                {
                  icon: <Lock />,
                  title: 'YOUR LOCATION STAYS YOURS.',
                  body: 'The public website never shows your live coordinates or raw running route.'
                },
                {
                  icon: <Flame />,
                  title: 'SHOW UP. STAY ON TOP.',
                  body: 'Keep your territory shield active by returning regularly. Inactive territories can become vulnerable.'
                }
              ].map((card) => (
                <article
                  className="feature-card"
                  key={card.title}
                >
                  <div className="lime">
                    {card.icon}
                  </div>

                  <h3>
                    {card.title}
                  </h3>

                  <p>
                    {card.body}
                  </p>
                </article>
              ))}
            </div>

            <div className="mini-cta">
              <h2>
                READY TO MAKE YOUR MARK?
              </h2>

              <button
                className="btn black"
                onClick={() => go('Explore Map')}
              >
                EXPLORE THE CITY
                <ArrowUpRight size={18} />
              </button>
            </div>
          </section>
        )}
      </main>

      <footer className="footer">
        <div className="container footer-grid">
          <div>
            <button
             className="brand"
             onClick={() => go('Home')}
            >
             <span className="brand-icon">
               <img
                 src="/ilaqa-logo.jpg"
                 alt="ILAQA Logo"
                className="logo-image"
               />
            </span>

               ILAQA<span className="lime">.</span>
            </button>


            <p>
              Turn your city into your playground.
              <br />
              Run the streets. Own your ILAQA.
            </p>
          </div>

          <div>
            <span className="eyebrow">
              EXPLORE
            </span>

            {[
              'Explore Map',
              'Leaderboard',
              'Competitions'
            ].map((item) => (
              <button
                key={item}
                onClick={() => go(item)}
              >
                {item}
              </button>
            ))}
          </div>

          <div>
            <span className="eyebrow">
              ILAQA
            </span>

            {[
              'How It Works',
              'Home'
            ].map((item) => (
              <button
                key={item}
                onClick={() => go(item)}
              >
                {item}
              </button>
            ))}
          </div>

          <div>
            <span className="eyebrow dot">
              MADE FOR THE STREETS
            </span>

            <h3>
              EVERY BLOCK
              <br />
              IS UP FOR GRABS.
            </h3>
          </div>
        </div>

        <div className="container footer-bottom">
          <span>
            © 2026 ILAQA. FINAL YEAR PROJECT PREVIEW.
          </span>

          <span>
            BUILT IN KARACHI ✳ MADE TO MOVE.
          </span>
        </div>
      </footer>
    </>
  );
}
