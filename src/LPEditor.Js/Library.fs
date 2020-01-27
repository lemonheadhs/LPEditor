namespace LPEditor.Js
open Monaco
open Fable.Core
open Fable.Core.JsInterop
open Browser.Dom
open Monaco.Monaco
open Monaco.Monaco.Languages
open Monaco.Monaco.Editor
open System.Text.RegularExpressions

module Say =
    let hello name =
        printfn "Hello %s" name

    let tab fn x = fn x; x

    
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
            ] |> List.toArray
        |}
    console.log(tkzr)
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
        ] |> ResizeArray |> tab (fun o -> console.log(o))
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
        promise {
            let! msg = Fetch.get("editor/content")
            let line = editor.getPosition()
            let range = monaco.Range.Create(line.lineNumber, 1., line.lineNumber, 1.)
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

    let sanitizeSelected (ed: ICommonCodeEditor) =
        let m: IModel = ed.getModel()
        let selection = ed.getSelection()
        let text = m.getValueInRange(!!selection)
        let rgx = Regex("""[\s\r\n\t]+""")
        let newText = rgx.Replace(text, " ")
        ed.executeEdits("my-sanitize-method", [
            !!{|
                identifier= {| major=1.; minor=1. |}
                range= selection
                text= newText
            |}
        ] |> ResizeArray) |> ignore
        
    editor.addAction(
        !!{|
            id= "lpe-sanitize"
            label= "LPE - Sanitize selected"
            keybindings= [
                monaco.KeyMod.chord(monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_K, monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_L)
            ] |> ResizeArray |> Some
            contextMenuGroupId= Some "lpe"
            contextMenuOrder= Some 1.5
            run= (sanitizeSelected >> U2.Case1)
        |}) |> ignore

    let lpecmdctx = editor.createContextKey("lpecmdctx", true)
    editor.addCommand(monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_S, !!(fun () ->
        let text = editor.getValue()
        console.log(text)
        Fetch.post("editor/content", text)
    ), "lpecmdctx") |> ignore

