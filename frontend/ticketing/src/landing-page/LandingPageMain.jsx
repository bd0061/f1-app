import { Link } from "react-router-dom";

const LandingPageMain = () => {
  return (
    <>
      <div
        id="hero-section"
        className="w-full flex flex-col items-center text-center pt-40 pb-28"
      >
        <h1 className="text-5xl md:text-6xl font-black text-white max-w-[50rem] leading-tight">
          Welcome to the future of racing technologies.
        </h1>

        <p className="mt-6 text-lg text-gray-400 max-w-[35rem]">
          Follow every Grand Prix, track live standings and secure your seat at
          the world&apos;s fastest circuits.
        </p>

        <div className="mt-10 flex flex-row flex-wrap justify-center gap-5">
          <a
            href="#race-information"
            className="border-2 border-blue-600 text-blue-500 px-8 py-3 rounded-md font-medium cursor-pointer hover:bg-blue-600 hover:text-white transition duration-300"
          >
            View races
          </a>

          <Link
            to="/buy-ticket"
            className="group bg-blue-600 text-white px-8 py-3 rounded-md font-medium cursor-pointer flex flex-row items-center gap-2 hover:bg-blue-700 transition duration-300"
          >
            Buy a pass
            <svg
              xmlns="http://www.w3.org/2000/svg"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
              className="w-5 h-5 transition-transform duration-300 group-hover:translate-x-1"
            >
              <path d="M5 12h14" />
              <path d="m12 5 7 7-7 7" />
            </svg>
          </Link>
        </div>
      </div>

      <div
        id="stats-section"
        className="w-full grid grid-cols-2 md:grid-cols-4 gap-y-10 border-y border-slate-800 py-12"
      >
        {[
          { value: "24", label: "Grand Prix" },
          { value: "20", label: "Drivers" },
          { value: "10", label: "Teams" },
          { value: "21", label: "Countries" },
        ].map((stat) => (
          <div key={stat.label} className="flex flex-col items-center">
            <span className="text-4xl font-black text-white">{stat.value}</span>
            <span className="mt-1 text-sm uppercase tracking-widest text-gray-500">
              {stat.label}
            </span>
          </div>
        ))}
      </div>

      <div id="features-section" className="w-full py-24">
        <h2 className="text-3xl font-black text-white text-center">
          Everything you need on race day
        </h2>
        <p className="mt-3 text-gray-400 text-center max-w-[35rem] mx-auto">
          One platform for schedules, tickets and live timing.
        </p>

        <div className="mt-14 grid grid-cols-1 md:grid-cols-3 gap-8">
          {[
            {
              title: "Live timing",
              text: "Lap-by-lap sector times and gaps, updated as the race unfolds.",
              path: "M12 6v6l4 2 M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20Z",
            },
            {
              title: "Circuit insights",
              text: "Track layouts, lap records and corner-by-corner breakdowns.",
              path: "M3 12a9 9 0 0 1 9-9 9 9 0 0 1 9 9 9 9 0 0 1-9 9 9 9 0 0 1-9-9Z M8 12a4 4 0 0 1 8 0 4 4 0 0 1-8 0Z",
            },
            {
              title: "Instant passes",
              text: "Buy a weekend pass in seconds and get it straight to your phone.",
              path: "M4 6h16v4a2 2 0 0 0 0 4v4H4v-4a2 2 0 0 0 0-4V6Z M12 8v8",
            },
          ].map((feature) => (
            <div
              key={feature.title}
              className="bg-slate-900 border border-slate-800 rounded-xl p-8 hover:border-blue-600 transition duration-300"
            >
              <svg
                xmlns="http://www.w3.org/2000/svg"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
                aria-hidden="true"
                className="w-10 h-10 text-blue-500"
              >
                <path d={feature.path} />
              </svg>
              <h3 className="mt-5 text-xl font-black text-white">
                {feature.title}
              </h3>
              <p className="mt-2 text-gray-400">{feature.text}</p>
            </div>
          ))}
        </div>
      </div>

      <div
        id="cta-section"
        className="w-full bg-slate-900 border border-slate-800 rounded-xl px-10 py-14 mb-24 flex flex-col md:flex-row items-center justify-between gap-8"
      >
        <div className="text-center md:text-left">
          <h2 className="text-3xl font-black text-white">
            Next up: Monaco Grand Prix
          </h2>
          <p className="mt-2 text-gray-400">
            Circuit de Monaco &middot; 67 laps &middot; Limited passes left.
          </p>
        </div>

        <a
          href="#race-information"
          className="bg-blue-600 text-white px-8 py-3 rounded-md font-medium cursor-pointer whitespace-nowrap hover:bg-blue-700 transition duration-300"
        >
          See the calendar
        </a>
      </div>
    </>
  );
};

export default LandingPageMain;
