import { useLocation, useNavigate, useParams } from "react-router-dom";
import { useEffect, useState } from "react";

const API = import.meta.env.VITE_API_BASE_URL || "";

export default function EditFiles() {
  const { id } = useParams(); // DB meeting id
  const location = useLocation();
  const navigate = useNavigate();
  const summary = location.state?.summary || "";
  
  const [generatedFiles, setGeneratedFiles] = useState([]);
  const [editingFiles, setEditingFiles] = useState([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!summary) {
      alert("Summary not found. Redirecting...");
      navigate(-1);
      return;
    }

    async function fetchFiles() {
      setLoading(true);
      setError(null);
      try {
        const res = await fetch(`${API}/api/external/generate-files`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ summary })
        });
        if (!res.ok) throw new Error(await res.text());
        const files = await res.json();
        const mappedFiles = [
          { name: "FunctionalDoc.txt", content: files[0]?.content || "" },
          { name: "Mockups.txt", content: files[1]?.content || "" },
          { name: "Markdown.md", content: files[2]?.content || "" }
        ];
        setGeneratedFiles(mappedFiles);
        setEditingFiles(mappedFiles.map(f => f.content));
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    }

    fetchFiles();
  }, [summary, navigate]);

  async function saveFiles() {
    setSaving(true);
    setError(null);
    try {
      const res = await fetch(`${API}/api/external/save-files`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          meetingId: id,
          files: generatedFiles.map((f, i) => ({
            name: f.name,
            content: editingFiles[i]
          }))
        })
      });
      if (!res.ok) throw new Error(await res.text());
      alert("Files updated successfully!");
      navigate(-1); // back to meeting detail
    } catch (err) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <p>Generating files...</p>;
  if (error) return <p style={{ color: "red" }}>{error}</p>;

  return (
    <div style={{ padding: "20px", maxWidth: "960px", margin: "auto" }}>
      <h2>Edit Generated Files</h2>
      {generatedFiles.map((file, idx) => (
        <div key={file.name} style={{ marginBottom: "24px" }}>
          <label style={{ fontWeight: 600 }}>{file.name}</label>
          <textarea
            value={editingFiles[idx]}
            onChange={e => {
              const newFiles = [...editingFiles];
              newFiles[idx] = e.target.value;
              setEditingFiles(newFiles);
            }}
            rows={10}
            style={{
              width: "100%",
              fontFamily: "monospace",
              fontSize: "1rem",
              borderRadius: "8px",
              padding: "10px"
            }}
          />
        </div>
      ))}
      <button
        onClick={saveFiles}
        disabled={saving}
        className="btn btn-success"
      >
        {saving ? "Saving..." : "Update Files"}
      </button>
    </div>
  );
}