module Engine

open System
open Fue.Data
open Fue.Compiler
open System.IO

type Metadata = {
    tags: string
    title: string
    filetype: string
    description: string
    fileName: string
    date: DateTime option
    category: string
}

type PostMetadata = {
    tags: string
    title: string
    description: string
    fileName: string
    date: DateTime
    category: string
}

type OutputPath = | OutputPath of string
type InputPath = | InputPath of string

let defaultModel =
    init
    |> add "generationTime" (DateTime.Now)
    |> add "formatDate" (fun (date: DateTime) -> date.ToString("yyyy-MM-dd"))

let getTemplate path =
    let templatePath = Path.Combine(path, "template.fue")
    if (File.Exists(templatePath)) then
        File.ReadAllText(templatePath)
    else
        "{{{content}}}"

let rec write currentDirectory (template, templateData) content =
    template
    |> function
    | None -> content
    | Some layoutRelativePath ->
        let layoutPath = Path.Combine(currentDirectory, sprintf "%s.fue" layoutRelativePath)

        File.Exists layoutPath
        |> function
        | false -> new Exception (sprintf "%s does not exist" layoutPath) |> raise
        | true ->
            let template = File.ReadAllText layoutPath

            let mutable masterLayout = None
            let mutable masterLayoutData: Map<string, obj> = templateData

            let pageModel =
                templateData
                |> add "content" content
                |> add "layout" (fun path -> masterLayout <- Some path)
                |> add "layoutWithData" (fun path paras ->
                    masterLayout <- Some path
                    masterLayoutData <- Helper.mergeMap templateData paras
                )
                |> fromText template

            let layoutFolder = Path.GetDirectoryName(layoutPath)

            write layoutFolder (masterLayout, masterLayoutData)  pageModel

let writeFile path content =
    File.WriteAllText(path, content)


let runFolder (InputPath path) children category =
    Directory.GetFiles(path, "*.fue", SearchOption.TopDirectoryOnly)
    |> Seq.choose (fun postPath ->
        let currentDirectory = Path.GetDirectoryName(postPath)

        if (Path.GetFileNameWithoutExtension(postPath) = "template") then
            None
        else
            printfn "Processing %s" postPath

            let resultName = Path.GetFileNameWithoutExtension(postPath)

            let defaultMetadata = {
                tags = ""
                title = ""
                description = ""
                fileName = resultName
                date = None
                category = category
                filetype = "html"
            }

            let mutable metadata: Metadata option = None

            let publish (date: string) =
                let parsedDate = DateTime.Parse date
                let currentDate = DateTime.Now.Date

                if parsedDate <= currentDate then
                    metadata <- 
                            metadata
                            |> Option.map (fun metadata ->
                                    {
                                        metadata with date = Some parsedDate
                                    })
                            |> Option.defaultValue
                                    {
                                        defaultMetadata with date = Some parsedDate
                                    }
                            |> Some

            let mutable pageModel = init
            let mutable masterLayout: string option = None
            let mutable masterLayoutData: Map<string, obj> = defaultModel

            pageModel <-
                defaultModel
                |> add "layout" (fun path -> masterLayout <- Some path)
                |> add "layoutWithData" (fun path paras ->
                    masterLayout <- Some path
                    masterLayoutData <- paras
                )
                |> add "children" children
                |> add "publish" publish
                |> add "groupByTags" (fun (children: (string*PostMetadata) array) ->
                    children
                    |> List.ofArray
                    |> List.indexed
                    |> List.fold (fun state (i,( _, item)) ->
                        if not <| Map.containsKey item.tags state then
                            state
                            |> Map.add item.tags []
                        else
                            state
                        |> fun ensuredState -> 
                            ensuredState
                            |> Map.add item.tags ((i, item) :: ensuredState.[item.tags])
                    ) Map.empty<string, (int*PostMetadata) list>
                )
                |> add "code" Helper.code
                |> add "toRfc822" (fun (date: DateTime) -> date.ToString("r"))
                |> add "notEmpty" (fun (str: string) -> String.IsNullOrWhiteSpace(str) |> not)
                |> add "clean" (fun (str: string) -> str.ToLower().Replace(" ", "-"))
                |> add "filetype" (fun filetype ->
                    metadata <- 
                        metadata
                        |> Option.map (fun metadata ->
                                {
                                    metadata with
                                        filetype = filetype
                                })
                        |> Option.defaultValue
                                {
                                    defaultMetadata with
                                        filetype = filetype
                                }
                        |> Some
                )
                |> Helper.addFootnotes
                |> add "render" (fun template vars -> Helper.render path pageModel template vars)
                |> add "isEqual" (fun left right -> left = right)
                |> add "colorHash" Helper.colorHash
                |> add "setMetadata" (fun (data: Map<string, obj>) ->
                    metadata <- 
                        data
                        |> Seq.fold (fun (state: Metadata) current ->
                            match current.Key with
                            | "tags" -> { state with tags = current.Value :?> string }
                            | "title" -> { state with title = current.Value :?> string }
                            | "description" -> { state with description = current.Value :?> string }
                            | "category" -> { state with category = current.Value :?> string }
                            | _ -> state
                        ) (Option.defaultValue defaultMetadata metadata)
                        |> Some
                )

            let renderedPage =
                pageModel
                |> fromFile postPath

            metadata
            |> Option.bind (fun info ->
                info.date
                |> Option.map (fun date ->
                    let mergedLayout = Helper.mergeMap pageModel masterLayoutData
                    write currentDirectory (masterLayout, mergedLayout) renderedPage, {
                        title = info.title
                        description = info.description
                        tags = info.tags
                        category = info.category
                        fileName = sprintf "%s.%s" info.fileName info.filetype
                        date = date
                    }
                )
            )
        )
    |> Seq.sortByDescending(fun (_, info) -> info.date)
    |> Seq.toArray

let runFolders (OutputPath outputPathBase) (InputPath path) =
    let rec doRun outputPathBase (path: string) partentFolderName =
        let children =
            Directory.EnumerateDirectories(path)
            |> Seq.collect (fun childPath -> 
                let folderName = Path.GetFileName(childPath)
                doRun (Path.Combine(outputPathBase, partentFolderName)) (childPath) folderName
            )
            |> Seq.toArray

        printfn "Analyzing \"%s\"" path

        let currentFolder = runFolder (InputPath path) children partentFolderName

        let outputPath = Path.Combine(outputPathBase, partentFolderName)

        if Directory.Exists outputPath |> not then
            Directory.CreateDirectory outputPath |> ignore

        let count = 
            currentFolder
            |> Array.fold (fun count (content, info) ->
                let filePath = Path.Combine(outputPath, info.fileName)
                printfn "Writing %s to %s" info.title filePath
                writeFile filePath content

                count + 1
            ) 0

        printfn "Written %i files" count

        currentFolder

    doRun outputPathBase path ""

