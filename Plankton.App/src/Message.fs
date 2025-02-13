namespace Plankton.Message

open System.Text.Json.Serialization
open System.Text.Json

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


type GameKeyJsonConverter() =
    inherit JsonConverter<GameKey>()
    override this.Read (reader: byref<Utf8JsonReader>, _: System.Type, _: JsonSerializerOptions): GameKey = 
        if reader.TokenType <> JsonTokenType.String then
            raise (JsonException("String expected"))
        match reader.GetString() with
            | "Fire" -> Fire
            | "Start" -> Start
            | "Stop" -> Stop
            | "RotateLeft" -> RotateLeft
            | "RotateRight" -> RotateRight
            | _ -> raise (JsonException("Unexpected GameKey"))
    override this.Write (_: Utf8JsonWriter, _: GameKey, _: JsonSerializerOptions): unit = 
        raise (System.NotImplementedException())

type GameActionJsonConverter() =
    inherit JsonConverter<GameAction>()
    override this.Read (reader: byref<Utf8JsonReader>, _: System.Type, options: JsonSerializerOptions): GameAction = 
        let gameKeyConverter: JsonConverter<GameKey> = downcast options.GetConverter(typeof<GameKey>)
        if reader.TokenType <> JsonTokenType.StartObject then
            raise (JsonException("StartObject expected"))
        reader.Read() |> ignore
        if reader.TokenType <> JsonTokenType.PropertyName then
            raise (JsonException("Expected property"))
        let propertyName = reader.GetString()
        reader.Read() |> ignore
        let key = gameKeyConverter.Read(&reader, typeof<GameKey>, options)
        reader.Read() |> ignore
        if reader.TokenType <> JsonTokenType.EndObject then
            raise (JsonException("EndObject expected"))
        match propertyName with
            | "KeyPressed" -> KeyPressed(key)
            | "KeyReleased" -> KeyReleased(key)
            | "None" -> None
            | _ -> raise (JsonException("Unexpected GameAction"))
    override this.Write (_: Utf8JsonWriter, _: GameAction, _: JsonSerializerOptions): unit = 
            raise (System.NotImplementedException())