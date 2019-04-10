open System
open System.Threading
open NuGet.Common
open NuGet.Configuration
open NuGet.Credentials

let tryGetArg (name : string) (argv : string array) =
    let lowerName = name.ToLowerInvariant()
    argv
    |> Seq.tryFindIndex
           (fun s ->
           let arg = s.ToLowerInvariant()
           arg = sprintf "/%s" lowerName || arg = sprintf "-%s" lowerName
           || arg = sprintf "--%s" lowerName)
    |> Option.map (fun i ->
           if argv.Length > i + 1 then argv.[i + 1]
           else failwithf "Argument for '%s' is missing" argv.[i])

let impl argv =
    Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", "dotnet")
    let givenUri =
        match tryGetArg "uri" argv with
        | Some givenUriStr -> System.Uri givenUriStr
        | None -> failwithf "the -uri argument is required"

    let plugins =
        SecurePluginCredentialProviderBuilder(pluginManager = NuGet.Protocol.Core.Types.PluginManager.Instance, canShowDialog = true, logger = NullLogger.Instance)
            .BuildAllAsync()
        |> Async.AwaitTask
        |> Async.RunSynchronously :> seq<_>
        |> Seq.filter (not << isNull)

    let credentials =
        plugins
        |> Seq.choose (fun p ->
               (p.GetAsync
                    (givenUri, Unchecked.defaultof<_>, CredentialRequestType.Unauthorized, "", false,
                     false, CancellationToken.None)
                |> Async.AwaitTask
                |> Async.RunSynchronously).Credentials
               |> Option.ofObj)
        |> Seq.tryHead

    match credentials with
    | Some credentials ->
        let credentials = credentials.GetCredential(givenUri, "Basic")
        printfn """
{ "Username" : "%s",
"Password" : "%s",
"Message"  : "" }""" credentials.UserName credentials.Password
        0
    | None -> 1

[<EntryPoint>]
let main argv =
    try
        try
            impl argv
        with e ->
            eprintf "Error: %O" e
            137
    finally
        Console.Out.Flush()
        Console.Error.Flush()
//    // Example of talking to lower API, if this were to move into Paket I think we would drop down to this
//    let credProviderPath = "/Users/stuart/Downloads/Microsoft.NetCore2.NuGet.CredentialProvider (1)/plugins/netcore/CredentialProvider.Microsoft"
//    let startInfo = ProcessStartInfo(
//                        "dotnet",
//                        Arguments = credProviderPath + "/CredentialProvider.Microsoft.dll",
//                        UseShellExecute = false,
//                        RedirectStandardError = false,
//                        RedirectStandardInput = true,
//                        RedirectStandardOutput = true,
//                        StandardOutputEncoding = UTF8Encoding(encoderShouldEmitUTF8Identifier = false)
//                    )
//
//    let proc = Process.Start(startInfo)
//    proc.Start() |> ignore
//    let encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier = false)
//    let standardInput = new StreamReader(Console.OpenStandardInput(), encoding)
//    let standardOutput = new StreamWriter(Console.OpenStandardOutput(), encoding)
//
//    use sender = new Sender(standardOutput)
//    use receiver = new StandardInputReceiver(standardInput)
//
//    sender.Connect()
//    receiver.Connect()
//    let message = Message("blah", MessageType.Request, MessageMethod.Handshake)
//    sender.SendAsync(message, CancellationToken.None)
//    |> Async.AwaitTask
//    |> Async.RunSynchronously
//
//    0 // return an integer exit code
