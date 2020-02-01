module rec MonacoQuickOpen
open System
open Fable.Core
open Fable.Core.JS
open Monaco.Monaco.Editor

type QuickOpenController =
    abstract dispose: unit -> unit
    abstract ID: string

type BaseEditorQuickOpenAction =
    abstract getController: ICommonCodeEditor -> QuickOpenController
    abstract _show: controller: QuickOpenController -> opts: QuickOpenActionOptions -> unit

type BaseEditorQuickOpenActionStatic =
    [<Emit "new $0($1...)">] abstract Create: inputArialLabel: string -> opts: obj -> BaseEditorQuickOpenAction

let [<Import("BaseEditorQuickOpenAction","monaco-editor/esm/vs/editor/standalone/browser/quickOpen/editorQuickOpen.js")>] 
    baseEditorQuickOpenCtor: BaseEditorQuickOpenActionStatic = jsNative


type QuickOpenActionOptions = {
    getModel: string -> QuickOpenModel
    getAutoFocus: string -> obj
}

type QuickOpenModel = 
    abstract entries: QuickOpenEntry[]
type QuickOpenModelStatic =
    [<Emit "new $0($1...)">] abstract Create: entries: QuickOpenEntry[] -> QuickOpenModel

let [<Import("QuickOpenModel","monaco-editor/esm/vs/base/parts/quickopen/browser/quickOpenModel.js")>] 
    QuickOpenModelCtor: QuickOpenModelStatic = jsNative

type QuickOpenEntry =
    abstract getId: unit -> string
    abstract getLabel: unit -> string
    abstract getAriaLabel: unit -> string
    abstract isHidden: unit -> bool
    abstract run: mode: obj -> context: obj -> bool
let [<Import("QuickOpenEntry", "monaco-editor/esm/vs/base/parts/quickopen/browser/quickOpenModel.js")>] 
    quickOpenEntry: QuickOpenEntry = jsNative

let [<Emit "$0.call(this) || this">] jsInherit<'a> (superClass: obj) : 'a = jsNative
let [<Emit "$2.prototype[$0] = $1">] jsPrototypeDefine (propName:string) (propVal:obj) (target:obj) = jsNative
let [<Emit "new $0($1, $2)">] jsConstructorCreate (fn:'a -> 'b) (a:'a) : 'b = jsNative
let [<ImportDefault "./extend.js">] __extends target superClass : unit = jsNative

type ActionProvider = 
    abstract hasActions: tree: obj -> element: obj -> bool
    abstract getActions: tree: obj -> element: obj -> Action[]

type Action = {
    enabled: bool
    run: QuickOpenEntry -> unit
}















