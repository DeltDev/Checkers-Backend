# Checkers API

REST API for the 10x10 international-draughts checkers backend (ASP.NET Core, .NET 10).

Interactive documentation is served by the app itself:

| What | URL |
| --- | --- |
| Swagger UI | `http://localhost:<port>/swagger` |
| OpenAPI JSON | `http://localhost:<port>/swagger/v1/swagger.json` |

The server binds to whatever `--urls` you pass (e.g. `dotnet run --project Checkers --urls http://localhost:5099`).
The Vite dev server and the frontend expect it on **http://localhost:5099**.

---

## Conventions

- **JSON** everywhere, `camelCase` property names.
- **Enums serialize as strings**: `"White"`, `"Ongoing"`, `"Capture"`, …
- **Coordinates** are 0-based `{ row, col }`. Row 0 is Black's back rank, row 9 is White's.
  `col` runs 0-9 left to right from White's point of view. Only squares where
  `row + col` is odd are playable (the dark squares).
- **Square numbers** (1-50) appear only as `number` on dark squares and inside the
  `notation` strings; they follow the board from the top-left dark square = 1.
- **Players**: White is always `playerId` **1**, Black is always `playerId` **2**.
- **Errors** are `{"message": "..."}` (type `ErrorResponse`), except rejected actions
  (409 on move/resign and `success: false` draw answers), which use `ActionResultResponse`.
- A rejected draw offer or an illegal move is **not** an exception: you get a normal
  response with `success: false` and a `message`.

---

## Endpoints

### `GET /health`

Liveness probe. No parameters.

| Code | Body |
| --- | --- |
| 200 | plain text `ok` |

---

### `POST /api/games` — create a game

Body (`CreateGameRequest`):

```json
{
  "whitePlayerName": "Alice",
  "blackPlayerName": "Bob"
}
```

`whitePlayerName` is required (blank falls back to `"Player 1"`); `blackPlayerName`
is optional (falls back to `"Player 2"`).

| Code | Body | Meaning |
| --- | --- | --- |
| 201 | `GameStateResponse` | Created; `Location: /api/games/{gameId}` header set |
| 400 | `ErrorResponse` | Body failed validation |
| 500 | `ErrorResponse` | Game could not be stored |

Response includes everything the client needs to start rendering: the `gameId`,
both `players` (ids 1 and 2), all 100 `squares` with the 40 starting pieces,
`turn: 1`, `turnColor: "White"`, `status: "Ongoing"`.

---

### `GET /api/games/{gameId}` — game state

No body. Returns the current `GameStateResponse`:

| Code | Body |
| --- | --- |
| 200 | `GameStateResponse` |
| 404 | `ErrorResponse` — `game not found` |

`GameStateResponse` fields: `gameId`, `turn` (ply counter), `turnColor`,
`status` (`Ongoing` / `WhiteWon` / `BlackWon` / `Draw`), `winnerId` (nullable),
`players`, `squares` (100 entries, light squares carry null occupant fields),
`history` (every applied move with `notation`).

---

### `GET /api/games/{gameId}/moves?playerId={1|2}` — legal moves

The source of truth for what the requesting player may do right now.

| Code | Body | Meaning |
| --- | --- | --- |
| 200 | `LegalMovesResponse` | Legal moves; **empty list** when it is not that player's turn or the game is over |
| 400 | `ErrorResponse` | `playerId query parameter is required` |
| 404 | `ErrorResponse` | Unknown game, or `player not in this game` |

Each `MoveResponse`:

```json
{
  "from": { "row": 6, "col": 1 },
  "to":   { "row": 5, "col": 0 },
  "path": [],
  "capturedPieceIds": [],
  "type": "Quiet",
  "color": "White",
  "notation": "[31]:[26]"
}
```

- Captures come back as **one** move carrying the **full jump route** in `path`
  (intermediate landing squares) plus every `capturedPieceIds` entry — a
  multi-jump is played in a single request.
- When captures are available, only the maximum-length captures are returned
  (the mandatory-capture rule with the longest chain).

---

### `POST /api/games/{gameId}/moves` — play a move

Body (`MakeMoveRequest`):

```json
{
  "playerId": 1,
  "from": { "row": 6, "col": 1 },
  "to":   { "row": 5, "col": 0 },
  "path": null
}
```

