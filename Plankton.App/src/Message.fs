namespace Plankton.Message

type GameKey =
    | Fire
    | Start
    | Stop
    | RotateLeft
    | RotateRight

type GameAction =
    | KeyPressed of key: GameKey
    | KeyReleased of key: GameKey
    | None

type SyncMessage = { playerPosition: float*float; velocity: float*float; angularVelocity: float }

type ServerMessage =
    | Sync of state: SyncMessage
