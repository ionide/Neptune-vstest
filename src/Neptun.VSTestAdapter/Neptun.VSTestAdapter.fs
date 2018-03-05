module VSTestAdapter

open System
open Suave.Web
open Suave.Successful
open Neptun.VSTestAdapterLib
open Suave.WebPart
open Suave.Filters
open Suave.Operators
open Suave.Http

let mutable scmPort = 0

let app (scm: SocketCommunicationManager) =
    choose [
        path "/getPort" >=> OK (string scmPort)
        path "/request" >=> request (fun r ->
            r.rawForm
            |> System.Text.Encoding.UTF8.GetString
            |> scm.WriteAndFlushToChannel

            scm.ReceiveRawMessage()
            |> OK

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
