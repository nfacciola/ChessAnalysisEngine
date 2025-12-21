import { useRef, useState } from "react";
import { Chess } from "chess.js";
import ChessBoard from "./ChessBoard";

function App() {
	const chessRef = useRef(new Chess());

	const [sanMoves, setSanMoves] = useState(["e4", "e5", "Nf3"]);
	const [currentIndex, setCurrentIndex] = useState(0);
	const [board, setBoard] = useState(chessRef.current.board());

	const next = () => {
		if (currentIndex >= sanMoves.length) return;

		const move = sanMoves[currentIndex];
		const ok = chessRef.current.move(move, { sloppy: true });

		if (!ok) {
			console.error("Illegal move:", move);
			return;
		}

		setCurrentIndex((i) => i + 1);
		setBoard(chessRef.current.board());
	};

	const previous = () => {
		if (currentIndex === 0) return;

		chessRef.current = new Chess();
		for (let i = 0; i < currentIndex - 1; i++) {
			chessRef.current.move(sanMoves[i], { sloppy: true });
		}

		setCurrentIndex((i) => i - 1);
		setBoard(chessRef.current.board());
	};

	return (
		<div style={{ padding: "2rem", maxWidth: "900px", margin: "0 auto" }}>
			<h1>Chess Analysis Engine</h1>

			<div style={{ marginBottom: "1rem" }}>
				<button onClick={previous} disabled={currentIndex === 0}>
					Previous
				</button>
				<button
					onClick={next}
					disabled={currentIndex >= sanMoves.length}
					style={{ marginLeft: "0.5rem" }}
				>
					Next
				</button>
				<span style={{ marginLeft: "1rem" }}>
					Move {currentIndex} / {sanMoves.length}
				</span>
			</div>

			<ChessBoard board={board} />
		</div>
	);
}

export default App;
