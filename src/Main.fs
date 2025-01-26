module Main

open Microsoft.AspNetCore.Builder
open System

[<EntryPoint>]
let main args =
    let app = WebApplication.Create(args)
    app.UseWebSockets() |> ignore
    app.MapGet("/health", Func<string>(fun () -> "OK")) |> ignore
    app.Run()
    0