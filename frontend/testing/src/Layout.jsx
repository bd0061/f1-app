import { Link, NavLink } from "react-router-dom";

const Layout = ({ children }) => {
  const navigation = [
    { to: "/", label: "Home", end: true },
    { to: "/buy-ticket", label: "Buy Ticket" },
    { to: "/manage-ticket", label: "Manage Ticket" },
    { to: "/paddock-pass", label: "Paddock Pass" },
    { to: "/admin", label: "Administration" },
  ];

  return (
    <div className="app-shell">
      <div className="app-container">
        <header className="site-header">
          <Link className="site-brand" to="/">
            Raceo <span>Ticketing</span>
          </Link>
          <nav aria-label="Main navigation">
            <ul className="nav-list">
              {navigation.map((item) => (
                <li key={item.to}>
                  <NavLink
                    className={({ isActive }) =>
                      isActive ? "nav-link nav-link-active" : "nav-link"
                    }
                    end={item.end}
                    to={item.to}
                  >
                    {item.label}
                  </NavLink>
                </li>
              ))}
            </ul>
          </nav>
        </header>
        <main>{children}</main>
        <footer className="site-footer">
          Raceo Ticketing keeps race days, tickets, and paddock access in one
          place.
        </footer>
      </div>
    </div>
  );
};

export default Layout;
