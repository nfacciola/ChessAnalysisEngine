import { useState } from "react";
import { Chessboard } from "react-chessboard";

function App() {
  const [file, setFile] = useState(null);

  const handleFileChange = (e) => {
    setFile(e.target.files[0]);
  };

  return (
    <div style={{ padding: "2rem", maxWidth: "900px", margin: "0 auto" }}>
      <h1>Chess Analysis Engine</h1>

      <div style={{ marginBottom: "1rem" }}>
        <input
          type="file"
          accept=".pgn"
          onChange={handleFileChange}
        />
        {file && <p>Selected: {file.name}</p>}
      </div>

      <Chessboard position="start" />
    </div>
  );
}

export default App;
