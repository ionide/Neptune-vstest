module VSTestAdapter

open System
open Suave.Web
open Suave.Successful
open Neptun.VSTestAdapterLib
open Suave.WebPart
open Suave.Filters
open Suave.Operators
open Suave.Http
open System.Threading
open Newtonsoft.Json

let mutable scmPort = 0

let app (scm: SocketCommunicationManager) =
    choose [
        path "/getPort" >=> OK (string scmPort)
        path "/handshake" >=>
            request (fun _ ->
                scm.AcceptClientAsync().Wait()
                scm.WaitForClientConnection(Timeout.Infinite) |> ignore
                scm.ReceiveRawMessage() |> ignore
                OK "connected"
            )
        path "/request" >=> request (fun r ->
            let req = r.rawForm |> System.Text.Encoding.UTF8.GetString

            req |> scm.WriteAndFlushToChannel

            let res =
                if req.Contains "TestDiscovery.Start" then
                    let mutable isFinished = false
                    [|
                        while not isFinished do
                            let msg = scm.ReceiveRawMessage()
                            if msg.Contains "TestDiscovery.TestFound" then
                                yield msg
                            if msg.Contains "TestDiscovery.Completed" then
                               yield msg
                               isFinished <- true
                    |]
                elif req.Contains "TestExecution.RunAllWithDefaultHost" then
                    let mutable isFinished = false
                    [|
                        while not isFinished do
                            let msg = scm.ReceiveRawMessage()
                            if msg.Contains "TestExecution.StatsChange" then
                                yield msg
                            if msg.Contains "TestExecution.Completed" then
                               yield msg
                               isFinished <- true
                    |]
                elif req.Contains "TestExecution.CustomTestHostLaunchCallback" then
                    let mutable isFinished = false
                    [|
                        while not isFinished do
                            let msg = scm.ReceiveRawMessage()
                            if msg.Contains "TestExecution.StatsChange" then
                                yield msg
                            if msg.Contains "TestExecution.Completed" then
                               yield msg
                               isFinished <- true
                    |]
                elif req.Contains "Extensions.Initialize" then [||]
                else
                    [| scm.ReceiveRawMessage() |]

            OK (JsonConvert.SerializeObject res)

        )
    ]

[<EntryPoint>]
let main args =
    let port =
        try
            args.[0]
        with
        | _ -> "8088"
    try
        match UInt16.TryParse port with
        | (true, port)->
            let defaultBinding = defaultConfig.bindings.[0]
            let withPort = { defaultBinding.socketBinding with port = port }
            let serverConfig =
                { defaultConfig with bindings = [{ defaultBinding with socketBinding = withPort }]}
            let scm = SocketCommunicationManager()
            scmPort <- scm.HostServer()
            startWebServer serverConfig (app scm)
            0
        | _ ->
            -2
    with
    | _ -> -1
