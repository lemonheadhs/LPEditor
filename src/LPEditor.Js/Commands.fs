module Commands

open System
open FSharp.Core
open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open Monaco
open Monaco.Monaco.Editor
open Monaco.Monaco
open System.Text.RegularExpressions
open Thoth.Fetch
open Browser.Dom
open Highlight

let tab fn x = fn x; x
let always x _ = x
let merge (s:obj) t = Constructors.Object.assign(t, s)
type Promise<'a> = Fable.Core.JS.Promise<'a>
let Promise = Fable.Core.JS.Constructors.Promise

let edit transformAsync (ed:ICommonCodeEditor) (r: IRange, text: string) =
    promise {
        let! newText = transformAsync text
        ed.executeEdits("my-lpe-method", [
            !!{|
                identifier= {| major=1.; minor=1. |}
                range= r
                text= newText
            |}
        ] |> ResizeArray) |> ignore
    }
let (^*) (regx: Regex) (rep:string) = fun inputStr ->
    regx.Replace(inputStr, rep)

[<AutoOpen>]
module CustomQuickOpen = begin
    open MonacoQuickOpen
    let chapterNameQuickOpen = baseEditorQuickOpenCtor.Create "Input Chapter Name" {|
        id= "lpeditor.action.chapterName"
        label= "Input chapter name..."
        alias= "Input chapter name..."
        precondition= null
        kbOpts= obj()
    |}
    let simpleEntry =
        let simpleEntry (label:string, runFn:unit -> unit) : QuickOpenEntry = 
            let _this = jsInherit quickOpenEntry
            _this?_label <- label
            _this?_runFn <- runFn
            _this
        __extends simpleEntry quickOpenEntry
        simpleEntry |> jsPrototypeDefine "getLabel" (fun () -> jsThis?_label)
        simpleEntry |> jsPrototypeDefine "getAriaLabel" (fun () -> jsThis?_label)
        simpleEntry |> jsPrototypeDefine "run" (fun () -> jsThis?_runFn())
        simpleEntry
    let splitSaveRun ed (r, s) =
        let runInner (chapterFilename:string) = fun () ->
            Fetch.post("editor/content/chapter", {|
                filename= chapterFilename
                content= s
            |})
                .``then``(fun _ ->
                    edit (always (Promise.resolve "")) ed (r, s) |> ignore) |> ignore
        chapterNameQuickOpen._show (chapterNameQuickOpen.getController(ed)) {
            getModel= fun value -> 
                QuickOpenModelCtor.Create 
                    [| 
                        jsConstructorCreate simpleEntry ((sprintf "Save as \"%s.txt\".." value), (runInner value))
                    |]
            getAutoFocus= always (obj())
        }
    let insertMetaRun (ed:ICommonCodeEditor) genMeta =
        let runInner meta () =
            let pos = ed.getPosition()
            let r = monaco.Range.Create (pos.lineNumber, pos.column, pos.lineNumber, pos.column)
            (edit (always (Promise.resolve meta)) ed (r, ""))
                .``then``(fun _ -> 
                            let form = document.forms.namedItem "doc-info"
                            form?order?value <- 1 + (form?order?value |> string |> Convert.ToInt32))
            |> ignore
        chapterNameQuickOpen._show (chapterNameQuickOpen.getController(ed)) {
            getModel= fun value ->
                QuickOpenModelCtor.Create [| jsConstructorCreate simpleEntry ((sprintf "Section: \"%s\".." value), (runInner (genMeta value))) |]
            getAutoFocus= always (obj())
        }
end

let selectedText (ed:ICommonCodeEditor) =
    let m: IModel = ed.getModel()
    let selection = ed.getSelection()
    selection :> IRange, m.getValueInRange(selection)
let upfrontText (ed:ICommonCodeEditor) =
    let m: IModel = ed.getModel()
    let pos = ed.getPosition()
    let upfront = monaco.Range.Create(1, 1, pos.lineNumber, pos.column)
    upfront :> IRange, m.getValueInRange(upfront)

let editSelected transformAsync (ed: ICommonCodeEditor) =
    edit transformAsync ed (selectedText ed)

let sanitizeSelected :(ICommonCodeEditor -> Promise<unit>) =
    editSelected (
        (MKReg.pageMark ^* "") >> (MKReg.wordbreak ^* "") >> (MKReg.seqWhtspc ^* " ")
        >> Promise.resolve)
