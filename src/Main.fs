module Main

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open System
open System.Threading
open System.Threading.Tasks
open System.Collections.Concurrent
open System.Net.WebSockets

let gameLoop (queue: ConcurrentQueue<string>) =
    task {
        let mutable result = ""
        while true do
            if queue.TryDequeue(&result) then
                printfn "Task received %s" result
            Thread.Sleep(1000)
    }

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
            let buffer = Array.zeroCreate 16
            let arraySegment = ArraySegment<byte>(buffer)
            let! webSocket = context.WebSockets.AcceptWebSocketAsync() |> Async.AwaitTask
            let mutable shouldExit = false
            while not shouldExit do
                if webSocket.State = WebSocketState.Open then
                    let! receiveResult = webSocket.ReceiveAsync(arraySegment, CancellationToken.None) |> Async.AwaitTask
                    if receiveResult.EndOfMessage then
                        printfn "Received end of message"
                    else
                        printfn "There are more bytes to read"
                    let result = System.Text.Encoding.UTF8.GetString(arraySegment.Array, 0, receiveResult.Count)
                    printfn "WebSocket received %s" result
                    queue.Enqueue(result)
                else if webSocket.State = WebSocketState.Aborted || webSocket.State = WebSocketState.Closed then
                    printfn "WebSocket Closed or Aborted"
                    shouldExit <- true
                Thread.Sleep(1000)
        }
        )) |> ignore
    let appTask = app.RunAsync()
    let gameTask = gameLoop queue
    Task.WaitAll(appTask, gameTask)
    0