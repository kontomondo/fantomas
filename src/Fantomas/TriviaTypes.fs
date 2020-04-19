module Fantomas.TriviaTypes

open FSharp.Compiler.SourceCodeServices
open FSharp.Compiler.Range

type Token =
    { TokenInfo: FSharpTokenInfo
      LineNumber: int
      Content: string }


type Comment =
    | LineCommentAfterSourceCode of comment: string
    | LineCommentOnSingleLine of comment: string
    | BlockComment of string * newlineBefore: bool * newlineAfter: bool

(* LineComment Examples

let a = 7 // b

=> LineCommentAfterSourceCode("// b", true)

// meh
let a = 7

=> LineCommentOnSingleLine("// meh", false)
*)

type TriviaContent =
    | Keyword of Token
    | Number of string
    | StringContent of string
    | IdentOperatorAsWord of string
    | IdentBetweenTicks of string
    | Comment of Comment
    | Newline
    | Directive of directive: string
    | CharContent of string

type Trivia =
    { Item: TriviaContent
      Range: range }
    static member Create item range: Trivia = { Item = item; Range = range }

type TriviaIndex = TriviaIndex of int * int

type TriviaNodeType =
    | MainNode of ``type``: string
    | Token of Token

type TriviaNode =
    { Type: TriviaNodeType
      ContentBefore: TriviaContent list
      ContentItself: TriviaContent option
      ContentAfter: TriviaContent list
      Range: range }
