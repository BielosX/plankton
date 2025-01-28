module Main

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open System
open System.Threading
open System.Threading.Tasks
open System.Collections.Concurrent

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
    app.UseWebSockets() |> ignore
    app.MapGet("/health", Func<string>(fun () -> "OK")) |> ignore
    app.Map("/ws", Func<HttpContext, _>(fun (context: HttpContext) -> 
        async {
            let buffer = Array.zeroCreate 1024
            let arraySegment = ArraySegment<byte>(buffer)
            let! webSocket = context.WebSockets.AcceptWebSocketAsync() |> Async.AwaitTask
            while true do
                webSocket.ReceiveAsync(arraySegment, CancellationToken.None) |> Async.AwaitTask |> ignore
                let result = System.Text.Encoding.UTF8.GetString(arraySegment)
                printfn "WebSocket received %s" result
                queue.Enqueue(result)
                Thread.Sleep(1000)
        }
        )) |> ignore
    let appTask = app.RunAsync()
    let gameTask = gameLoop queue
    Task.WaitAll(appTask, gameTask)
    0