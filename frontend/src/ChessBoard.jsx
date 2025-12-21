import "./ChessBoard.css";

// Load all piece images in /src/assets/pieces
const pieceImages = import.meta.glob("./assets/pieces/*.png", {
    eager: true,
    import: "default",
});

// Convert chess.js piece -> file key like "wp", "bk"
function pieceKey(piece) {
    if (!piece) return null;
    return `${piece.color}${piece.type}`; // w + p/n/b/r/q/k
}

// Resolve "wp" -> actual image url
function getPieceSrc(key) {
    if (!key) return null;
    const path = `./assets/pieces/${key}.png`;
    return pieceImages[path] ?? null;
}

export default function ChessBoard({ board, orientation = "white" }) {
    const ranks = orientation === "white" ? board : [...board].reverse();

    return (
        <div className="board">
            {ranks.map((rank, rankIndex) => {
                const files = orientation === "white" ? rank : [...rank].reverse();

                return files.map((piece, fileIndex) => {
                    const isLight = (rankIndex + fileIndex) % 2 === 0;
                    const key = pieceKey(piece);
                    const src = getPieceSrc(key);

                    return (
                        <div
                            key={`${rankIndex}-${fileIndex}`}
                            className={`square ${isLight ? "light" : "dark"}`}
                        >
                            {src && <img className="piece" src={src} alt={key} />}
                        </div>
                    );
                });
            })}
        </div>
    );
}
