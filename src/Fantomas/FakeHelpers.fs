module Fantomas.FakeHelpers

open System
open System.IO
open FSharp.Compiler.SourceCodeServices
open Fantomas
open Fantomas.FormatConfig

// Share an F# checker instance across formatting calls
let sharedChecker = lazy (FSharpChecker.Create())

exception CodeFormatException of (string * Option<Exception>) array with
    override x.ToString() =
        let errors =
            x.Data0
            |> Array.choose (fun z ->
                match z with
                | file, Some ex -> Some(file, ex)
                | _ -> None)
            |> Array.map (fun z ->
                let file, ex = z
                file + ":\r\n" + ex.Message + "\r\n\r\n")

        let files =
            x.Data0
            |> Array.map (fun z ->
                match z with
                | file, Some _ -> file + " !"
                | file, None -> file)

        String.Join(String.Empty, errors)
        + "The following files aren't formatted properly:"
        + "\r\n- "
        + String.Join("\r\n- ", files)

type FormatResult =
    | Formatted of filename: string * formattedContent: string
    | Unchanged of filename: string
    | Error of filename: string * formattingError: Exception

let createParsingOptionsFromFile fileName =
    { FSharpParsingOptions.Default with
          SourceFiles = [| fileName |] }

let formatContentAsync config (file: string) (originalContent: string) =
    async {
        try
            let! formattedContent =
                CodeFormatter.FormatDocumentAsync
                    (file,
                     SourceOrigin.SourceString originalContent,
                     config,
                     createParsingOptionsFromFile file,
                     sharedChecker.Value)
            if originalContent <> formattedContent then
                let! isValid =
                    CodeFormatter.IsValidFSharpCodeAsync
                        (file,
                         (SourceOrigin.SourceString(formattedContent)),
                         createParsingOptionsFromFile file,
                         sharedChecker.Value)
                if not isValid then
                    raise
                    <| FormatException "Formatted content is not valid F# code"

                return Formatted(filename = file, formattedContent = formattedContent)
            else
                return Unchanged(filename = file)
        with ex -> return Error(file, ex)
    }

let formatFileAsync config (file: string) =
    let originalContent = File.ReadAllText file
    async {
        let! formated = originalContent |> formatContentAsync config file
        return formated
    }

let formatFilesAsync config files =
    files
    |> Seq.map (formatFileAsync config)
    |> Async.Parallel

let formatCode config files =
    async {
        let! results = files |> formatFilesAsync config

        // Check for formatting errors:
        let errors =
            results
            |> Array.choose (fun x ->
                match x with
                | Error (file, ex) -> Some(file, Some(ex))
                | _ -> None)

        if not <| Array.isEmpty errors then raise <| CodeFormatException errors

        // Overwritte source files with formatted content
        let result =
            results
            |> Array.choose (fun x ->
                match x with
                | Formatted (source, formatted) ->
                    File.WriteAllText(source, formatted)
                    Some source
                | _ -> None)

        return result
    }

type CheckResult =
    { Errors: (string * exn) list
      Formatted: string list }
    member this.HasErrors = List.isNotEmpty this.Errors
    member this.NeedsFormatting = List.isNotEmpty this.Formatted

    member this.IsValid =
        List.isEmpty this.Errors
        && List.isEmpty this.Formatted

/// Runs a check on the given files and reports the result to the given output:
///
/// * It shows the paths of the files that need formatting
/// * It shows the path and the error message of files that failed the format check
///
/// Returns:
///
/// A record with the file names that were formatted and the files that encounter problems while formatting.
let checkCode (config: FormatConfig) (filenames: seq<string>) =
    async {
        let! formatted =
            filenames
            |> Seq.map (formatFileAsync config)
            |> Async.Parallel

        let getChangedFile =
            function
            | FormatResult.Unchanged (_) -> None
            | FormatResult.Formatted (f, _)
            | FormatResult.Error (f, _) -> Some f

        let changes =
            formatted
            |> Seq.choose getChangedFile
            |> Seq.toList

        let getErrors =
            function
            | FormatResult.Error (f, e) -> Some(f, e)
            | _ -> None

        let errors =
            formatted |> Seq.choose getErrors |> Seq.toList

        return { Errors = errors; Formatted = changes }
    }