let removeWSSelected :(ICommonCodeEditor -> Promise<unit>) =
    editSelected (
        (MKReg.pageMark ^* "") >> (MKReg.wordbreak ^* "") >> (MKReg.seqWhtspc ^* "") 
        >> Promise.resolve)
let gotoNextSuspect (ed:ICommonCodeEditor) =
    let m = ed.getModel()
    let pos = ed.getPosition()
    let matchSuspect =
        m.findNextMatch(MK._3plusWhtspc + "|" + MK.pageMark, pos, 
            isRegex=true, matchCase=false, 
            wordSeparators=null, captureMatches=false)
    ed.setSelection(matchSuspect.range)
    ed.revealRangeInCenterIfOutsideViewport(matchSuspect.range) |> Promise.resolve
let splitSave (ed:ICommonCodeEditor) =
    let filterRange (r: IRange, s: string) =
        match s.Length with
        | l when l < 10 -> None
        | _             -> Some (r, s)
    [selectedText; upfrontText]
    |> Seq.fold (fun s fn -> 
                    match s with
                    | Some p -> Some p
                    | None   -> fn ed |> filterRange
                    ) None
    |> Option.map (splitSaveRun ed) |> Option.defaultValue ()
    |> Promise.resolve
            
let metaTemplate t o l s = (sprintf """
<lpmentor>
Topic: %s
Section: %s
Order: %i
Lang: %s
</lpmentor>
""" t s o l)
let insertMeta (ed:ICommonCodeEditor) =
    let genMeta = 
        let form = document.forms.namedItem "doc-info"
        let topic:string = form?topic?value
        let order:int = form?order?value |> string |> Convert.ToInt32
        let language:string = form?lang?value
        metaTemplate topic order language
    insertMetaRun ed genMeta
    Promise.resolve()


type EditorActionDef = {
    id: string
    label: string
    keybindings: ResizeArray<int>
    run: ICommonCodeEditor -> Promise<unit>
}

let actions = [|
    {
        id= "lpe-sanitize"
        label= "LPE - Sanitize selected"
        keybindings= [|
            (monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_L
            ,monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_L) |> monaco.KeyMod.chord
        |] |> ResizeArray
        run= sanitizeSelected 
    }
    {
        id= "lpe-remove-whitespace"
        label= "LPE - Remove whitespace"
        keybindings= [|
            (monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_L
            ,monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_O) |> monaco.KeyMod.chord
        |] |> ResizeArray
        run= removeWSSelected
    }
    {
        id= "lpe-next-suspect"
        label= "LPE - Next suspect"
        keybindings= [|
            (monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_L
            ,monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_M) |> monaco.KeyMod.chord
        |] |> ResizeArray
        run= gotoNextSuspect
    }
    {
        id= "lpe-split-save-portion"
        label= "LPE - Split & Save portion"
        keybindings= [|
            (monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_L
            ,monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_P) |> monaco.KeyMod.chord
        |] |> ResizeArray
        run= splitSave
    }
    {
        id= "lpe-insert-meta"
        label= "LPE - Insert meta"
        keybindings= [|
            (monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_L
            ,monaco.KeyMod.CtrlCmd ||| int KeyCode.KEY_E) |> monaco.KeyMod.chord
        |] |> ResizeArray
        run= insertMeta
    }
|] 

let registerActions (editor: IStandaloneCodeEditor) =
    actions
    |> Collections.Array.map (merge {|
                        contextMenuGroupId= "lpe"
                        contextMenuOrder= 1500 
                    |} >> (fun o -> o :?> IActionDescriptor))
    |> Seq.iter (editor.addAction >> ignore)

let registerCommands (editor: IStandaloneCodeEditor) =
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
            let form = document.forms.namedItem "doc-info"
            let topic:string = form?topic?value
            let order:int = form?order?value |> string |> Convert.ToInt32
            let language:string = form?lang?value
            window.localStorage.setItem 
                ("lpe-docinfo", JSON.stringify({| order=order; topic=topic; language=language |}))
            
            Fetch.put("editor/shutdown", null)
                .``then``(fun _ ->
                    window.alert("server shut down, you can close the window now")) |> ignore
        )
    let mutable currWordWrap = "off"
    addLPECommand (monaco.KeyMod.Alt ||| int KeyCode.KEY_Z)
        (fun () ->
            currWordWrap <- if currWordWrap = "off" then "on" else "off" 
            editor.updateOptions(!!{| wordWrap = currWordWrap |}))


