﻿open System
open System.Threading
open NuGet.Common
open NuGet.Configuration
open NuGet.Credentials
open NuGet.Protocol.Plugins

let tryGetArg (name : string) (argv : string array) =
    let lowerName = name.ToLowerInvariant()
    argv
    |> Seq.tryFindIndex
           (fun s ->
           let arg = s.ToLowerInvariant()
           arg = $"/%s{lowerName}" || arg = $"-%s{lowerName}"
           || arg = $"--%s{lowerName}")
    |> Option.map (fun i ->
           if argv.Length > i + 1 then argv.[i + 1]
           else failwithf $"Argument for '%s{argv.[i]}' is missing")

let handleAzureCredentials (givenUri:Uri) =
    let path =
        PluginManager.Instance.FindAvailablePluginsAsync(CancellationToken.None)
        |> Async.AwaitTask |> Async.RunSynchronously
        |> Seq.map (fun p -> p.PluginFile.Path)
        |> Seq.find (fun path -> path.EndsWith("CredentialProvider.Microsoft.dll"))
    
    let message =
        let singleLine = Environment.NewLine
        let doubleLine = singleLine + singleLine
        let uri = givenUri.ToString()
        let command = $"dotnet %s{path} -uri %s{uri}"
        let instruction = $"%s{doubleLine}In order to authenticate to %s{uri} you must first run:%s{doubleLine}%s{command}%s{doubleLine}"
        let divider = "    **********************************************************************"
        doubleLine
        + "ATTENTION: User interaction required." + singleLine
        + divider
        + instruction
        + divider + doubleLine

    printf $"""
{{ "Username" : "",
"Password" : "",
"Message" : "%s{message}" }}"""
    exit 2

let impl argv =
    Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", "dotnet")
    let givenUri =
        match tryGetArg "uri" argv with
        | Some givenUriStr -> Uri givenUriStr
        | None -> failwithf "the -uri argument is required"

    let hasFlag (flag : string) =
        argv |> Seq.exists (fun s -> s.Equals(flag, StringComparison.InvariantCultureIgnoreCase))
    let nonInteractive = hasFlag "nonInteractive"
    let isRetry = hasFlag "isRetry"

    let plugins =
        SecurePluginCredentialProviderBuilder(pluginManager = PluginManager.Instance, canShowDialog = true, logger = NullLogger.Instance)
            .BuildAllAsync()
        |> Async.AwaitTask
        |> Async.RunSynchronously
        |> Seq.filter (not << isNull)

    let credentials =
        plugins
        |> Seq.choose (fun p ->
                let isAzureProvider =  p.Id.EndsWith("CredentialProvider.Microsoft.dll")
                p.GetAsync
                    (givenUri, Unchecked.defaultof<_>, CredentialRequestType.Unauthorized, "",
                     isRetry, isAzureProvider || nonInteractive, CancellationToken.None)
                |> Async.AwaitTask
                |> Async.RunSynchronously
                |> Option.ofObj
                |> Option.bind (fun c ->
                    let isAzureUri (uri:Uri) =
                        [
                            ".pkgs.vsts.me" // DevFabric
                            ".pkgs.codedev.ms" // DevFabric
                            ".pkgs.codeapp.ms" // AppFabric
                            ".pkgs.visualstudio.com" // Prod
                            ".pkgs.dev.azure.com" // Prod
                        ] |> List.exists (fun h -> uri.Host.EndsWith(h))
                        
                    if isAzureProvider && (isNull c.Credentials) && (isAzureUri givenUri) then
                        handleAzureCredentials givenUri
                        None 
                    else
                        c.Credentials |> Option.ofObj))
        |> Seq.tryHead

    match credentials with
    | Some credentials ->
        let credentials = credentials.GetCredential(givenUri, "Basic")
        printfn $"""
{{ "Username" : "%s{credentials.UserName}",
"Password" : "%s{credentials.Password}",
"Message" : "" }}"""
        0
    | None -> 1

[<EntryPoint>]
let main argv =
    try
        try
            impl argv
        with e ->
            eprintf $"Error: {e}"
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
