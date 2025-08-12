import { useNavigate } from "react-router-dom";

export default function Home(){
  const navigate = useNavigate();
  return (
    <div className="home">
      <div className="home-card">
        <h2>Welcome to Fireflies Dashboard</h2>
        <p>Click below to fetch meetings from your Fireflies account.</p>

        <div style={{display:"flex", gap:12, marginTop:18}}>
          <button className="btn btn-primary" onClick={() => navigate("/meetings")}>
            Fetch Meetings
          </button>
          <LinkToSaved />
        </div>
      </div>
    </div>
  );
}

function LinkToSaved(){
  return <a className="btn btn-ghost" href="/saved">View Saved</a>;
}
