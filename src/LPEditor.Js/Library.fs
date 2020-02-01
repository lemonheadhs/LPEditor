namespace LPEditor.Js
open Monaco
open Fable.Core
open Fable.Core.JsInterop
open Browser.Dom
open Monaco.Monaco
open Monaco.Monaco.Languages
open Monaco.Monaco.Editor
open Commands

module Say =
    let hello name =
        printfn "Hello %s" name
    
    [<Emit("self")>]
    let self: Browser.Types.Window = jsNative
    (!!self)?MonacoEnvironment <- 
        createObj [
            "getWorkerUrl" ==> 
                fun moduleId label ->
                    match label with
                    | "json" -> "./json.worker.bundle.js"
                    | "css" -> "./css.worker.bundle.js"
                    | "html" -> "./html.worker.bundle.js"
                    | "typescript" | "javascript" -> "./ts.worker.bundle.js"
                    | _ -> "./editor.worker.bundle.js"

        ]
    Languages.typescript.typescriptDefaults |> ignore
    importSideEffects "monaco-editor/esm/vs/basic-languages/javascript/javascript.contribution.js"
    importSideEffects "monaco-editor/esm/vs/editor/browser/controller/coreCommands.js"
    importSideEffects "monaco-editor/esm/vs/editor/contrib/codeAction/codeActionContributions.js"
    importSideEffects "monaco-editor/esm/vs/editor/contrib/find/findController.js"
    importSideEffects "monaco-editor/esm/vs/editor/contrib/multicursor/multicursor.js"
    importSideEffects "monaco-editor/esm/vs/editor/standalone/browser/quickOpen/gotoLine.js"
    importSideEffects "monaco-editor/esm/vs/editor/standalone/browser/quickOpen/quickCommand.js"
    importSideEffects "monaco-editor/esm/vs/editor/standalone/browser/quickOpen/quickOutline.js"
    importSideEffects "monaco-editor/esm/vs/base/browser/ui/codiconLabel/codicon/codicon.css"
    
    Monaco.languages.register(!!{| id = "lpcontent" |})
    let tkzr : IMonarchLanguageTokenizer = 
        !!{|
            root = [
                ("\\s{3,}", {| token = "error-token" |})
                ("\\s{2}", {| token = "warn-token" |})
                ("-+ Page \\d+-+", {| token = "keyword" |})
            ] |> List.toArray
        |}
    
    Monaco.languages.setMonarchTokensProvider("lpcontent", jsOptions<IMonarchLanguage>(fun x ->
        x.tokenizer <- tkzr
        x.ignoreCase <- Some true
    ))
    |> ignore
    Monaco.editor.defineTheme("myCustomTheme", jsOptions<IStandaloneThemeData>(fun x ->
        x.``base`` <- BuiltinTheme.Vs
        x.``inherit`` <- true
        x.rules <- [
            !!{| token="error-token";foreground="ff2222";fontStyle=" wavy underline" |}
        ] |> ResizeArray
    ))
    
    let editor =
        Monaco.editor.create(
            document.getElementById("container"),
            !!{|
                value = ""
                language = "lpcontent"
                theme= "myCustomTheme"
            |})

    open Thoth.Fetch
    open Fable.Core
    open Fable.Import
    type Msg = { text: string }

    window.addEventListener("load", fun _ -> 
        registerActions editor
        registerCommands editor
        promise {
            let! msg = Fetch.get("editor/content")
            let line = editor.getPosition()
            let range = monaco.Range.Create(line.lineNumber, 1, line.lineNumber, 1)
            let id: Editor.ISingleEditOperationIdentifier = !!{| major=1.; minor=1. |}
            editor.executeEdits("my-source", [
                !!{|
                    identifier= id
                    range= range
                    text= msg.text
                    forceMoveMarkers= true
                |}
            ] |> ResizeArray) |> ignore
        } |> ignore)
