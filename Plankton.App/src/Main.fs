﻿module Main

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open System
open System.Threading
open System.Threading.Tasks
open System.Net.WebSockets
open System.Text.Json
open System.IO.Pipes
open System.Threading.Channels
open Plankton.Message
open Plankton.JsonMapping

let jsonSerializationOptions = JsonSerializerOptions()

let gameLoop (requestChannel: ChannelReader<GameAction>) (responseChannel: ChannelWriter<ServerMessage>) =
    task {
        let mutable result: GameAction = None
        while true do
            if requestChannel.TryRead(&result) then
                let message = Sync { angularVelocity = 0.0; playerPosition = 0.0, 0.0; velocity = 0.0, 0.0 }
                match result with
                    | KeyPressed _ -> do! responseChannel.WriteAsync(message).AsTask()
                    | KeyReleased _ -> do! responseChannel.WriteAsync(message).AsTask()
                    | _ -> do! responseChannel.WriteAsync(message).AsTask()
    }

let createStreams () =
    let serverStream = new AnonymousPipeServerStream(PipeDirection.Out)
    let clientStream = new AnonymousPipeClientStream(PipeDirection.In,serverStream.ClientSafePipeHandle)
    serverStream, clientStream


let handleFrame<'a> (webSocket: WebSocket): Task<Result<'a,string>> =
    task {
        let streams = createStreams ()
        let serverStream = fst streams
        let clientStream = snd streams
        let receiveTask = task {
            let mutable endOfMessage = false
            let buffer = Array.zeroCreate 1024
            let arraySegment = ArraySegment<byte>(buffer)
            while not endOfMessage do
                let! receiveResult = webSocket.ReceiveAsync(arraySegment, CancellationToken.None) |> Async.AwaitTask
                printfn "Received %u bytes" receiveResult.Count
                do! serverStream.WriteAsync(arraySegment.Array, 0, receiveResult.Count)
                endOfMessage <- receiveResult.EndOfMessage
            serverStream.Dispose()
        }
        let parseTask = task {
            try
                try
                    let! result = JsonSerializer.DeserializeAsync<'a>(clientStream, options = jsonSerializationOptions).AsTask() |> Async.AwaitTask
                    return Ok result
                with ex ->
                    return Error ex.Message 
            finally
                clientStream.Dispose()
        }
        Task.WaitAll(receiveTask, parseTask)
        return parseTask.Result
    }


let handleWebSocket (webSocket: WebSocket) (handler: WebSocket -> Task<Result<unit,string>>): Task<Result<unit,string>> =
    task {
        let mutable shouldExit = false
        let mutable result: Result<unit,string> = Ok(())
        while not shouldExit do
            if webSocket.State = WebSocketState.Open then
                let! handlerResult = handler webSocket |> Async.AwaitTask
                match handlerResult with
                    | Ok _ -> ()
                    | Error e ->
                        shouldExit <- true
                        result <- Error e
            else if webSocket.State = WebSocketState.Aborted || webSocket.State = WebSocketState.Closed then
                shouldExit <- true
        return result
    }

let receiveMessage (channel: ChannelWriter<GameAction>) (webSocket: WebSocket): Task<Result<unit,string>> =
    task {
        let! result = handleFrame<GameAction> webSocket |> Async.AwaitTask
        match result with
            | Ok action ->
                do! channel.WriteAsync(action).AsTask()
                return Ok(())
            | Error str -> return Error str
    }

let sendAsync (webSocket: WebSocket) (arraySegment: ArraySegment<byte>) (endOfMessage: bool) = 
    webSocket.SendAsync(arraySegment,  WebSocketMessageType.Text, endOfMessage, CancellationToken.None)

let sendMessage (channel: ChannelReader<ServerMessage>) (webSocket: WebSocket): Task<Result<unit,string>> =
    task {
        let streams = createStreams ()
        let serverStream = fst streams
        let clientStream = snd streams
        let! value = channel.ReadAsync().AsTask() |> Async.AwaitTask
        let serializeTask = task {
            do! JsonSerializer.SerializeAsync<ServerMessage>(serverStream, value, options = jsonSerializationOptions)
            serverStream.Dispose()
        }
        let sendTask = task {
            let bufferSize = 1024
            let buffer = Array.zeroCreate bufferSize
            let mutable shouldExit = false
            while not shouldExit do
                let! fetched = clientStream.ReadAsync(buffer, 0, bufferSize)
                let arraySegment = ArraySegment<byte>(buffer, 0, fetched)
                if fetched = 0 then
                    do! sendAsync webSocket arraySegment true
                    shouldExit <- true
                else
                    do! sendAsync webSocket arraySegment false
            clientStream.Dispose()
        }
        Task.WaitAll(sendTask, serializeTask)
        return Ok(())
    }

let shortenString (str: string, maxLength: int): string =
    if str.Length > maxLength then
        str.Substring(0, maxLength)
    else
        str.Substring(0, str.Length)

[<EntryPoint>]
let main args =
    jsonSerializationOptions.Converters.Add(FSharpTypesMapper())
    let app = WebApplication.Create(args)
    let requestChannel = Channel.CreateUnbounded<GameAction>()
    let responseChannel = Channel.CreateUnbounded<ServerMessage>()
    let options = new WebSocketOptions()
    options.KeepAliveInterval <- TimeSpan.FromSeconds(10L)
    options.KeepAliveTimeout <- TimeSpan.FromSeconds(2L)
    app.UseWebSockets(options) |> ignore
    app.MapGet("/health", Func<string>(fun () -> "OK")) |> ignore
    app.Map("/ws", Func<HttpContext, Async<IResult>>(fun (context: HttpContext) -> 
        async {
            if not context.WebSockets.IsWebSocketRequest then
                return Results.BadRequest "WebSocket expected"
            else
                let! webSocket = context.WebSockets.AcceptWebSocketAsync() |> Async.AwaitTask
                let sendTask: Task<Result<unit,string>> = handleWebSocket webSocket (sendMessage responseChannel.Reader)
                let receiveTask: Task<Result<unit,string>> = handleWebSocket webSocket (receiveMessage requestChannel.Writer)
                let! result = Task.WhenAny(sendTask, receiveTask) |> Async.AwaitTask
                match result.Result with
                    | Ok _ -> ()
                    | Error e ->
                        printfn "Closing connection with error %s" e
                        // Close Message statusDescription length max 123 bytes
                        webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, shortenString(e, 123), CancellationToken.None)
                            |> Async.AwaitTask |> ignore
                return Results.Empty
        }
        )) |> ignore
    let appTask = app.RunAsync()
    let gameTask = gameLoop requestChannel.Reader responseChannel.Writer
    Task.WaitAll(appTask, gameTask)
    0