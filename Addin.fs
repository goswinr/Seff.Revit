﻿namespace Seff.Revit

open Autodesk.Revit.UI
open Autodesk.Revit.DB
open Autodesk.Revit.Attributes
open System
open System.Windows
open Seff
open System.Windows.Input
open System.Reflection
open System.Collections.Concurrent


 [<AbstractClass; Sealed>]
 type Current private ()=     
     static member val internal SeffIsShown: bool     = false  with get,set
     static member val          SeffWindow: Window    = null  with get, set 


type internal RunEvHandler(seff:Seff, queue: ConcurrentQueue< UIApplication->unit >) =    
    member this.GetName() = "Run in Seff"
    member this.Execute(app:UIApplication) = 
        let ok,f = queue.TryDequeue()
        if ok then
            try
                f(app) 
            with e ->
                seff.Log.PrintFsiErrorMsg "Error caught in IExternalEventHandler: %A" e
                
    
    interface IExternalEventHandler with 
        member this.Execute(app:UIApplication)  = this.Execute(app)
        member this.GetName() = this.GetName()



[<Regeneration(RegenerationOption.Manual)>]  
[<Transaction(TransactionMode.Manual)>]
type StartEditorCommand() = // don't rename ! string referenced in  PushButtonData //new instance is created on every button click    
    interface IExternalCommand with
        member this.Execute(commandData: ExternalCommandData, message: byref<string>, elements: ElementSet): Result = 
            try                
                if not Current.SeffIsShown then 
                    Current.SeffWindow.Show()
                    Current.SeffIsShown <- true
                    Result.Succeeded
                else                    
                    Current.SeffWindow.Visibility <- Visibility.Visible
                    Result.Succeeded

            with ex ->
                TaskDialog.Show("Execute Button Seff", sprintf "%A" ex ) |> ignore 
                Result.Failed


[<Regeneration(RegenerationOption.Manual)>]
[<Transaction(TransactionMode.Manual)>]
[<AllowNullLiteralAttribute>]
type SeffAddin() = // don't rename ! string referenced in in seff.addin file 
    
    let mutable exEvent:ExternalEvent = null 
    
    let queue = ConcurrentQueue<UIApplication->unit>()
    
    static let mutable instance : SeffAddin = null

    static member Instance = instance
    
    member this.RunApp (f:UIApplication->unit) =  
        queue.Enqueue(f)
        exEvent.Raise() |> ignore 
    
    member this.RunDoc (f:Document->unit) =  
        queue.Enqueue(fun app -> f(app.ActiveUIDocument.Document))
        exEvent.Raise() |> ignore 
    
    // reverse a string
    member this.Test(s:String) = s |> Seq.rev |> Seq.map string |> String.concat "" 
   
    interface IExternalApplication with
        member this.OnStartup(uiConApp:UIControlledApplication) =
            try
                Sync.installSynchronizationContext()
                           
                // ------------------- create Ribbon and button -------------------------------------------
                let thisAssemblyPath = Reflection.Assembly.GetExecutingAssembly().Location     
                let button = new PushButtonData("Seff", "Open Fsharp Editor", thisAssemblyPath, "Seff.Revit.StartEditorCommand")
                button.ToolTip <- "This will open the Seff Editor Window"
            
                let uriImage = new Uri("pack://application:,,,/Seff.Revit;component/Media/LogoCursorTr32.png") // build from VS not via "dotnet build"  to include. <Resource Include="Media\LogoCursorTr32.png" />  
                let largeImage = new System.Windows.Media.Imaging.BitmapImage(uriImage)
                button.LargeImage <- largeImage
            
                let tabId = "Seff"
                uiConApp.CreateRibbonTab(tabId)
                let panel = uiConApp.CreateRibbonPanel(tabId,"Seff")            
                panel.AddItem(button) |> ignore
                

                //-------------- start Seff -------------------------------------------------------------                
                //https://thebuildingcoder.typepad.com/blog/2018/11/revit-window-handle-and-parenting-an-add-in-form.html
                let winHandle = Diagnostics.Process.GetCurrentProcess().MainWindowHandle
                let seff= Seff.App.createEditorForHosting(winHandle,"Revit")
                Current.SeffWindow <- seff.Window

                (* //TODO Alt enter does not work !
                seff.Window.KeyDown.Add(fun e -> //to avoid pressing alt to focus on menu and the diabeling Alt+Enter for Evaluationg selection in FSI                       
                    seff.Log.PrintDebugMsg "key: %A, sytem key: %A, mod: %A " e.Key e.SystemKey Keyboard.Modifiers 
                    //if e.Key = Key.LeftAlt || e.Key = Key.RightAlt then 
                    //    e.Handled <- true
                    //elif (Keyboard.Modifiers = ModifierKeys.Alt && e.Key = Key.Enter) ||
                    //   (Keyboard.Modifiers = ModifierKeys.Alt && e.Key = Key.Return) then 
                    //        seff.Fsi.Evaluate{code = seff.Tabs.CurrAvaEdit.SelectedText ; file=seff.Tabs.Current.FilePath; allOfFile=false}                          
                    //        e.Handled <- true
                    )                
                seff.Tabs.Control.PreviewKeyDown.Add (fun e -> 
                    if Keyboard.Modifiers = ModifierKeys.Alt && Keyboard.IsKeyDown(Key.Enter) then 
                        seff.Log.PrintDebugMsg "Alt+Enter"
                    elif Keyboard.Modifiers = ModifierKeys.Alt && Keyboard.IsKeyDown(Key.Return) then 
                        seff.Log.PrintDebugMsg "Alt+Return"
                    else
                        seff.Log.PrintDebugMsg "not Alt+Enter" 
                        )            *)

                seff.Window.Closing.Add (fun e -> 
                    match seff.Fsi.AskAndCancel() with
                    |Evaluating -> e.Cancel <- true // no closing
                    |Ready | Initalizing | NotLoaded -> 
                        seff.Window.Visibility <- Visibility.Hidden                           
                        e.Cancel <- true) // no closing
                
             
                //-------------- hook up Seff ------------------------------------------------------------- 
                let handler = RunEvHandler(seff, queue)
                exEvent <- ExternalEvent.Create(handler)                
                
                //TaskDialog.Show("uiConApp:", sprintf "%A" uiConApp.ControlledApplication.VersionNumber ) |> ignore 
                (*
                let uiApp =
                    let versionNumber = int uiConApp.ControlledApplication.VersionNumber
                    let fieldName = if versionNumber >= 2017 then  "m_uiapplication" else "m_application"
                    let fi = uiConApp.GetType().GetField(fieldName, BindingFlags.NonPublic ||| BindingFlags.Instance)
                    fi.GetValue(uiConApp) :?> UIApplication 
                uiApp.Idling.AddHandler(fun obj args -> 
                    let app = obj :?> UIApplication
                    let ok,f = Current.Queue.TryDequeue()
                    if ok then 
                        let doc = uiApp.ActiveUIDocument.Document
                        f(doc)                    
                        )  *)
                
                instance <- this

                Result.Succeeded

            with ex ->
                TaskDialog.Show("OnStartup of Seff.Revit.dll", sprintf "%A" ex ) |> ignore 
                Result.Failed

        
        member this.OnShutdown(app:UIControlledApplication) =           

            Result.Succeeded




// System.Guid.NewGuid().ToString().ToUpper() // for FSI