module Main

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open System
open System.Threading
open System.Threading.Tasks
open System.Net.WebSockets
open System.Text
open System.Text.Json
open System.Collections.Generic
open System.IO.Pipes
open System.Threading.Channels

let gameLoop (requestChannel: ChannelReader<string>) (responseChannel: ChannelWriter<string>) =
    task {
        let mutable result = ""
        while true do
            if requestChannel.TryRead(&result) then
                do! responseChannel.WriteAsync(result).AsTask()
    }

let handleFrame<'a> (webSocket: WebSocket): Task<'a> =
    task {
        let serverStream = new AnonymousPipeServerStream(PipeDirection.Out)
        let clientStream = new AnonymousPipeClientStream(PipeDirection.In,serverStream.ClientSafePipeHandle)
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
            let! result = JsonSerializer.DeserializeAsync<'a>(clientStream).AsTask() |> Async.AwaitTask
            clientStream.Dispose()
            return result
        }
        Task.WaitAll(receiveTask, parseTask)
        return parseTask.Result
    }


let handleWebSocket (webSocket: WebSocket) (handler: WebSocket -> Task<unit>) =
    task {
        let mutable shouldExit = false
        while not shouldExit do
            if webSocket.State = WebSocketState.Open then
                do! handler webSocket
            else if webSocket.State = WebSocketState.Aborted || webSocket.State = WebSocketState.Closed then
                shouldExit <- true
    }

let receiveMessage (channel: ChannelWriter<string>) (webSocket: WebSocket) =
    task {
        let! result = handleFrame<Dictionary<string,string>>(webSocket) |> Async.AwaitTask
        do! channel.WriteAsync(result["test"]).AsTask()
    }

let sendMessage (channel: ChannelReader<string>) (webSocket: WebSocket) =
    task {
        let! value = channel.ReadAsync().AsTask() |> Async.AwaitTask
        let buffer = Encoding.UTF8.GetBytes(value)
        let arraySegment = ArraySegment<byte>(buffer)
        do! webSocket.SendAsync(arraySegment,  WebSocketMessageType.Text, true, CancellationToken.None)
    }

[<EntryPoint>]
let main args =
    let app = WebApplication.Create(args)
    let requestChannel = Channel.CreateUnbounded<string>()
    let responseChannel = Channel.CreateUnbounded<string>()
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
                let sendTask: Task<unit> = handleWebSocket webSocket (sendMessage responseChannel.Reader)
                let receiveTask: Task<unit> = handleWebSocket webSocket (receiveMessage requestChannel.Writer)
                Task.WhenAll(sendTask, receiveTask) |> Async.AwaitTask |> ignore
                return Results.NoContent()
        }
        )) |> ignore
    let appTask = app.RunAsync()
    let gameTask = gameLoop requestChannel.Reader responseChannel.Writer
    Task.WaitAll(appTask, gameTask)
    0