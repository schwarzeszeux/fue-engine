open System.IO
open Engine

[<EntryPoint>]
let main(args) =
    if args.Length <> 2 then
        printfn "Must provide exactly two arguments"
        1
    else
        let root = args.[0]
        let outputFolder = args.[1]

        if (Directory.Exists(outputFolder) |> not) then
            Directory.CreateDirectory(outputFolder) |> ignore

        let fsw = new FileSystemWatcher(root)
        fsw.IncludeSubdirectories <- true
        fsw.Changed.Add(fun file ->
            runFolders (OutputPath outputFolder) (InputPath root)
            |> ignore
            ()
        )
        fsw.Created.Add(fun file ->
            runFolders (OutputPath outputFolder) (InputPath root)
            |> ignore
            ()
        )
        fsw.Filter <- "*.fue"
        fsw.EnableRaisingEvents <- true


        runFolders (OutputPath outputFolder) (InputPath root)
        |> ignore
        
        
        System.Console.ReadLine() |> ignore
        0
