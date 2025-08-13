import { BrowserRouter, Routes, Route, NavLink } from "react-router-dom";
import Home from "./pages/Home";
import MeetingList from "./MeetingList";
import MeetingDetail from "./MeetingDetail";
import SavedMeetings from "./SavedMeetings";


export default function App() {
  return (
    <BrowserRouter>
      <div className="app-root">
        <header className="app-header" role="banner">
          <div className="brand" tabIndex={-1}>
            <h1>Fireflies Dashboard</h1>
            <small>Meetings • Summaries</small>
          </div>

          <nav className="nav" role="navigation" aria-label="Primary">
            <NavLink to="/" className={({ isActive }) => (isActive ? "nav-link active" : "nav-link")} end>
              Home
            </NavLink>
            <NavLink to="/meetings" className={({ isActive }) => (isActive ? "nav-link active" : "nav-link")}>
              Meetings
            </NavLink>
            <NavLink to="/saved" className={({ isActive }) => (isActive ? "nav-link active" : "nav-link")}>
              Saved
            </NavLink>
          </nav>
        </header>

        <main className="app-main" role="main">
          <Routes>
            <Route path="/" element={<Home />} />
            <Route path="/meetings" element={<MeetingList />} />
            <Route path="/meetings/:id" element={<MeetingDetail />} />
            <Route path="/saved" element={<SavedMeetings />} />
           
          </Routes>
        </main>
      </div>
    </BrowserRouter>
  );
}
