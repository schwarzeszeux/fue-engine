module Helper

open System.Net
open Highlight
open Highlight.Engines
open Fue.Compiler
open System.IO
open System
open System.Security.Cryptography

let highlighter = new Highlighter(new HtmlEngine());

let code language code =
    let highlightedCode = highlighter.Highlight(language, WebUtility.HtmlDecode(code));

    let encodedText = $"""
<pre>{highlightedCode}</pre>
    """

    encodedText

let addFootnotes (data: Map<string, obj>) =
    let (footnote, renderFootnotes) =
        (fun () ->
            let footnotes: System.Collections.Generic.List<string> = new System.Collections.Generic.List<string>()

            let setFootnote text =
                let nextFootnotenumber = footnotes.Count + 1
                footnotes.Add(text)

                sprintf """<a href="#footnote-%i" id="footnote-%i-source"><sup>[%i]</sup></a>""" nextFootnotenumber nextFootnotenumber nextFootnotenumber

            let renderFootnotes () =
                let renderFootnote i text =
                    let number = i + 1
                    sprintf """<p id="footnote-%i">[%i] %s <a href="#footnote-%i-source">^</i></p>""" number number text number

                footnotes
                |> Seq.mapi renderFootnote
                |> String.concat ""
                |> sprintf "<hr />%s"

            (setFootnote, renderFootnotes)
        )()

    data
    |> Map.add "footnote" footnote
    |> Map.add "renderFootnotes" renderFootnotes

let mergeMap init map =
    Map.fold (fun acc key value -> Map.add key value acc) init map

let private colors = [|
    "#FFB300"    // Vivid Yellow
    "#803E75"    // Strong Purple
    "#FF6800"    // Vivid Orange
    "#A6BDD7"    // Very Light Blue
    "#C10020"    // Vivid Red
    "#CEA262"    // Grayish Yellow
    "#817066"    // Medium Gray
            
    "#007D34"    // Vivid Green
    "#F6768E"    // Strong Purplish Pink
    "#00538A"    // Strong Blue
    "#FF7A5C"    // Strong Yellowish Pink
    "#53377A"    // Strong Violet
    "#FF8E00"    // Vivid Orange Yellow
    "#B32851"    // Strong Purplish Red
    "#F4C800"    // Vivid Greenish Yellow
    "#7F180D"    // Strong Reddish Brown
    "#93AA00"    // Vivid Yellowish Green
    "#593315"    // Deep Yellowish Brown
    "#F13A13"    // Vivid Reddish Orange
    "#232C16"    // Dark Olive Green
|]

let colorHash (value: string) = 
    //let i = (value.GetHashCode()) % (colors.Length)
    let md5Hasher = MD5.Create();
    let hashed = md5Hasher.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
    let i = BitConverter.ToUInt32(hashed, 0);

    let x = uint32 colors.Length;
    colors.[int(i % x)]

let render currentPath init =

    fun template vars ->
        let mergedMap =
            vars
            |> mergeMap init

        mergedMap
        |> fromFile (Path.Combine(currentPath, sprintf "%s.fue" template))