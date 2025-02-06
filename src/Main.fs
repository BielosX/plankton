module Main

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open System
open System.Threading
open System.Threading.Tasks
open System.Collections.Concurrent
open System.Net.WebSockets
open System.IO
open System.Text.Json
open System.Collections.Generic
open System.IO.Pipes

let gameLoop (queue: ConcurrentQueue<string>) =
    task {
        let mutable result = ""
        while true do
            if queue.TryDequeue(&result) then
                printfn "Task received %s" result
            Thread.Sleep(1000)
    }

let handleFrame<'a> (webSocket: WebSocket): 'a =
    let serverStream = new AnonymousPipeServerStream(PipeDirection.Out)
    let clientStream = new AnonymousPipeClientStream(PipeDirection.In,serverStream.ClientSafePipeHandle)
    let receiveTask = task {
        let mutable endOfMessage = false
        let buffer = Array.zeroCreate 1024
        let arraySegment = ArraySegment<byte>(buffer)
        while not endOfMessage do
            let! receiveResult = webSocket.ReceiveAsync(arraySegment, CancellationToken.None) |> Async.AwaitTask
            printfn "Received %u bytes" receiveResult.Count
            serverStream.WriteAsync(arraySegment.Array, 0, receiveResult.Count) |> Async.AwaitTask |> ignore
            endOfMessage <- receiveResult.EndOfMessage
        serverStream.Dispose()
    }
    let parseTask = task {
        let! result = JsonSerializer.DeserializeAsync<'a>(clientStream).AsTask() |> Async.AwaitTask
        clientStream.Dispose()
        return result
    }
    Task.WaitAll(receiveTask, parseTask)
    parseTask.Result


[<EntryPoint>]
let main args =
    let app = WebApplication.Create(args)
    let queue = ConcurrentQueue<string>()
    let options = new WebSocketOptions()
    options.KeepAliveInterval <- TimeSpan.FromSeconds(10L)
    options.KeepAliveTimeout <- TimeSpan.FromSeconds(2L)
    app.UseWebSockets(options) |> ignore
    app.MapGet("/health", Func<string>(fun () -> "OK")) |> ignore
    app.Map("/ws", Func<HttpContext, _>(fun (context: HttpContext) -> 
        async {
            let! webSocket = context.WebSockets.AcceptWebSocketAsync() |> Async.AwaitTask
            let mutable shouldExit = false
            while not shouldExit do
                if webSocket.State = WebSocketState.Open then
                    let result = handleFrame<Dictionary<string,string>>(webSocket)
                    printfn "Test %s" (result["test"])
                else if webSocket.State = WebSocketState.Aborted || webSocket.State = WebSocketState.Closed then
                    shouldExit <- true
        }
        )) |> ignore
    let appTask = app.RunAsync()
    let gameTask = gameLoop queue
    Task.WaitAll(appTask, gameTask)
    0