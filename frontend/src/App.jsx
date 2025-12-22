import { useRef, useState } from "react";
import { Chess } from "chess.js";
import ChessBoard from "./ChessBoard";

const BEST_EPSILON = 15; // cp tolerance for depth noise

function toWhiteCp(cp, sideToMove) {
	return sideToMove === "w" ? cp : -cp;
}

function computeLoss(bestCp, playedCp, mover, sideToMoveBefore) {
	// normalize to White POV
	const bestWhite = toWhiteCp(bestCp, sideToMoveBefore);
	const playedWhite = toWhiteCp(playedCp, sideToMoveBefore);

	// convert to mover POV
	const bestForMover = mover === "w" ? bestWhite : -bestWhite;
	const playedForMover = mover === "w" ? playedWhite : -playedWhite;

	return Math.abs(bestForMover - playedForMover);
}

function sameMove(uci, san) {
	if (!uci || !san) return false;

	// crude but effective for now
	if (san.length === 2) {
		// pawn move like e4
		return uci.endsWith(san);
	}

	// strip capture symbols
	const cleanSan = san.replace("x", "");

	return uci.endsWith(cleanSan.slice(-2));
}

async function fetchEvaluation(fen, depth = 10) {
	const response = await fetch("/api/analysis/evaluate", {
		method: "POST",
		headers: { "Content-Type": "application/json" },
		body: JSON.stringify({ fen, depth }),
	});
	if (!response.ok) throw new Error("Evaluation failed");
	return await response.json();
}

function App() {
	const chessRef = useRef(new Chess());

	const [sanMoves, setSanMoves] = useState([]);
	const [currentIndex, setCurrentIndex] = useState(0);
	const [board, setBoard] = useState(chessRef.current.board());
	const [fileName, setFileName] = useState(null);
	const [evaluations, setEvaluations] = useState({});

	const handleFileChange = async (e) => {
		const file = e.target.files?.[0];
		if (!file) return;

		setFileName(file.name);

		const formData = new FormData();
		formData.append("pgnFile", file); // MUST match backend parameter name

		try {
			const response = await fetch("/api/analysis/upload", {
				method: "POST",
				body: formData,
			});

			if (!response.ok) {
				console.error("Upload failed:", response.statusText);
				return;
			}

			const result = await response.json();

			// Reset chess state
			chessRef.current = new Chess();
			setSanMoves(result.sanMoves ?? []);
			setCurrentIndex(0);
			setBoard(chessRef.current.board());
			setEvaluations({});
			try {
				const startEval = await fetchEvaluation(chessRef.current.fen(), 10);
				setEvaluations({ 0: startEval });
				console.log("Eval for start:", startEval);
			} catch (e) {
				console.error("Start evaluation error:", e);
			}
		} catch (err) {
			console.error("Upload error:", err);
		}
	};

	const next = async () => {
		if (currentIndex >= sanMoves.length) return;

		const moveIndex = currentIndex + 1;
		const playedMove = sanMoves[currentIndex];

		const fenBefore = chessRef.current.fen();
		const sideToMoveBefore = chessRef.current.turn(); // 'w' or 'b'
		const mover = sideToMoveBefore;

		// 1) Evaluate BEFORE move (baseline)
		let preEval = evaluations[moveIndex - 1];
		if (!preEval) {
			preEval = await fetchEvaluation(fenBefore, 12);
			setEvaluations(prev => ({ ...prev, [moveIndex - 1]: preEval }));
		}

		const bestMove = preEval.bestMove;
		if (!bestMove) {
			console.warn("No best move from engine");
		}

		// 2) Evaluate AFTER PLAYED move
		const playedChess = new Chess(fenBefore);
		playedChess.move(playedMove, { sloppy: true });
		const playedEval = await fetchEvaluation(playedChess.fen(), 12);

		// 3) Evaluate AFTER BEST move
		let bestEval = null;
		if (bestMove) {
			const bestChess = new Chess(fenBefore);
			bestChess.move(bestMove, { sloppy: true });
			bestEval = await fetchEvaluation(bestChess.fen(), 12);
		}

		// 4) Compute loss + label
		let loss = null;
		let label = "unknown";

		if (bestEval) {
			loss = computeLoss(
				bestEval.evaluation.value,
				playedEval.evaluation.value,
				mover,
				sideToMoveBefore
			);
		}

		const isEngineMove = sameMove(bestMove, playedMove);

		if (moveIndex <= 6 && loss <= 30) {
			label = "book";
		} else if (isEngineMove && loss <= BEST_EPSILON) {
			label = "best";
		} else if (loss <= 20) {
			label = "excellent";
		} else if (loss <= 50) {
			label = "good";
		} else if (loss <= 100) {
			label = "inaccuracy";
		} else if (loss <= 250) {
			label = "mistake";
		} else {
			label = "blunder";
		}

		if (bestEval) {
			loss = computeLoss(
				bestEval.evaluation.value,
				playedEval.evaluation.value,
				mover,
				sideToMoveBefore
			);
		}

		// 5) Log sanity output
		console.log(
			`Move ${moveIndex}\n` +
			`Played: ${playedMove}\n` +
			`Best:   ${bestMove ?? "n/a"}\n` +
			`Eval(best):   ${bestEval?.evaluation.value ?? "n/a"} cp\n` +
			`Eval(played): ${playedEval.evaluation.value} cp\n` +
			`Loss: ${loss} cp\n` +
			`Label: ${label}`
		);

		// 6) Apply move to real board
		chessRef.current.move(playedMove, { sloppy: true });
		setCurrentIndex(moveIndex);
		setBoard(chessRef.current.board());

		// 7) Cache results
		setEvaluations(prev => ({
			...prev,
			[moveIndex]: playedEval,
			[`best_${moveIndex}`]: bestEval,
			[`label_${moveIndex}`]: label,
			[`loss_${moveIndex}`]: loss
		}));
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
				<input type="file" accept=".pgn" onChange={handleFileChange} />
				{fileName && <div>Loaded: {fileName}</div>}
			</div>

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
