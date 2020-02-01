namespace LPEditor.Js
open Monaco
open Fable.Core
open Fable.Core.JsInterop
open Browser.Dom
open Monaco.Monaco
open Monaco.Monaco.Languages
open Monaco.Monaco.Editor
open System.Text.RegularExpressions
open Fable.Core.JS

module Say =
    let hello name =
        printfn "Hello %s" name

    let tab fn x = fn x; x
    let always x _ = x

    
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

    let selectedText (ed:ICommonCodeEditor) =
        let m: IModel = ed.getModel()
        let selection = ed.getSelection()
        selection :> IRange, m.getValueInRange(selection)
    let upfrontText (ed:ICommonCodeEditor) =
        let m: IModel = ed.getModel()
        let pos = ed.getPosition()
        let upfront = monaco.Range.Create(1, 1, pos.lineNumber, pos.column)
        upfront :> IRange, m.getValueInRange(upfront)

    let edit transformAsync (ed:ICommonCodeEditor) (r: IRange, text: string) =
        promise {
            let! newText = transformAsync text
            ed.executeEdits("my-sanitize-method", [
                !!{|
                    identifier= {| major=1.; minor=1. |}
                    range= r
                    text= newText
                |}
            ] |> ResizeArray) |> ignore
        }
    let editSelected transformAsync (ed: ICommonCodeEditor) =
        edit transformAsync ed (selectedText ed)

    let sequentialWhitespace = Regex("""[\s\r\n\t]+""")
    let sanitizeSelected :(ICommonCodeEditor -> Promise<unit>) =
        editSelected (fun text ->
            sequentialWhitespace.Replace(text, " ") |> Promise.resolve)
    let removeWSSelected :(ICommonCodeEditor -> Promise<unit>) =
        editSelected (fun text ->
            sequentialWhitespace.Replace(text, "") |> Promise.resolve)
    let gotoNextSuspect (ed:ICommonCodeEditor) =
        let m = ed.getModel()
        let pos = ed.getPosition()
        let matchSuspect =
            m.findNextMatch("""[\s\r\n\t]{3,}""", pos, 
                isRegex=true, matchCase=false, 
                wordSeparators=null, captureMatches=false)
        ed.setSelection(matchSuspect.range)
        ed.revealRangeInCenterIfOutsideViewport(matchSuspect.range) |> Promise.resolve
    open MonacoQuickOpen
    let splitSaveQuickOpen = baseEditorQuickOpenCtor.Create "" {|
        id= "lpeditor.action.saveChapter"
        label= "Input chapter name..."
        alias= "Input chapter name..."
        precondition= null
        kbOpts= obj()
    |}
    let splitSaveEntry =
        let splitSaveEntry (value:string, runFn:unit -> unit) : QuickOpenEntry = 
            let _this = jsInherit quickOpenEntry
            _this?_val <- value
            _this?_runFn <- runFn
            _this
        __extends splitSaveEntry quickOpenEntry
        !!splitSaveEntry |> jsPrototypeDefine "getLabel" (fun () -> sprintf "Save as \"%A.txt\".." jsThis?_val)
        !!splitSaveEntry |> jsPrototypeDefine "getAriaLabel" (fun () -> sprintf "Save as \"%A.txt\".." jsThis?_val)
        !!splitSaveEntry |> jsPrototypeDefine "run" (fun () -> jsThis?_runFn())
        splitSaveEntry
    let run ed (r, s) =
        let runInner (chapterFilename:string) = fun () ->
            Fetch.post("editor/content/chapter", {|
                filename= chapterFilename
                content= s
            |})
                .``then``(fun _ ->
                    edit (always (Promise.resolve "")) ed (r, s) |> ignore) |> ignore
        splitSaveQuickOpen._show (splitSaveQuickOpen.getController(ed)) {
            getModel= fun value -> 
                QuickOpenModelCtor.Create 
                    [| 
                        jsConstructorCreate splitSaveEntry (value, (runInner value)) |> tab (fun o -> console.log(o))
                    |]
            getAutoFocus= fun searchValue -> obj()
        }
    let splitSave (ed:ICommonCodeEditor) =
        let filterRange (r: IRange, s: string) =
            match s.Length with
            | l when l < 10 -> None
            | _ -> Some (r, s)
        [selectedText; upfrontText]
        |> Seq.fold (fun s fn -> 
                        match s with
                        | Some p -> Some p
                        | None ->
                            fn ed |> filterRange
                        ) None
        |> function
        | None -> ()
        | Some (r, s) ->            
            run ed (r, s)
        |> Promise.resolve
                
    let insertMeta (ed:ICommonCodeEditor) =
        
        Promise.resolve()

    let merge (s:obj) t = Constructors.Object.assign(t, s)
    [|
        {|
            id= "lpe-sanitize"
            label= "LPE - Sanitize selected"
            keybindings= [
                (monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_L
                ,monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_L) |> monaco.KeyMod.chord
            ] |> ResizeArray |> Some
            run= (sanitizeSelected >> U2.Case2) 
        |}
        {|
            id= "lpe-remove-whitespace"
            label= "LPE - Remove whitespace"
            keybindings= [
                (monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_L
                ,monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_O) |> monaco.KeyMod.chord
            ] |> ResizeArray |> Some
            run= (removeWSSelected >> U2.Case2) 
        |}
        {|
            id= "lpe-next-suspect"
            label= "LPE - Next suspect"
            keybindings= [
                (monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_L
                ,monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_M) |> monaco.KeyMod.chord
            ] |> ResizeArray |> Some
            run= (gotoNextSuspect >> U2.Case2) 
        |}
        {|
            id= "lpe-split-save-portion"
            label= "LPE - Split & Save portion"
            keybindings= [
                (monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_L
                ,monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_P) |> monaco.KeyMod.chord
            ] |> ResizeArray |> Some
            run= (splitSave >> U2.Case2) 
        |}
        {|
            id= "lpe-insert-meta"
            label= "LPE - Insert meta"
            keybindings= [
                (monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_L
                ,monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_E) |> monaco.KeyMod.chord
            ] |> ResizeArray |> Some
            run= (insertMeta >> U2.Case2) 
        |}
    |] 
    |> Seq.map (merge {|
                        contextMenuGroupId= Some "lpe"
                        contextMenuOrder= Some 1500 
                    |} >> (fun o -> o :?> IActionDescriptor))
    |> Seq.iter (editor.addAction >> ignore)

    let lpecmdctx = editor.createContextKey("lpecmdctx", true)
    let addLPECommand keybindings (fn: unit -> unit) =
        editor.addCommand(keybindings, !!(fn), "lpecmdctx") |> ignore
    addLPECommand (monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_S)
        (fun () ->
            let text = editor.getValue()
            Fetch.post("editor/content", text) |> ignore
        )
    addLPECommand (monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_Q)
        (fun () ->
            Fetch.put("editor/shutdown", null)
                .``then``(fun _ ->
                    window.alert("server shut down, you can close the window now")) |> ignore
        )
