import { useRef, useState } from "react";
import { Chess } from "chess.js";
import ChessBoard from "./ChessBoard";

const sessionId = crypto.randomUUID();

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

async function fetchEvaluation(fen, depth = 10) {
	const response = await fetch("/api/analysis/evaluate", {
		method: "POST",
		headers: {
			"Content-Type": "application/json",
			"X-Session-ID": sessionId
		},
		body: JSON.stringify({ fen, depth }),
	});
	if (!response.ok) throw new Error("Evaluation failed");
	return await response.json();
}

async function fetchContext(fen) {
	const response = await fetch("/api/analysis/context", {
		method: "POST",
		headers: { "Content-Type": "application/json" },
		body: JSON.stringify({ fen }),
	});
	if (!response.ok) throw new Error("Context analysis failed");
	return await response.json();
}

function resolveUciMove(fen, moveSan) {
	try {
		const tempChess = new Chess(fen);
		const moveDetails = tempChess.move(moveSan, { sloppy: true });
		if (!moveDetails) return null;
		return moveDetails.from + moveDetails.to + (moveDetails.promotion ?? "");
	} catch (e) {
		return null;
	}
}

// Helper: Determine the label (Best, Mistake, Blunder, etc.)
function classifyMove(loss, moveIndex, isEngineMove) {
	if (isEngineMove) {
		return moveIndex <= 4 ? "book" : "best";
	}
	if (moveIndex <= 4 && loss <= 30) return "book";
	if (loss <= 20) return "excellent";
	if (loss <= 50) return "good";
	if (loss <= 100) return "inaccuracy";
	if (loss <= 250) return "mistake";
	return "blunder";
}

async function fetchExplanation(context) {
	try {
		const response = await fetch("/api/coach/explain", {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify(context),
		});
		if (!response.ok) return "Coach error.";
		const data = await response.json();
		return data.explanation;
	} catch (e) {
		console.error("Coach failed:", e);
		return null;
	}
}

function App() {
	const chessRef = useRef(new Chess());

	const [sanMoves, setSanMoves] = useState([]);
	const [currentIndex, setCurrentIndex] = useState(0);
	const [board, setBoard] = useState(chessRef.current.board());
	const [fileName, setFileName] = useState(null);
	const [evaluations, setEvaluations] = useState({});
	const [explanation, setExplanation] = useState("");

	const handleFileChange = (e) => {
		const file = e.target.files?.[0];
		if (!file) return;

		setFileName(file.name);

		const reader = new FileReader();
		reader.onload = async (event) => {
			const pgnText = event.target.result;

			try {
				// 1. Use robust chess.js parser
				const tempChess = new Chess();
				tempChess.loadPgn(pgnText);

				// 2. Extract clean SAN moves (strips comments/variations automatically)
				const history = tempChess.history();

				// 3. Reset State
				chessRef.current = new Chess();
				setSanMoves(history);
				setCurrentIndex(0);
				setBoard(chessRef.current.board());
				setEvaluations({});

				// 4. Kick off the start position evaluation
				try {
					const startEval = await fetchEvaluation(chessRef.current.fen(), 10);
					setEvaluations({ 0: startEval });
					console.log("Eval for start:", startEval);
				} catch (e) {
					console.error("Start evaluation error:", e);
				}

			} catch (error) {
				console.error("PGN Parse Error:", error);
				alert("Invalid PGN file");
			}
		};

		// Read the file as text directly in the browser
		reader.readAsText(file);
	};

	// ---------------------------------------------------------
	// 1. Data Fetching Orchestrator (Parallelized)
	// ---------------------------------------------------------
	const fetchAnalysisData = async (fenBefore, playedMove, bestMove) => {
		// Prepare "After" state
		const playedChess = new Chess(fenBefore);
		playedChess.move(playedMove, { sloppy: true });
		const fenAfter = playedChess.fen();

		// Prepare "Best" state (if it exists)
		let bestMoveFen = null;
		if (bestMove) {
			const bestChess = new Chess(fenBefore);
			bestChess.move(bestMove, { sloppy: true });
			bestMoveFen = bestChess.fen();
		}

		// Fire all requests in parallel
		const promises = [
			fetchEvaluation(fenAfter, 12),      // 0: Played Eval (Truth)
			fetchContext(fenAfter)              // 1: Context (Facts)
		];

		if (bestMoveFen) {
			promises.push(fetchEvaluation(bestMoveFen, 12)); // 2: Best Eval
		}

		const results = await Promise.all(promises);

		return {
			playedEval: results[0],
			boardContext: results[1],
			bestEval: bestMoveFen ? results[2] : null
		};
	};

	// ---------------------------------------------------------
	// 2. The Main "Next" Function (Clean & Readable)
	// ---------------------------------------------------------
	const next = async () => {
		if (currentIndex >= sanMoves.length) return;

		const moveIndex = currentIndex + 1;
		const playedMove = sanMoves[currentIndex];
		const fenBefore = chessRef.current.fen();
		const sideToMove = chessRef.current.turn();

		// Step 1: Ensure we have the "Baseline" (Pre-move eval)
		let preEval = evaluations[moveIndex - 1];
		if (!preEval) {
			preEval = await fetchEvaluation(fenBefore, 12);
			setEvaluations(prev => ({ ...prev, [moveIndex - 1]: preEval }));
		}
		const bestMoveSan = preEval.bestMove;

		// Step 2: Fetch all analysis data in parallel
		const { playedEval, bestEval, boardContext } = await fetchAnalysisData(
			fenBefore,
			playedMove,
			bestMoveSan
		);

		// Step 3: Compute Logic (UCI resolution & Loss)
		const playedUci = resolveUciMove(fenBefore, playedMove);
		// Note: 'bestMoveSan' from backend is usually UCI (e.g. "e2e4"), check your API. 
		// If API returns SAN, use resolveUciMove on bestMoveSan too.
		const isEngineMove = bestMoveSan && playedUci && (bestMoveSan === playedUci);

		let loss = 0;
		if (bestEval && !isEngineMove) {
			loss = computeLoss(
				bestEval.evaluation.value,
				playedEval.evaluation.value,
				sideToMove, // Mover
				sideToMove  // Side before move
			);
		}

		// Step 4: Classification
		const label = classifyMove(loss, moveIndex, isEngineMove);

		// Step 5: Update React State (Board & Cache)
		chessRef.current.move(playedMove, { sloppy: true });
		setCurrentIndex(moveIndex);
		setBoard(chessRef.current.board());

		setEvaluations(prev => ({
			...prev,
			[moveIndex]: playedEval,
			[`context_${moveIndex}`]: boardContext,
			[`label_${moveIndex}`]: label,
			// ...
		}));

		// Step 6: Trigger AI Explanation (Restored Logic)
		if (label !== "book") {
			setExplanation("Thinking...");

			// Construct the context object (matches CoachContext DTO in C#)
			const contextPayload = {
				fen: fenBefore,
				moveSan: playedMove,
				bestMoveSan: bestMoveSan || "",
				label: label,
				scoreBefore: preEval?.evaluation?.value ?? 0,
				scoreAfter: playedEval?.evaluation?.value ?? 0,
				boardContext: boardContext // The holistic facts from C#
			};

			// Use your existing fetcher
			const text = await fetchExplanation(contextPayload);
			setExplanation(text);
		} else {
			setExplanation("Book move.");
		}
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
			<div style={{
				marginTop: "1rem",
				padding: "1rem",
				backgroundColor: "#333",
				borderRadius: "8px",
				minHeight: "60px"
			}}>
				<strong>Coach:</strong> {explanation}
			</div>
		</div>
	);
}

export default App;