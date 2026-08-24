import { Link, Route, Routes } from "react-router-dom";
import Layout from "./Layout.jsx";
import LandingPageMain from "./landing-page/LandingPageMain.jsx";
import RaceInfoSection from "./landing-page/RaceInfoSection.jsx";
import Admin from "./pages/Admin.jsx";
import BuyTicket from "./pages/BuyTicket.jsx";
import ManageTicket from "./pages/ManageTicket.jsx";
import PaddockPass from "./pages/PaddockPass.jsx";

function Home() {
  return (
    <>
      <LandingPageMain />
      <RaceInfoSection />
    </>
  );
}

function NotFound() {
  return (
    <section className="page-section compact-section">
      <p className="eyebrow">404</p>
      <h1>That page is off the racing line.</h1>
      <p>Use the navigation to return to ticketing.</p>
      <Link className="button button-primary" to="/">
        Return home
      </Link>
    </section>
  );
}

const App = () => {
  return (
    <Layout>
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/buy-ticket" element={<BuyTicket />} />
        <Route path="/manage-ticket" element={<ManageTicket />} />
        <Route path="/paddock-pass" element={<PaddockPass />} />
        <Route path="/admin" element={<Admin />} />
        <Route path="*" element={<NotFound />} />
      </Routes>
    </Layout>
  );
};

export default App;
