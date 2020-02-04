module LPEditor.App

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Giraffe.GoodRead
open Microsoft.Extensions.Hosting
open System.Reflection
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open System.Diagnostics
open System.Text.RegularExpressions

// ---------------------------------
// Models
// ---------------------------------
[<CLIMutable>]
type Message =
    {
        Text : string
    }
[<CLIMutable>]
type Chapter =
    {
        Filename: string
        Content: string
    }

// ---------------------------------
// Views
// ---------------------------------

module Views =
    open GiraffeViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "LPEditor" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/main.css" ]
            ]
            body [] content
        ]

    let partial () =
        h1 [] [ encodedText "LPEditor" ]

    let index (model : Message) =
        [
            partial()
            p [] [ encodedText model.Text ]
        ] |> layout

    let editorPage =
        html [] [
            head [] [
                link [_href "/editor.css"; _type "text/css"; _rel "stylesheet"]
            ]
            body [] [
                div [_id "container"] []
                div [_id "side-panel"] [
                    form [_id "doc-info"] [
                        div [_class "form-group"] [
                            label [_for "topic"] [ encodedText "Topic" ]
                            input [_id "topic"; _name "topic"; _type "text"]
                        ]
                        // div [_class "form-group"] [
                        //     label [_for "section"] [ encodedText "Section" ]
                        //     input [_id "section"; _type "text"]
                        // ]
                        div [_class "form-group"] [
                            label [] [ encodedText "Language" ]
                            div [] [
                                input [_id "lang_en"; _name "lang"; _type "radio"; _value "en"]
                                label [_for "lang_en"] [ encodedText "English" ]
                                input [_id "lang_cn"; _name "lang"; _type "radio"; _value "cn"]
                                label [_for "lang_cn"] [ encodedText "Chinese" ]
                            ]
                        ]
                        div [_class "form-group"] [
                            label [_for "order"] [ encodedText "Next Chapter Order" ]
                            input [_id "order"; _name "order"; _type "number"]
                        ]
                    ]
                ]
                script [_src "./app.bundle.js"] []
            ]
        ]

// ---------------------------------
// Web app
// ---------------------------------

let indexHandler (name : string) =
    let greetings = sprintf "Hello %s, from Giraffe!" name
    let model     = { Text = greetings }
    let view      = Views.index model
    htmlView view

let deliverTextFile (txtfile:string) = 
    use sr = new StreamReader(txtfile)
    negotiate { Text= sr.ReadToEnd() }

let webApp (txtfile:string) =
    choose [
        GET >=>
            choose [
                route "/" >=> indexHandler "world"
                routef "/hello/%s" indexHandler
                route "/editor/content" >=> deliverTextFile txtfile
                route "/editor" >=> htmlView Views.editorPage
            ]
        POST >=> 
            choose [
                route "/editor/content" >=> bindModel None (fun (s:string) -> 
                    use sw = new StreamWriter(txtfile)
                    sw.Write s
                    sw.Flush()
                    Successful.OK "saved")
                route "/editor/content/chapter" >=> bindModel None (fun (chapter:Chapter) ->
                    let d = [|Path.GetDirectoryName txtfile; Path.GetFileNameWithoutExtension txtfile|] |> Path.Combine
                    if Directory.Exists d |> not then
                        Directory.CreateDirectory d |> ignore
                    let chapterFile = Path.Combine(d, sprintf "%s.txt" chapter.Filename)
                    use sw = new StreamWriter(chapterFile)
                    sw.Write chapter.Content
                    sw.Flush()
                    Successful.OK "saved")
            ]
        PUT >=>
            choose [
                route "/editor/shutdown" >=> 
                Require.services(fun (hostApp:IHostApplicationLifetime) ->
                    hostApp.StopApplication()
                    Successful.OK "app shut down")
            ]
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder.WithOrigins("http://localhost:8080")
           .AllowAnyMethod()
           .AllowAnyHeader()
           |> ignore

let configureApp (txtfile:string) (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IHostEnvironment>()
    (match env.IsDevelopment() with
    | true  -> app.UseDeveloperExceptionPage()
    | false -> app.UseGiraffeErrorHandler errorHandler)
        .UseHttpsRedirection()
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseGiraffe(webApp txtfile)

let configureServices (services : IServiceCollection) =
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    builder.AddFilter(fun l -> l.Equals LogLevel.Error)
           .AddConsole()
           .AddDebug() |> ignore

[<EntryPoint>]
let main args =
    let contentRoot = 
        match Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") with
        | "Development" -> Directory.GetCurrentDirectory()
        | _ -> Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    let txtFileName = 
        if isNull args || args.Length = 0 || args.[0] |> String.IsNullOrEmpty then
            None
        else 
            Some args.[0]
        |> Option.bind (fun s ->
            let fpath = s |> Path.GetFullPath
            File.Exists fpath |> function
            | true  -> Some fpath
            | false -> None)
    
    if txtFileName |> Option.isNone then
        printfn "no text file specified.."
    else
        let txtfile = Option.get txtFileName
        let fnameShort = Path.GetFileNameWithoutExtension(txtfile)
        let txtfile' =
            if Regex("""_\d{12}$""").IsMatch(fnameShort) then
                txtfile
            else
                let txtfileCopy = 
                    sprintf "%s/%s_%s%s" 
                        (Path.GetDirectoryName txtfile) 
                        fnameShort 
                        (DateTime.Now.ToString("yyyyMMddHHmm"))
                        (Path.GetExtension txtfile)
                    |> Path.GetFullPath
                File.Copy(txtfile, txtfileCopy, overwrite=true)
                txtfileCopy
        Process.Start("powershell.exe", "sleep 5; start https://localhost:5001/editor") |> ignore
        WebHostBuilder()
            .UseKestrel()
            .UseContentRoot(contentRoot)
            .UseIISIntegration()
            .UseWebRoot(webRoot)
            .Configure(Action<IApplicationBuilder> (configureApp txtfile'))
            .ConfigureServices(configureServices)
            .ConfigureLogging(configureLogging)
            .Build()
            .Run()

    0