`path` is only needed for multi-jumps and must equal one of the `path` values
from `GET /moves`; for quiet moves send `null` or omit it. The move is validated
against the legal-move list, so any mismatch is rejected, never misapplied.

| Code | Body | Meaning |
| --- | --- | --- |
| 200 | `ActionResultResponse` | Move applied; `success: true`, `status` = new game status |
| 400 | `ErrorResponse` | Malformed coordinates or shapes |
| 404 | `ErrorResponse` | Unknown game or player |
| 409 | `ActionResultResponse` | Rejected with `message`: `"not this player's turn"`, `"illegal move"`, or `"game is finished"` |

A game can also end inside this call: the new `status` may be `WhiteWon`,
`BlackWon` or `Draw` (capture-less opponent, no legal moves, threefold
repetition, or the king-only streak rule).

---

### `POST /api/games/{gameId}/draw` — offer a draw

Body (`OfferDrawRequest`): `{ "playerId": 1 }`

| Code | Body | Meaning |
| --- | --- | --- |
| 200 | `ActionResultResponse` | Offer recorded — `success: true` |
| 200 | `ActionResultResponse` | Duplicate offer — `success: false`, `message: "draw offer rejected (finished game or duplicate)"` |
| 400 | `ErrorResponse` | Malformed input |
| 404 | `ErrorResponse` | Unknown game or player |

The game only becomes a `Draw` when **both** players have offered; each new
move clears the outstanding offers. There is no decline endpoint — declining
is done by simply moving.

---

### `POST /api/games/{gameId}/resign` — resign

Body (`ResignRequest`): `{ "playerId": 2 }`

| Code | Body | Meaning |
| --- | --- | --- |
| 200 | `ActionResultResponse` | Accepted; `status` flips to the opponent's win |
| 400 | `ErrorResponse` | Malformed input |
| 404 | `ErrorResponse` | Unknown game or player |
| 409 | `ActionResultResponse` | `message: "game already finished"` |

---

## Shared schemas

**`Position`** — `{ "row": 0, "col": 1 }`, 0-based, in-bounds values 0-9.

**`ActionResultResponse`** — `{ "success": bool, "message": string|null, "status": GameStatus }`.

**`ErrorResponse`** — `{ "message": string }`.

**`SquareResponse`** — `position`, `number` (1-50, dark only), `isDark`,
`occupantId` / `occupantColor` / `occupantType` (null on empty squares).

**`MoveResponse`** — `from`, `to`, `path`, `capturedPieceIds`, `type`
(`Quiet` | `Capture`), `color`, `notation` (`"[31]:[26]"`, `"x"`-separated for captures).

**Enums** — `PieceColor`: `White` | `Black`;
`PieceType`: `Man` | `King`;
`MoveType`: `Quiet` | `Capture`;
`GameStatus`: `Ongoing` | `WhiteWon` | `BlackWon` | `Draw`;
`SquareNumber`: `Square1` … `Square50`.

---

## Example session

```bash
BASE=http://localhost:5099

# 1. create
curl -s -X POST $BASE/api/games \
  -H "Content-Type: application/json" \
  -d '{"whitePlayerName":"Alice","blackPlayerName":"Bob"}'
# -> 201, note gameId + that White (id 1) moves first

# 2. what can White play?
curl -s "$BASE/api/games/<GAME_ID>/moves?playerId=1"

# 3. play the first listed move
curl -s -X POST $BASE/api/games/<GAME_ID>/moves \
  -H "Content-Type: application/json" \
  -d '{"playerId":1,"from":{"row":6,"col":1},"to":{"row":5,"col":0},"path":null}'

# 4. now Black's turn — repeat steps 2-3 with playerId 2

# 5. draw offer / resign
curl -s -X POST $BASE/api/games/<GAME_ID>/draw   -H "Content-Type: application/json" -d '{"playerId":2}'
curl -s -X POST $BASE/api/games/<GAME_ID>/resign -H "Content-Type: application/json" -d '{"playerId":2}'
```

---

## Architecture note

Every endpoint resolves its game (and the acting player) through the
`IGameProvider` service layer (`Services/GameProvider.cs`, returning
`GameAccess` / `PlayerAccess` results), so controllers stay free of
store lookups and the 404/500 wording lives in one place. The global
exception handler in `Program.cs` remains the final safety net and answers
anything unhandled with `500 {"message":"internal server error"}`.
