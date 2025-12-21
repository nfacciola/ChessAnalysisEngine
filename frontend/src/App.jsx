import { useState } from "react";
import { Chessboard } from "react-chessboard";

function App() {
	const [file, setFile] = useState(null);

	const handleFileChange = async (e) => {
		const selectedFile = e.target.files?.[0];
		setFile(selectedFile);

		if (!selectedFile) return;

		const formData = new FormData();
		formData.append("pgnFile", selectedFile);

		try {
			const response = await fetch("/api/analysis/upload", {
				method: "POST",
				body: formData,
			});

			if (!response.ok) {
				console.error("Upload failed", response.statusText);
				return;
			}

			const metadata = await response.json();
			console.log("Uploaded file metadata:", metadata);
		} catch (err) {
			console.error("Upload error", err);
		}
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
