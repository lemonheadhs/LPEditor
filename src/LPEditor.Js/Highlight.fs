module Highlight

open System
open System.Text
open System.Text.RegularExpressions

[<RequireQualifiedAccess>]
module MK =
    let seqWhtspc = """[\s\r\n\t]+"""
    let _3plusWhtspc = """[\s\r\n\t]{3,}"""
    let pageMark = """-+ Page \d+-+"""
    let wordbreak = """-\s*\n+"""

[<RequireQualifiedAccess>]
module MKReg =
    let seqWhtspc = Regex(MK.seqWhtspc)
    let _3plusWhtspc = Regex(MK._3plusWhtspc)
    let pageMark = Regex(MK.pageMark)
    let wordbreak = Regex(MK.wordbreak)

